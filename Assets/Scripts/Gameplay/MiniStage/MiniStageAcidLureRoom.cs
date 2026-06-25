using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 위산 유인 미니 스테이지 방.
    ///
    /// 플레이어가 몬스터를 3개의 위산 필드로 유도해서 녹이는 기믹방입니다.
    /// - 위산 필드 3개는 방 시작과 동시에 활성화됩니다.
    /// - 각 위산 필드는 지정된 수만큼 몬스터를 처치하면 사라집니다.
    /// - 모든 위산 필드가 사라지면 마지막으로 사라진 위산 필드 위치에 보상 상자가 생성됩니다.
    /// - 제한 시간이 지나면 방은 종료되지 않고, 귀환 상호작용만 열립니다.
    /// - 제한 시간 이후 플레이어는 계속 클리어를 노리거나, E키로 중도 퇴장할 수 있습니다.
    ///
    /// 이번 수정 핵심:
    /// - 몬스터 풀링 때문에 같은 Monster 인스턴스가 다시 스폰될 수 있으므로,
    ///   스폰 직후 해당 몬스터의 이전 위산 카운트 기록을 초기화합니다.
    /// </summary>
    public class MiniStageAcidLureRoom : MiniStageRoomBase
    {
        [Header("Acid Lure Fields")]
        [Tooltip("방 안에 배치할 미니 스테이지 전용 위산 필드들입니다. 삼각형 꼭짓점처럼 3개 배치하는 것을 추천합니다.")]
        [SerializeField] private MiniStageAcidLureField[] acidFields;

        [Tooltip("각 위산 필드가 완료되기 위해 처치해야 하는 몬스터 수입니다. 예: 15면 각 필드마다 15마리씩 필요합니다.")]
        [SerializeField] private int requiredKillsPerField = 15;

        [Header("Optional Exit")]
        [Tooltip("제한 시간이 지나면 클리어하지 못해도 귀환 상호작용을 열지 여부입니다. 방 자체는 종료하지 않습니다.")]
        [SerializeField] private bool unlockReturnAfterTimeLimit = true;

        [Tooltip("방 시작 후 몇 초가 지나면 선택형 중도 귀환을 허용할지 정합니다.")]
        [SerializeField] private float optionalReturnUnlockSeconds = 45f;

        [Tooltip("선택형 중도 귀환이 열릴 때 로그를 출력합니다.")]
        [SerializeField] private bool logOptionalReturnUnlock = true;

        [Header("Monster Spawn")]
        [Tooltip("스폰에 사용할 몬스터 풀 인덱스입니다. LevelBlueprint의 Monsters 배열 순서와 맞춰야 합니다.")]
        [SerializeField] private int monsterPoolIndex = 0;

        [Tooltip("이 방에서 스폰할 몬스터 Blueprint 목록입니다. 하나만 넣어도 되고, 여러 개를 넣으면 랜덤으로 선택됩니다.")]
        [SerializeField] private MonsterBlueprint[] monsterBlueprints;

        [Tooltip("스폰되는 몬스터에게 적용할 추가 HP 값입니다. 0이면 Blueprint 기본 체력을 사용합니다.")]
        [SerializeField] private float monsterHpBuff = 0f;

        [Tooltip("방 시작 직후 바로 생성할 몬스터 수입니다.")]
        [SerializeField] private int initialSpawnCount = 12;

        [Tooltip("방 안에 동시에 살아 있을 수 있는 최대 몬스터 수입니다.")]
        [SerializeField] private int maxAliveMonsterCount = 18;

        [Tooltip("몬스터를 추가로 생성하는 간격입니다.")]
        [SerializeField] private float spawnInterval = 0.7f;

        [Tooltip("몬스터를 생성할 위치 목록입니다. 비워두면 PlayerStartPoint 주변 랜덤 위치를 사용합니다.")]
        [SerializeField] private Transform[] monsterSpawnPoints;

        [Tooltip("Monster Spawn Points가 비어 있을 때 PlayerStartPoint 주변에서 몬스터가 생성될 반경입니다.")]
        [SerializeField] private float fallbackSpawnRadius = 5f;

        [Tooltip("방이 완전 클리어되거나 플레이어가 중도 퇴장할 때 남아 있는 몬스터를 제거할지 여부입니다.")]
        [SerializeField] private bool clearRemainingMonstersOnEnd = true;

        [Header("Debug")]
        [Tooltip("위산 유인방 진행 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        [Tooltip("몬스터가 스폰될 때 위산 카운트 기록 초기화 로그를 출력합니다.")]
        [SerializeField] private bool debugForgetMonsterHistory = false;

        private readonly List<Monster> spawnedMonsters = new List<Monster>();
        private readonly HashSet<Monster> acidKilledMonsters = new HashSet<Monster>();

        private Coroutine spawnRoutine;
        private Coroutine optionalReturnRoutine;

        private bool roomRunning;
        private bool roomEnded;
        private bool optionalReturnUnlockedByTimer;

        private Vector3 lastCompletedFieldPosition;

        protected override void OnInitRoom()
        {
            ResolveAcidFields();

            for (int i = 0; i < acidFields.Length; i++)
            {
                if (acidFields[i] == null)
                {
                    continue;
                }

                acidFields[i].Initialize(this, requiredKillsPerField);
            }
        }

        protected override void OnBeginRoom()
        {
            ResolveAcidFields();

            roomRunning = true;
            roomEnded = false;
            optionalReturnUnlockedByTimer = false;

            spawnedMonsters.Clear();
            acidKilledMonsters.Clear();

            lastCompletedFieldPosition = transform.position;

            for (int i = 0; i < acidFields.Length; i++)
            {
                if (acidFields[i] == null)
                {
                    continue;
                }

                acidFields[i].Initialize(this, requiredKillsPerField);
                acidFields[i].ResetField();
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

            if (debugLog)
            {
                Debug.Log(
                    $"[MiniStageAcidLureRoom] 위산 유인방 시작. " +
                    $"fields={acidFields.Length}, requiredKillsPerField={requiredKillsPerField}, " +
                    $"optionalReturnTime={optionalReturnUnlockSeconds}"
                );
            }
        }

        private IEnumerator MonsterSpawnRoutine()
        {
            while (roomRunning && !roomEnded && !AreAllFieldsCompleted())
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

            if (AreAllFieldsCompleted())
            {
                yield break;
            }

            optionalReturnUnlockedByTimer = true;

            UnlockOptionalReturn();

            if (logOptionalReturnUnlock || debugLog)
            {
                Debug.Log(
                    $"[MiniStageAcidLureRoom] {optionalReturnUnlockSeconds}초 경과. " +
                    "방은 계속 진행되며, 플레이어가 원하면 ReturnBloodClot으로 중도 퇴장할 수 있습니다."
                );
            }

            optionalReturnRoutine = null;
        }

        /// <summary>
        /// 위산 필드가 몬스터 처치 카운트를 올릴 수 있는지 확인합니다.
        ///
        /// 같은 몬스터가 여러 필드에 동시에 걸렸을 때 중복 카운트를 막기 위해 사용합니다.
        /// 단, 몬스터 풀링으로 같은 Monster 인스턴스가 다시 스폰될 수 있으므로,
        /// SpawnOneMonster()에서 이 기록을 제거해 새 스폰 몬스터는 다시 카운트 가능하게 만듭니다.
        /// </summary>
        public bool TryRegisterAcidKill(MiniStageAcidLureField field, Monster monster)
        {
            if (!roomRunning || roomEnded)
            {
                return false;
            }

            if (field == null || monster == null)
            {
                return false;
            }

            if (field.IsCompleted)
            {
                return false;
            }

            if (acidKilledMonsters.Contains(monster))
            {
                return false;
            }

            acidKilledMonsters.Add(monster);
            return true;
        }

        public void NotifyMonsterDissolved(MiniStageAcidLureField field, Monster monster)
        {
            if (field == null)
            {
                return;
            }

            CleanupSpawnedMonsterList();

            if (debugLog)
            {
                Debug.Log(
                    $"[MiniStageAcidLureRoom] 위산 필드 처치 카운트 갱신. " +
                    $"field={field.name}, progress={field.CurrentKillCount}/{field.RequiredKillCount}"
                );
            }
        }

        public void NotifyFieldCompleted(MiniStageAcidLureField field)
        {
            if (roomEnded)
            {
                return;
            }

            if (field == null)
            {
                return;
            }

            lastCompletedFieldPosition = field.transform.position;

            if (debugLog)
            {
                Debug.Log($"[MiniStageAcidLureRoom] 위산 필드 완료: {field.name}");
            }

            if (!AreAllFieldsCompleted())
            {
                return;
            }

            CompleteAcidLureRoomWithReward();
        }

        private void CompleteAcidLureRoomWithReward()
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

            if (debugLog)
            {
                Debug.Log(
                    $"[MiniStageAcidLureRoom] 모든 위산 필드 완료. " +
                    $"마지막 필드 위치에 보상 상자를 생성합니다. position={lastCompletedFieldPosition}"
                );
            }

            CompleteRoom(lastCompletedFieldPosition);
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

        private void SpawnOneMonster()
        {
            if (entityManager == null)
            {
                Debug.LogWarning("[MiniStageAcidLureRoom] EntityManager가 없어 몬스터를 스폰할 수 없습니다.");
                return;
            }

            MonsterBlueprint selectedBlueprint = PickMonsterBlueprint();

            if (selectedBlueprint == null)
            {
                Debug.LogWarning("[MiniStageAcidLureRoom] Monster Blueprint가 비어 있어 몬스터를 스폰할 수 없습니다.");
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

            // 핵심 수정:
            // 같은 Monster 컴포넌트가 풀링으로 재사용될 수 있으므로,
            // 새로 스폰된 시점에는 이전 위산 카운트 기록을 반드시 제거합니다.
            ForgetMonsterKillHistory(monster);

            spawnedMonsters.Add(monster);

            if (debugLog)
            {
                Debug.Log(
                    $"[MiniStageAcidLureRoom] 몬스터 스폰. " +
                    $"monster={monster.name}, position={spawnPosition}, alive={spawnedMonsters.Count}/{maxAliveMonsterCount}"
                );
            }
        }

        private void ForgetMonsterKillHistory(Monster monster)
        {
            if (monster == null)
            {
                return;
            }

            acidKilledMonsters.Remove(monster);

            ResolveAcidFields();

            if (acidFields != null)
            {
                for (int i = 0; i < acidFields.Length; i++)
                {
                    if (acidFields[i] == null)
                    {
                        continue;
                    }

                    acidFields[i].ForgetMonsterHistory(monster);
                }
            }

            if (debugForgetMonsterHistory)
            {
                Debug.Log($"[MiniStageAcidLureRoom] 풀링 재사용 몬스터 위산 기록 초기화: {monster.name}");
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

            return validBlueprints[Random.Range(0, validBlueprints.Count)];
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
                    return validSpawnPoints[Random.Range(0, validSpawnPoints.Count)].position;
                }
            }

            Vector2 center = PlayerStartPoint != null
                ? PlayerStartPoint.position
                : transform.position;

            Vector2 direction = Random.insideUnitCircle.normalized;

            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector2.right;
            }

            return center + direction * fallbackSpawnRadius;
        }

        private void ResolveAcidFields()
        {
            if (acidFields != null && acidFields.Length > 0)
            {
                return;
            }

            acidFields = GetComponentsInChildren<MiniStageAcidLureField>(true);
        }

        private bool AreAllFieldsCompleted()
        {
            ResolveAcidFields();

            if (acidFields == null || acidFields.Length == 0)
            {
                return false;
            }

            bool hasValidField = false;

            for (int i = 0; i < acidFields.Length; i++)
            {
                if (acidFields[i] == null)
                {
                    continue;
                }

                hasValidField = true;

                if (!acidFields[i].IsCompleted)
                {
                    return false;
                }
            }

            return hasValidField;
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

        protected override void OnCleanupRoom()
        {
            roomRunning = false;
            roomEnded = true;

            StopRunningCoroutines();

            if (clearRemainingMonstersOnEnd)
            {
                RemoveRemainingMonsters();
            }

            if (acidFields != null)
            {
                for (int i = 0; i < acidFields.Length; i++)
                {
                    if (acidFields[i] != null)
                    {
                        acidFields[i].CleanupField();
                    }
                }
            }

            spawnedMonsters.Clear();
            acidKilledMonsters.Clear();

            if (debugLog)
            {
                Debug.Log("[MiniStageAcidLureRoom] 방 정리 완료.");
            }
        }
    }
}