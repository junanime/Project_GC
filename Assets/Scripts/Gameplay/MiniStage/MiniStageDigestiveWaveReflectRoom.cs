using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 소화 파동 반사방.
    ///
    /// 플레이어가 거울 오브젝트를 공격해서 각도를 조정하고,
    /// 발사기에서 나온 소화 파동을 거울에 반사시켜 목표 코어까지 연결하는 미니 스테이지 방입니다.
    ///
    /// 이번 수정 핵심:
    /// - Beam Hit Layer Mask 설정이 조금 틀려도 거울/목표 코어 콜라이더 레이어를 자동 포함합니다.
    /// - Trigger Collider도 빔 Raycast가 감지할 수 있게 옵션을 추가했습니다.
    /// - Raycast가 몬스터/플레이어를 잘못 맞아 빔이 끊기지 않도록 필터링합니다.
    /// - 거울을 맞으면 확실히 MiniStageWaveMirror.GetReflectionNormal()을 통해 반사됩니다.
    /// </summary>
    public class MiniStageDigestiveWaveReflectRoom : MiniStageRoomBase
    {
        [Header("Wave Beam References")]
        [Tooltip("소화 파동이 시작되는 위치입니다. 이 Transform의 오른쪽 방향이 기본 발사 방향입니다.")]
        [SerializeField] private Transform beamStartPoint;

        [Tooltip("소화 파동의 목표 코어입니다. 빔이 이 오브젝트에 닿으면 클리어 게이지가 차오릅니다.")]
        [SerializeField] private MiniStageWaveReceiver waveReceiver;

        [Tooltip("방 안의 반사 거울들입니다. 비워두면 자식 오브젝트에서 자동으로 찾습니다.")]
        [SerializeField] private MiniStageWaveMirror[] mirrors;

        [Tooltip("소화 파동을 그릴 LineRenderer입니다. 비워두면 자동으로 생성합니다.")]
        [SerializeField] private LineRenderer beamLineRenderer;

        [Tooltip("보상 상자를 생성할 위치입니다. 비워두면 목표 코어 위치에 생성합니다. 목표 코어도 없으면 MiniStageRoomBase의 기본 Reward Spawn Point를 사용합니다.")]
        [SerializeField] private Transform rewardDropPoint;

        [Header("Wave Beam Rule")]
        [Tooltip("소화 파동이 충돌 판정에 사용할 기본 레이어입니다. 거울, 목표 코어, 벽 레이어를 포함하세요.")]
        [SerializeField] private LayerMask beamHitLayerMask;

        [Tooltip("거울과 목표 코어에 붙어 있는 Collider2D의 레이어를 Beam Hit Layer Mask에 자동으로 추가합니다. LayerMask 설정 실수를 줄이기 위해 true를 추천합니다.")]
        [SerializeField] private bool autoAppendMirrorAndReceiverLayersToBeamMask = true;

        [Tooltip("Trigger Collider도 빔 Raycast에 감지되게 할지 여부입니다. BeamReflectCollider나 WaveReceiver Collider가 Trigger일 수 있으므로 true를 추천합니다.")]
        [SerializeField] private bool includeTriggerCollidersInBeamRaycast = true;

        [Tooltip("빔이 몬스터나 플레이어를 맞아 끊기지 않도록 무시할지 여부입니다. 거울의 ProjectileHitbox는 부모에 MiniStageWaveMirror가 있으므로 무시되지 않습니다.")]
        [SerializeField] private bool ignoreCharactersAndMonstersForBeam = true;

        [Tooltip("한 번의 직선 빔이 최대 몇 유닛까지 뻗을지 정합니다.")]
        [SerializeField] private float maxSegmentDistance = 30f;

        [Tooltip("최대 반사 횟수입니다. 거울이 너무 많아도 무한 반사되지 않게 제한합니다.")]
        [SerializeField] private int maxReflectionCount = 8;

        [Tooltip("빔 시작 지점이 발사기 콜라이더와 겹치지 않도록 앞으로 살짝 밀어내는 거리입니다.")]
        [SerializeField] private float beamStartForwardOffset = 0.05f;

        [Tooltip("반사 후 다음 레이캐스트 시작 지점을 충돌 지점에서 살짝 밀어내는 거리입니다.")]
        [SerializeField] private float reflectedRayStartOffset = 0.04f;

        [Tooltip("목표 코어에 빔이 몇 초 동안 유지되어야 클리어되는지 정합니다.")]
        [SerializeField] private float receiverHoldDuration = 0.25f;

        [Tooltip("빔 경로를 매 프레임 다시 계산할지 여부입니다. 거울 회전이 실시간 반영되려면 true를 추천합니다.")]
        [SerializeField] private bool updateBeamEveryFrame = true;

        [Header("Beam Visual")]
        [Tooltip("빔 LineRenderer를 자동 생성할지 여부입니다.")]
        [SerializeField] private bool autoCreateLineRenderer = true;

        [Tooltip("빔의 시작/끝 두께입니다.")]
        [SerializeField] private float beamWidth = 0.12f;

        [Tooltip("빔 색상입니다.")]
        [SerializeField] private Color beamColor = new Color(1f, 0.85f, 0.1f, 1f);

        [Tooltip("빔 LineRenderer의 Sorting Layer 이름입니다.")]
        [SerializeField] private string beamSortingLayerName = "Default";

        [Tooltip("빔 LineRenderer의 Order in Layer입니다. 플레이어/몬스터보다 아래에 두려면 -500 근처를 추천합니다.")]
        [SerializeField] private int beamSortingOrder = -500;

        [Header("Optional Exit")]
        [Tooltip("제한 시간이 지나면 클리어하지 못해도 귀환 상호작용을 열지 여부입니다. 방 자체는 계속 진행됩니다.")]
        [SerializeField] private bool unlockReturnAfterTimeLimit = true;

        [Tooltip("방 시작 후 몇 초가 지나면 선택형 중도 귀환을 허용할지 정합니다.")]
        [SerializeField] private float optionalReturnUnlockSeconds = 45f;

        [Header("Monster Spawn")]
        [Tooltip("스폰에 사용할 몬스터 풀 인덱스입니다. LevelBlueprint의 Monsters 배열 순서와 맞춰야 합니다.")]
        [SerializeField] private int monsterPoolIndex = 0;

        [Tooltip("이 방에서 스폰할 몬스터 Blueprint 목록입니다. 하나만 넣어도 되고, 여러 개를 넣으면 랜덤으로 선택됩니다.")]
        [SerializeField] private MonsterBlueprint[] monsterBlueprints;

        [Tooltip("스폰되는 몬스터에게 적용할 추가 HP 값입니다. 0이면 Blueprint 기본 체력을 사용합니다.")]
        [SerializeField] private float monsterHpBuff = 0f;

        [Tooltip("방 시작 직후 바로 생성할 몬스터 수입니다.")]
        [SerializeField] private int initialSpawnCount = 6;

        [Tooltip("방 안에 동시에 살아 있을 수 있는 최대 몬스터 수입니다.")]
        [SerializeField] private int maxAliveMonsterCount = 10;

        [Tooltip("몬스터를 추가로 생성하는 간격입니다.")]
        [SerializeField] private float spawnInterval = 1.2f;

        [Tooltip("몬스터를 생성할 위치 목록입니다. 비워두면 PlayerStartPoint 주변 랜덤 위치를 사용합니다.")]
        [SerializeField] private Transform[] monsterSpawnPoints;

        [Tooltip("Monster Spawn Points가 비어 있을 때 PlayerStartPoint 주변에서 몬스터가 생성될 반경입니다.")]
        [SerializeField] private float fallbackSpawnRadius = 5f;

        [Tooltip("방이 완전 클리어되거나 플레이어가 중도 퇴장할 때 남아 있는 몬스터를 제거할지 여부입니다.")]
        [SerializeField] private bool clearRemainingMonstersOnEnd = true;

     

        [Tooltip("빔이 맞힌 오브젝트 정보를 로그로 출력합니다. 문제 확인용입니다.")]
        [SerializeField] private bool debugBeamHitLog = false;

        [Tooltip("빔 Raycast에 실제로 사용되는 LayerMask 값을 로그로 출력합니다.")]
        [SerializeField] private bool debugRuntimeLayerMask = false;

        private readonly List<Vector3> beamPoints = new List<Vector3>();
        private readonly List<Monster> spawnedMonsters = new List<Monster>();
        private readonly List<Collider2D> tempMirrorBeamColliders = new List<Collider2D>();

        private Coroutine spawnRoutine;
        private Coroutine optionalReturnRoutine;

        private bool roomRunning;
        private bool roomEnded;
        

        private float receiverHitTimer;

        protected override void OnInitRoom()
        {
            ResolveReferences();
            PrepareLineRenderer();

            if (waveReceiver != null)
            {
                waveReceiver.ResetReceiver();
            }

            if (mirrors != null)
            {
                for (int i = 0; i < mirrors.Length; i++)
                {
                    if (mirrors[i] != null)
                    {
                        mirrors[i].ResetRuntimeState();
                    }
                }
            }
        }

        protected override void OnBeginRoom()
        {
            ResolveReferences();
            PrepareLineRenderer();

            roomRunning = true;
            roomEnded = false;
            receiverHitTimer = 0f;

            spawnedMonsters.Clear();

            if (waveReceiver != null)
            {
                waveReceiver.ResetReceiver();
            }

            if (mirrors != null)
            {
                for (int i = 0; i < mirrors.Length; i++)
                {
                    if (mirrors[i] != null)
                    {
                        mirrors[i].ResetRuntimeState();
                    }
                }
            }

            for (int i = 0; i < initialSpawnCount; i++)
            {
                SpawnOneMonster();
            }

            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
            }

            spawnRoutine = StartCoroutine(MonsterSpawnRoutine());

            if (optionalReturnRoutine != null)
            {
                StopCoroutine(optionalReturnRoutine);
            }

            if (unlockReturnAfterTimeLimit)
            {
                optionalReturnRoutine = StartCoroutine(OptionalReturnUnlockRoutine());
            }

            RecalculateBeamPath();

            if (debugLog)
            {
                Debug.Log(
                    $"[MiniStageDigestiveWaveReflectRoom] 소화 파동 반사방 시작. " +
                    $"mirrors={(mirrors != null ? mirrors.Length : 0)}, holdDuration={receiverHoldDuration}"
                );
            }

            if (debugRuntimeLayerMask)
            {
                Debug.Log(
                    $"[MiniStageDigestiveWaveReflectRoom] Runtime Beam LayerMask={GetRuntimeBeamLayerMask().value}"
                );
            }
        }

        private void Update()
        {
            if (!roomRunning || roomEnded)
            {
                return;
            }

            if (!updateBeamEveryFrame)
            {
                return;
            }

            bool beamReachedReceiver = RecalculateBeamPath();
            UpdateReceiverHold(beamReachedReceiver);
        }

        private void UpdateReceiverHold(bool beamReachedReceiver)
        {
            if (waveReceiver == null)
            {
                return;
            }

            waveReceiver.SetBeamReceiving(beamReachedReceiver);

            if (beamReachedReceiver)
            {
                receiverHitTimer += Time.deltaTime;

                if (receiverHitTimer >= Mathf.Max(0.01f, receiverHoldDuration))
                {
                    CompleteRoomWithReward();
                }
            }
            else
            {
                receiverHitTimer = 0f;
            }
        }

        private bool RecalculateBeamPath()
        {
            beamPoints.Clear();

            if (beamStartPoint == null)
            {
                if (debugLog)
                {
                    Debug.LogWarning("[MiniStageDigestiveWaveReflectRoom] Beam Start Point가 비어 있습니다.");
                }

                ApplyBeamLine();
                return false;
            }

            Vector2 direction = beamStartPoint.right.normalized;

            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector2.right;
            }

            Vector2 origin = (Vector2)beamStartPoint.position + direction * beamStartForwardOffset;

            beamPoints.Add(origin);

            bool reachedReceiver = false;

            for (int reflectionIndex = 0; reflectionIndex <= maxReflectionCount; reflectionIndex++)
            {
                RaycastHit2D hit = FindNearestBeamHit(origin, direction);

                if (hit.collider == null)
                {
                    Vector2 endPoint = origin + direction * maxSegmentDistance;
                    beamPoints.Add(endPoint);
                    break;
                }

                beamPoints.Add(hit.point);

                if (debugBeamHitLog)
                {
                    Debug.Log(
                        $"[MiniStageDigestiveWaveReflectRoom] Beam Hit | " +
                        $"collider={hit.collider.name}, layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}, " +
                        $"point={hit.point}, normal={hit.normal}, distance={hit.distance:0.00}"
                    );
                }

                MiniStageWaveReceiver receiver = hit.collider.GetComponentInParent<MiniStageWaveReceiver>();

                if (receiver != null && receiver == waveReceiver)
                {
                    reachedReceiver = true;
                    break;
                }

                MiniStageWaveMirror mirror = hit.collider.GetComponentInParent<MiniStageWaveMirror>();

                if (mirror != null && mirror.IsReflective)
                {
                    Vector2 reflectNormal = mirror.GetReflectionNormal(hit.point, direction, hit.normal);
                    direction = Vector2.Reflect(direction, reflectNormal).normalized;
                    origin = hit.point + direction * reflectedRayStartOffset;
                    continue;
                }

                // 거울도 아니고 목표도 아니면 벽/차단물로 보고 빔 종료.
                break;
            }

            ApplyBeamLine();
            return reachedReceiver;
        }

        private RaycastHit2D FindNearestBeamHit(Vector2 origin, Vector2 direction)
        {
            LayerMask runtimeMask = GetRuntimeBeamLayerMask();

            if (runtimeMask.value == 0)
            {
                if (debugLog)
                {
                    Debug.LogWarning(
                        "[MiniStageDigestiveWaveReflectRoom] Beam Hit Layer Mask가 비어 있습니다. " +
                        "MiniStageReflector / MiniStageWaveTarget / MiniStageWaveBlocker 또는 거울 콜라이더 레이어를 확인하세요."
                    );
                }

                return default;
            }

            bool previousQueriesHitTriggers = Physics2D.queriesHitTriggers;

            if (includeTriggerCollidersInBeamRaycast)
            {
                Physics2D.queriesHitTriggers = true;
            }

            RaycastHit2D[] hits = Physics2D.RaycastAll(
                origin,
                direction,
                maxSegmentDistance,
                runtimeMask
            );

            Physics2D.queriesHitTriggers = previousQueriesHitTriggers;

            if (hits == null || hits.Length == 0)
            {
                return default;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit2D hit = hits[i];

                if (hit.collider == null)
                {
                    continue;
                }

                if (hit.distance <= 0.01f)
                {
                    continue;
                }

                if (!IsValidBeamHit(hit.collider))
                {
                    if (debugBeamHitLog)
                    {
                        Debug.Log(
                            $"[MiniStageDigestiveWaveReflectRoom] Beam Hit 무시 | " +
                            $"collider={hit.collider.name}, layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}"
                        );
                    }

                    continue;
                }

                return hit;
            }

            return default;
        }

        private bool IsValidBeamHit(Collider2D hitCollider)
        {
            if (hitCollider == null)
            {
                return false;
            }

            MiniStageWaveMirror mirror = hitCollider.GetComponentInParent<MiniStageWaveMirror>();

            if (mirror != null)
            {
                return true;
            }

            MiniStageWaveReceiver receiver = hitCollider.GetComponentInParent<MiniStageWaveReceiver>();

            if (receiver != null)
            {
                return true;
            }

            if (ignoreCharactersAndMonstersForBeam)
            {
                Character character = hitCollider.GetComponentInParent<Character>();

                if (character != null)
                {
                    return false;
                }

                Monster monster = hitCollider.GetComponentInParent<Monster>();

                if (monster != null)
                {
                    return false;
                }
            }

            // 거울/목표가 아니고 몬스터/플레이어도 아니면 벽이나 차단물로 인정.
            return true;
        }

        private LayerMask GetRuntimeBeamLayerMask()
        {
            int mask = beamHitLayerMask.value;

            if (!autoAppendMirrorAndReceiverLayersToBeamMask)
            {
                return mask;
            }

            ResolveReferences();

            if (mirrors != null)
            {
                for (int i = 0; i < mirrors.Length; i++)
                {
                    MiniStageWaveMirror mirror = mirrors[i];

                    if (mirror == null)
                    {
                        continue;
                    }

                    tempMirrorBeamColliders.Clear();
                    mirror.CollectBeamReflectColliders(tempMirrorBeamColliders);

                    for (int j = 0; j < tempMirrorBeamColliders.Count; j++)
                    {
                        Collider2D collider = tempMirrorBeamColliders[j];

                        if (collider == null)
                        {
                            continue;
                        }

                        mask |= 1 << collider.gameObject.layer;
                    }
                }
            }

            if (waveReceiver != null)
            {
                Collider2D[] receiverColliders = waveReceiver.GetComponentsInChildren<Collider2D>(true);

                for (int i = 0; i < receiverColliders.Length; i++)
                {
                    if (receiverColliders[i] == null)
                    {
                        continue;
                    }

                    mask |= 1 << receiverColliders[i].gameObject.layer;
                }
            }

            return mask;
        }

        private void ApplyBeamLine()
        {
            if (beamLineRenderer == null)
            {
                return;
            }

            beamLineRenderer.positionCount = beamPoints.Count;

            for (int i = 0; i < beamPoints.Count; i++)
            {
                beamLineRenderer.SetPosition(i, beamPoints[i]);
            }
        }

        private void PrepareLineRenderer()
        {
            if (beamLineRenderer == null && autoCreateLineRenderer)
            {
                GameObject lineObject = new GameObject("Digestive Wave Beam Line");
                lineObject.transform.SetParent(transform);
                lineObject.transform.localPosition = Vector3.zero;
                lineObject.transform.localRotation = Quaternion.identity;
                lineObject.transform.localScale = Vector3.one;

                beamLineRenderer = lineObject.AddComponent<LineRenderer>();
            }

            if (beamLineRenderer == null)
            {
                return;
            }

            beamLineRenderer.useWorldSpace = true;
            beamLineRenderer.widthMultiplier = Mathf.Max(0.01f, beamWidth);
            beamLineRenderer.startColor = beamColor;
            beamLineRenderer.endColor = beamColor;
            beamLineRenderer.sortingLayerName = beamSortingLayerName;
            beamLineRenderer.sortingOrder = beamSortingOrder;

            if (beamLineRenderer.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");

                if (shader != null)
                {
                    beamLineRenderer.sharedMaterial = new Material(shader);
                }
            }
        }

        private IEnumerator MonsterSpawnRoutine()
        {
            while (roomRunning && !roomEnded)
            {
                CleanupSpawnedMonsterList();

                if (spawnedMonsters.Count < maxAliveMonsterCount)
                {
                    SpawnOneMonster();
                }

                yield return new WaitForSeconds(Mathf.Max(0.1f, spawnInterval));
            }

            spawnRoutine = null;
        }

        private IEnumerator OptionalReturnUnlockRoutine()
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, optionalReturnUnlockSeconds));

            if (roomEnded)
            {
                yield break;
            }

           
            UnlockOptionalReturn();

            if (debugLog)
            {
                Debug.Log(
                    $"[MiniStageDigestiveWaveReflectRoom] {optionalReturnUnlockSeconds}초 경과. " +
                    "방은 계속 진행되며, 플레이어가 원하면 ReturnBloodClot으로 중도 퇴장할 수 있습니다."
                );
            }

            optionalReturnRoutine = null;
        }

        private void CompleteRoomWithReward()
        {
            if (roomEnded)
            {
                return;
            }

            roomEnded = true;
            roomRunning = false;

            StopRunningCoroutines();

            if (clearRemainingMonstersOnEnd)
            {
                RemoveRemainingMonsters();
            }

            if (waveReceiver != null)
            {
                waveReceiver.SetSolved(true);
            }

            Vector3? rewardPosition = null;

            if (rewardDropPoint != null)
            {
                rewardPosition = rewardDropPoint.position;
            }
            else if (waveReceiver != null)
            {
                rewardPosition = waveReceiver.transform.position;
            }

            if (debugLog)
            {
                Debug.Log("[MiniStageDigestiveWaveReflectRoom] 목표 코어에 소화 파동 연결 완료. 보상 상자를 생성합니다.");
            }

            CompleteRoom(rewardPosition);
        }

        private void SpawnOneMonster()
        {
            if (entityManager == null)
            {
                Debug.LogWarning("[MiniStageDigestiveWaveReflectRoom] EntityManager가 없어 몬스터를 스폰할 수 없습니다.");
                return;
            }

            MonsterBlueprint selectedBlueprint = PickMonsterBlueprint();

            if (selectedBlueprint == null)
            {
                Debug.LogWarning("[MiniStageDigestiveWaveReflectRoom] Monster Blueprint가 비어 있어 몬스터를 스폰할 수 없습니다.");
                return;
            }

            Vector2 spawnPosition = GetMonsterSpawnPosition();

            Monster monster = entityManager.SpawnMonster(
                monsterPoolIndex,
                spawnPosition,
                selectedBlueprint,
                monsterHpBuff,
                true
            );

            if (monster == null)
            {
                return;
            }

            spawnedMonsters.Add(monster);

            if (debugLog)
            {
                Debug.Log(
                    $"[MiniStageDigestiveWaveReflectRoom] 몬스터 스폰. " +
                    $"monster={monster.name}, position={spawnPosition}, alive={spawnedMonsters.Count}/{maxAliveMonsterCount}"
                );
            }
        }

        private MonsterBlueprint PickMonsterBlueprint()
        {
            if (monsterBlueprints == null || monsterBlueprints.Length == 0)
            {
                return null;
            }

            List<MonsterBlueprint> validBlueprints = new List<MonsterBlueprint>();

            for (int i = 0; i < monsterBlueprints.Length; i++)
            {
                if (monsterBlueprints[i] != null)
                {
                    validBlueprints.Add(monsterBlueprints[i]);
                }
            }

            if (validBlueprints.Count == 0)
            {
                return null;
            }

            return validBlueprints[UnityEngine.Random.Range(0, validBlueprints.Count)];
        }

        private Vector2 GetMonsterSpawnPosition()
        {
            if (monsterSpawnPoints != null && monsterSpawnPoints.Length > 0)
            {
                List<Transform> validSpawnPoints = new List<Transform>();

                for (int i = 0; i < monsterSpawnPoints.Length; i++)
                {
                    if (monsterSpawnPoints[i] != null)
                    {
                        validSpawnPoints.Add(monsterSpawnPoints[i]);
                    }
                }

                if (validSpawnPoints.Count > 0)
                {
                    return validSpawnPoints[UnityEngine.Random.Range(0, validSpawnPoints.Count)].position;
                }
            }

            Vector2 center = PlayerStartPoint != null
                ? PlayerStartPoint.position
                : transform.position;

            Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;

            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector2.right;
            }

            return center + direction * fallbackSpawnRadius;
        }

        private void CleanupSpawnedMonsterList()
        {
            for (int i = spawnedMonsters.Count - 1; i >= 0; i--)
            {
                Monster monster = spawnedMonsters[i];

                if (monster == null ||
                    !monster.gameObject.activeInHierarchy ||
                    monster.HP <= 0f)
                {
                    spawnedMonsters.RemoveAt(i);
                }
            }
        }

        private void RemoveRemainingMonsters()
        {
            for (int i = spawnedMonsters.Count - 1; i >= 0; i--)
            {
                Monster monster = spawnedMonsters[i];

                if (monster == null)
                {
                    spawnedMonsters.RemoveAt(i);
                    continue;
                }

                if (!monster.gameObject.activeInHierarchy || monster.HP <= 0f)
                {
                    spawnedMonsters.RemoveAt(i);
                    continue;
                }

                monster.StartCoroutine(monster.Killed(false));
                spawnedMonsters.RemoveAt(i);
            }
        }

        private void StopRunningCoroutines()
        {
            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }

            if (optionalReturnRoutine != null)
            {
                StopCoroutine(optionalReturnRoutine);
                optionalReturnRoutine = null;
            }
        }

        private void ResolveReferences()
        {
            if (waveReceiver == null)
            {
                waveReceiver = GetComponentInChildren<MiniStageWaveReceiver>(true);
            }

            if (mirrors == null || mirrors.Length == 0)
            {
                mirrors = GetComponentsInChildren<MiniStageWaveMirror>(true);
            }

            if (beamStartPoint == null)
            {
                Transform found = transform.Find("WaveEmitter/BeamStartPoint");

                if (found != null)
                {
                    beamStartPoint = found;
                }
            }
        }

        protected override void OnCleanupRoom()
        {
            roomRunning = false;
            roomEnded = true;

            StopRunningCoroutines();

            if (clearRemainingMonstersOnEnd)
            {
                RemoveRemainingMonsters();
            }

            spawnedMonsters.Clear();
            beamPoints.Clear();

            if (beamLineRenderer != null)
            {
                beamLineRenderer.positionCount = 0;
            }

            if (waveReceiver != null)
            {
                waveReceiver.ResetReceiver();
            }

            if (debugLog)
            {
                Debug.Log("[MiniStageDigestiveWaveReflectRoom] 방 정리 완료.");
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (beamStartPoint == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(
                beamStartPoint.position,
                beamStartPoint.position + beamStartPoint.right * 2f
            );
        }
    }
}