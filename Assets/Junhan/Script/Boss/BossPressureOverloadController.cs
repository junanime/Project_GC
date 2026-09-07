using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// Pressure Gauge 100%에서 발동하는 크리피커피 압력 과부하 기믹입니다.
    /// 3초 전조 -> Pressure Node 생성 -> 6초 내 전부 파괴 판정 -> 성공/실패 처리 -> Gauge 소비.
    /// Pressure Node 피격은 이미 검증된 BossCaffeineInjector를 임시 재사용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossPressureOverloadController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("PressureFilled 이벤트와 게이지 리셋을 담당합니다.")]
        [SerializeField] private BossPressureGaugeController pressureGaugeController;

        [Tooltip("보스 전체 Max HP와 Core Part를 제공합니다.")]
        [SerializeField] private BossPartDamageTestRootController partRootController;

        [Tooltip("플레이어, 보스 상태, 이동 잠금을 제공합니다.")]
        [SerializeField] private BossController bossController;

        [Tooltip("성공 시 Groggy/Core Open을 적용합니다.")]
        [SerializeField] private BossCoreDamageStateBridge coreDamageStateBridge;

        [Header("Telegraph / 과부하 전조")]
        [Tooltip("Pressure 100% 후 Node 생성 전 전조 시간입니다.")]
        [SerializeField, Min(0f)] private float telegraphDuration = 3f;

        [Tooltip("전조 표시용 프리팹입니다. 기존 BossWarningCircle을 사용할 수 있습니다.")]
        [SerializeField] private GameObject telegraphPrefab;

        [Tooltip("전조 프리팹 크기 배율입니다.")]
        [SerializeField, Min(0.1f)] private float telegraphScale = 3f;

        [Tooltip("전조 시작 시 보스 중심에 생성할 선택 VFX입니다.")]
        [SerializeField] private GameObject telegraphVfxPrefab;

        [Header("Pressure Nodes / 압력 노드")]
        [Tooltip("임시 Pressure Node 프리팹입니다. 현재 테스트에서는 기존 Caffeine Injector 프리팹을 재사용할 수 있습니다.")]
        [SerializeField] private GameObject pressureNodePrefab;

        [Tooltip("한 번의 과부하에서 생성할 Pressure Node 수입니다.")]
        [SerializeField, Min(1)] private int pressureNodeCount = 3;

        [Tooltip("각 Pressure Node의 HP입니다.")]
        [SerializeField, Min(1f)] private float pressureNodeHp = 10f;

        [Tooltip("보스 중심에서 Pressure Node를 배치할 거리입니다.")]
        [SerializeField, Min(0f)] private float nodeSpawnRadius = 1.8f;

        [Tooltip("첫 Pressure Node 배치 각도입니다. 90이면 첫 노드가 위쪽입니다.")]
        [SerializeField] private float nodeSpawnAngleOffset = 90f;

        [Tooltip("체크하면 Node가 보스 이동을 따라가도록 BossController 자식으로 생성합니다.")]
        [SerializeField] private bool parentNodesToBoss = true;

        [Tooltip("Node 생성 후 모두 파괴해야 하는 제한시간입니다.")]
        [SerializeField, Min(0.1f)] private float nodeResponseDuration = 6f;

        [Tooltip("Node 생성 시 사용할 선택 VFX입니다.")]
        [SerializeField] private GameObject nodeSpawnVfxPrefab;

        [Tooltip("Node 파괴 시 사용할 선택 VFX입니다.")]
        [SerializeField] private GameObject nodeDestroyVfxPrefab;

        [Tooltip("체크하면 Pressure Node SpriteRenderer Sorting Order를 강제합니다.")]
        [SerializeField] private bool forceNodeSortingOrder = true;

        [Tooltip("Pressure Node Sorting Order입니다.")]
        [SerializeField] private int nodeSortingOrder = 650;

        [Header("Success / 성공")]
        [Tooltip("성공 시 Core에 들어가는 BossSelf 피해입니다. 보스 전체 Max HP 비율이며 0.12는 12%입니다.")]
        [SerializeField, Range(0f, 1f)] private float successBossMaxHpDamagePercent = 0.12f;

        [Tooltip("성공 후 Core Groggy가 유지되는 시간입니다.")]
        [SerializeField, Min(0f)] private float successGroggyDuration = 3f;

        [Tooltip("성공 시 보스 중심에 생성할 선택 VFX입니다.")]
        [SerializeField] private GameObject successVfxPrefab;

        [Header("Failure / 실패 압력 파동")]
        [Tooltip("실패 시 보스 중심에 생성할 선택 VFX입니다.")]
        [SerializeField] private GameObject failureVfxPrefab;

        [Tooltip("실패 확정 후 압력 파동이 퍼지기 전 경고 시간입니다.")]
        [SerializeField, Min(0f)] private float failureWaveTelegraphDuration = 0.75f;

        [Tooltip("압력 파동이 최대 반경까지 확장되는 시간입니다.")]
        [SerializeField, Min(0.05f)] private float failureWaveExpansionDuration = 2.5f;

        [Tooltip("압력 파동 시작 반경입니다.")]
        [SerializeField, Min(0f)] private float failureWaveStartRadius = 0.5f;

        [Tooltip("압력 파동 최대 반경입니다.")]
        [SerializeField, Min(0.1f)] private float failureWaveMaxRadius = 30f;

        [Tooltip("플레이어와 충돌하는 파동 띠 두께입니다.")]
        [SerializeField, Min(0.05f)] private float failureWaveHitThickness = 1f;

        [Tooltip("기존 Caffeine Failure Wave와 같은 치명 피해 계산 배율입니다.")]
        [SerializeField, Min(0.1f)] private float failureWaveLethalDamageMultiplier = 2f;

        [Tooltip("체크하면 별도 아트 없이 LineRenderer 원형 파동을 표시합니다.")]
        [SerializeField] private bool drawFailureRuntimeRing = true;

        [Tooltip("런타임 원형 파동 선분 수입니다.")]
        [SerializeField, Range(24, 180)] private int failureRuntimeRingSegments = 96;

        [Tooltip("런타임 원형 파동 선 두께입니다.")]
        [SerializeField, Min(0.01f)] private float failureRuntimeRingWidth = 0.18f;

        [Tooltip("런타임 압력 파동 색상입니다.")]
        [SerializeField] private Color failureRuntimeRingColor = new Color(1f, 0.18f, 0.08f, 0.92f);

        [Tooltip("런타임 압력 파동 Sorting Order입니다.")]
        [SerializeField] private int failureRuntimeRingSortingOrder = 920;

        [Tooltip("최대 반경 도달 후 파동이 남아 있는 시간입니다.")]
        [SerializeField, Min(0f)] private float failureWaveEndLinger = 0.2f;

        [Header("Runtime - Read Only")]
        [Tooltip("현재 Pressure Overload 진행 여부입니다.")]
        [SerializeField] private bool overloadActive;

        [Tooltip("현재 전조 단계 여부입니다.")]
        [SerializeField] private bool telegraphActive;

        [Tooltip("현재 Node 파괴 챌린지 단계 여부입니다.")]
        [SerializeField] private bool nodeChallengeActive;

        [Tooltip("현재 실패 파동 단계 여부입니다.")]
        [SerializeField] private bool failureSequenceActive;

        [Tooltip("현재 살아 있는 Pressure Node 개수입니다.")]
        [SerializeField] private int remainingNodeCount;

        [Tooltip("현재 Node 챌린지 경과 시간입니다.")]
        [SerializeField] private float currentChallengeElapsed;

        [Tooltip("마지막 과부하 성공 여부입니다.")]
        [SerializeField] private bool lastOverloadSucceeded;

        [Tooltip("마지막 과부하 실패 여부입니다.")]
        [SerializeField] private bool lastOverloadFailed;

        [Tooltip("PressureFilled 이벤트 구독 상태입니다.")]
        [SerializeField] private bool pressureEventSubscribed;

        [Header("Debug")]
        [Tooltip("과부하 시작/노드/성공/실패/리셋 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private readonly List<BossCaffeineInjector> activeNodes = new List<BossCaffeineInjector>();
        private BossPressureGaugeController subscribedPressureGauge;
        private BossController subscribedBossController;
        private Coroutine overloadRoutine;
        private GameObject activeTelegraph;
        private BossCaffeineFailureWave activeFailureWave;
        private bool successGroggyApplied;
        private bool failureRuntimeLockApplied;

        public bool IsOverloadActive => overloadActive;
        public int RemainingNodeCount => remainingNodeCount;
        public float CurrentChallengeElapsed => currentChallengeElapsed;
        public bool LastOverloadSucceeded => lastOverloadSucceeded;
        public bool LastOverloadFailed => lastOverloadFailed;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeEvents();
        }

        private void Start()
        {
            ResolveReferences();
            SubscribeEvents();
        }

        private void Update()
        {
            if (pressureGaugeController == null || bossController == null || partRootController == null)
            {
                ResolveReferences();
            }

            SubscribeEvents();

            if (overloadActive && IsBossDead())
            {
                CancelActiveOverload("Boss Dead", false);
            }
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            CancelActiveOverload("Component Disabled", false);
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            CancelActiveOverload("Component Destroyed", false);
        }

        private void ResolveReferences()
        {
            Transform topRoot = transform.root != null ? transform.root : transform;

            if (pressureGaugeController == null)
                pressureGaugeController = topRoot.GetComponentInChildren<BossPressureGaugeController>(true);

            if (partRootController == null)
                partRootController = topRoot.GetComponentInChildren<BossPartDamageTestRootController>(true);

            if (bossController == null)
                bossController = topRoot.GetComponentInChildren<BossController>(true);

            if (coreDamageStateBridge == null)
                coreDamageStateBridge = topRoot.GetComponentInChildren<BossCoreDamageStateBridge>(true);
        }

        private void SubscribeEvents()
        {
            SubscribePressureEvent();
            SubscribePhaseEvent();
        }

        private void SubscribePressureEvent()
        {
            if (subscribedPressureGauge == pressureGaugeController && pressureEventSubscribed)
                return;

            if (subscribedPressureGauge != null && pressureEventSubscribed)
                subscribedPressureGauge.PressureFilled -= HandlePressureFilled;

            subscribedPressureGauge = pressureGaugeController;
            pressureEventSubscribed = false;

            if (subscribedPressureGauge == null)
                return;

            subscribedPressureGauge.PressureFilled += HandlePressureFilled;
            pressureEventSubscribed = true;
        }

        private void SubscribePhaseEvent()
        {
            if (subscribedBossController == bossController)
                return;

            if (subscribedBossController != null)
                subscribedBossController.PhaseChanged -= HandlePhaseChanged;

            subscribedBossController = bossController;

            if (subscribedBossController != null)
                subscribedBossController.PhaseChanged += HandlePhaseChanged;
        }

        private void UnsubscribeEvents()
        {
            if (subscribedPressureGauge != null && pressureEventSubscribed)
                subscribedPressureGauge.PressureFilled -= HandlePressureFilled;

            if (subscribedBossController != null)
                subscribedBossController.PhaseChanged -= HandlePhaseChanged;

            subscribedPressureGauge = null;
            subscribedBossController = null;
            pressureEventSubscribed = false;
        }

        private void HandlePressureFilled()
        {
            if (overloadActive || IsBossDead())
                return;

            if (overloadRoutine != null)
                StopCoroutine(overloadRoutine);

            overloadRoutine = StartCoroutine(OverloadRoutine());
        }

        private void HandlePhaseChanged(int newPhase)
        {
            if (!overloadActive)
                return;

            // Gauge는 Phase 변경 시 자체 리셋되므로 이전 Phase의 과부하도 함께 취소합니다.
            CancelActiveOverload($"Phase Changed -> {newPhase}", false);
        }

        private IEnumerator OverloadRoutine()
        {
            overloadActive = true;
            telegraphActive = true;
            nodeChallengeActive = false;
            failureSequenceActive = false;
            lastOverloadSucceeded = false;
            lastOverloadFailed = false;
            remainingNodeCount = 0;
            currentChallengeElapsed = 0f;

            CleanupNodes();
            DestroyActiveTelegraph();
            SpawnTelegraph();

            if (telegraphVfxPrefab != null && bossController != null)
                Instantiate(telegraphVfxPrefab, bossController.BossCenterPosition, Quaternion.identity);

            if (debugLog)
                Debug.LogWarning($"[BossPressureOverload] START | Telegraph={telegraphDuration:0.##}s", this);

            float telegraphElapsed = 0f;
            while (telegraphElapsed < Mathf.Max(0f, telegraphDuration))
            {
                if (IsBossDead())
                {
                    CancelActiveOverload("Boss Dead During Telegraph", false);
                    yield break;
                }

                UpdateTelegraphPosition();
                telegraphElapsed += Time.deltaTime;
                yield return null;
            }

            DestroyActiveTelegraph();
            telegraphActive = false;

            SpawnPressureNodes();
            remainingNodeCount = CountAliveNodes();

            if (remainingNodeCount <= 0)
            {
                Debug.LogWarning(
                    "[BossPressureOverload] Pressure Node를 생성하지 못했습니다. Pressure Node Prefab/Collider를 확인하세요.",
                    this);
                FinishAndConsumePressure("Node Spawn Failed");
                yield break;
            }

            nodeChallengeActive = true;
            currentChallengeElapsed = 0f;

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPressureOverload] NODE CHALLENGE | Count={remainingNodeCount}, Limit={nodeResponseDuration:0.##}s",
                    this);
            }

            while (nodeChallengeActive)
            {
                if (IsBossDead())
                {
                    CancelActiveOverload("Boss Dead During Node Challenge", false);
                    yield break;
                }

                CleanupNodeList();
                remainingNodeCount = CountAliveNodes();

                if (remainingNodeCount <= 0)
                {
                    yield return HandleSuccess();
                    yield break;
                }

                currentChallengeElapsed += Time.deltaTime;

                if (currentChallengeElapsed >= Mathf.Max(0.1f, nodeResponseDuration))
                {
                    yield return HandleFailure();
                    yield break;
                }

                yield return null;
            }
        }

        private void SpawnTelegraph()
        {
            if (telegraphPrefab == null || bossController == null)
                return;

            activeTelegraph = Instantiate(
                telegraphPrefab,
                bossController.BossCenterPosition,
                Quaternion.identity);

            activeTelegraph.name = "Boss_PressureOverload_Telegraph";
            activeTelegraph.transform.localScale = Vector3.one * Mathf.Max(0.1f, telegraphScale);
        }

        private void UpdateTelegraphPosition()
        {
            if (activeTelegraph != null && bossController != null)
                activeTelegraph.transform.position = bossController.BossCenterPosition;
        }

        private void DestroyActiveTelegraph()
        {
            if (activeTelegraph == null)
                return;

            Destroy(activeTelegraph);
            activeTelegraph = null;
        }

        private void SpawnPressureNodes()
        {
            CleanupNodes();

            if (pressureNodePrefab == null || bossController == null)
                return;

            int finalCount = Mathf.Max(1, pressureNodeCount);
            float angleStep = 360f / finalCount;

            for (int i = 0; i < finalCount; i++)
            {
                float angle = nodeSpawnAngleOffset + angleStep * i;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * nodeSpawnRadius;
                Vector3 spawnPosition = bossController.BossCenterPosition + offset;

                GameObject nodeObject = Instantiate(pressureNodePrefab, spawnPosition, Quaternion.identity);
                if (nodeObject == null)
                    continue;

                nodeObject.name = $"Boss_PressureNode_{i + 1}";

                if (parentNodesToBoss)
                    nodeObject.transform.SetParent(bossController.transform, true);

                BossCaffeineInjector node = nodeObject.GetComponent<BossCaffeineInjector>();
                if (node == null)
                    node = nodeObject.GetComponentInChildren<BossCaffeineInjector>(true);
                if (node == null)
                    node = nodeObject.AddComponent<BossCaffeineInjector>();

                node.Setup(null, pressureNodeHp, nodeDestroyVfxPrefab, debugLog);
                ApplyNodeSortingOrder(nodeObject);
                activeNodes.Add(node);

                if (nodeSpawnVfxPrefab != null)
                    Instantiate(nodeSpawnVfxPrefab, spawnPosition, Quaternion.identity);
            }
        }

        private void ApplyNodeSortingOrder(GameObject nodeObject)
        {
            if (!forceNodeSortingOrder || nodeObject == null)
                return;

            SpriteRenderer[] renderers = nodeObject.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].sortingOrder = nodeSortingOrder;
            }
        }

        private IEnumerator HandleSuccess()
        {
            nodeChallengeActive = false;
            lastOverloadSucceeded = true;
            lastOverloadFailed = false;
            remainingNodeCount = 0;

            if (successVfxPrefab != null && bossController != null)
                Instantiate(successVfxPrefab, bossController.BossCenterPosition, Quaternion.identity);

            float actualDamage = ApplySuccessCoreBackfire();

            if (debugLog)
            {
                Debug.LogWarning(
                    $"[BossPressureOverload] SUCCESS | Time={currentChallengeElapsed:0.##}s, " +
                    $"CoreActualDamage={actualDamage:0.##}, Groggy={successGroggyDuration:0.##}s",
                    this);
            }

            if (!IsBossDead() && successGroggyDuration > 0f)
                yield return ApplySuccessGroggy();

            FinishAndConsumePressure("Pressure Overload Success");
        }

        private float ApplySuccessCoreBackfire()
        {
            if (partRootController == null || partRootController.TotalMaxHealth <= 0f)
                return 0f;

            BossPartDamageTestPart corePart = partRootController.CorePart;
            if (corePart == null || corePart.IsBroken)
            {
                if (debugLog)
                {
                    Debug.LogWarning(
                        "[BossPressureOverload] 살아 있는 Core Part를 찾지 못해 성공 BossSelf 피해를 적용하지 못했습니다.",
                        this);
                }
                return 0f;
            }

            float requestedDamage =
                partRootController.TotalMaxHealth * Mathf.Clamp01(successBossMaxHpDamagePercent);

            return corePart.TakeDamageFromSource(
                requestedDamage,
                Vector2.zero,
                false,
                BossDamageSourceType.BossSelf);
        }

        private IEnumerator ApplySuccessGroggy()
        {
            ResolveReferences();

            if (coreDamageStateBridge == null)
            {
                Debug.LogWarning(
                    "[BossPressureOverload] BossCoreDamageStateBridge를 찾지 못했습니다. 성공 피해는 적용되지만 Groggy/Core x2는 적용되지 않습니다.",
                    this);
                yield break;
            }

            successGroggyApplied = true;
            coreDamageStateBridge.SetGroggy(true);

            if (bossController != null)
            {
                bossController.SetExternalMovementLock(true);
                bossController.SetSuppressContactDamage(true);
            }

            float endTime = Time.time + Mathf.Max(0f, successGroggyDuration);
            while (Time.time < endTime)
            {
                if (IsBossDead())
                    break;

                StopBossMotion();
                yield return null;
            }

            ClearSuccessGroggy();
        }

        private IEnumerator HandleFailure()
        {
            nodeChallengeActive = false;
            lastOverloadSucceeded = false;
            lastOverloadFailed = true;
            failureSequenceActive = true;

            if (failureVfxPrefab != null && bossController != null)
                Instantiate(failureVfxPrefab, bossController.BossCenterPosition, Quaternion.identity);

            if (debugLog)
            {
                Debug.LogWarning(
                    $"[BossPressureOverload] FAILURE | Limit={nodeResponseDuration:0.##}s, " +
                    $"Remain={CountAliveNodes()} | Pressure Wave START",
                    this);
            }

            ForceDestroyRemainingNodes(true);
            remainingNodeCount = 0;

            if (IsBossDead())
            {
                FinishAndConsumePressure("Pressure Overload Failure - Boss Dead");
                yield break;
            }

            if (bossController != null)
            {
                bossController.SetExternalMovementLock(true);
                bossController.SetSuppressContactDamage(true);
                failureRuntimeLockApplied = true;
            }

            SpawnFailureWave();

            while (activeFailureWave != null && !activeFailureWave.IsFinished)
            {
                if (IsBossDead())
                {
                    activeFailureWave.CancelWave();
                    break;
                }

                StopBossMotion();
                yield return null;
            }

            ClearFailureSequence();
            FinishAndConsumePressure("Pressure Overload Failure");
        }

        private void SpawnFailureWave()
        {
            if (bossController == null || bossController.PlayerCharacter == null)
                return;

            if (activeFailureWave != null)
            {
                activeFailureWave.CancelWave();
                activeFailureWave = null;
            }

            GameObject waveObject = new GameObject("Boss_PressureOverloadFailureWave");
            waveObject.transform.position = bossController.BossCenterPosition;
            activeFailureWave = waveObject.AddComponent<BossCaffeineFailureWave>();

            activeFailureWave.Setup(
                bossController.PlayerCharacter,
                bossController.BossCenterPosition,
                failureWaveTelegraphDuration,
                failureWaveExpansionDuration,
                failureWaveStartRadius,
                failureWaveMaxRadius,
                failureWaveHitThickness,
                failureWaveLethalDamageMultiplier,
                drawFailureRuntimeRing,
                failureRuntimeRingSegments,
                failureRuntimeRingWidth,
                failureRuntimeRingColor,
                failureRuntimeRingSortingOrder,
                failureWaveEndLinger,
                debugLog);
        }

        private void ClearSuccessGroggy()
        {
            if (!successGroggyApplied)
                return;

            if (coreDamageStateBridge != null)
                coreDamageStateBridge.SetGroggy(false);

            if (bossController != null)
            {
                bossController.SetSuppressContactDamage(false);
                bossController.SetExternalMovementLock(false);
            }

            successGroggyApplied = false;
        }

        private void ClearFailureSequence()
        {
            if (activeFailureWave != null)
            {
                if (!activeFailureWave.IsFinished)
                    activeFailureWave.CancelWave();

                activeFailureWave = null;
            }

            if (failureRuntimeLockApplied && bossController != null)
            {
                bossController.SetSuppressContactDamage(false);
                bossController.SetExternalMovementLock(false);
            }

            failureRuntimeLockApplied = false;
            failureSequenceActive = false;
        }

        private void StopBossMotion()
        {
            if (bossController == null || bossController.Rigidbody == null)
                return;

            bossController.Rigidbody.velocity = Vector2.zero;
            bossController.Rigidbody.angularVelocity = 0f;
        }

        private int CountAliveNodes()
        {
            int count = 0;
            for (int i = 0; i < activeNodes.Count; i++)
            {
                BossCaffeineInjector node = activeNodes[i];
                if (node != null && !node.IsDestroyed)
                    count++;
            }
            return count;
        }

        private void CleanupNodeList()
        {
            for (int i = activeNodes.Count - 1; i >= 0; i--)
            {
                BossCaffeineInjector node = activeNodes[i];
                if (node == null || node.IsDestroyed)
                    activeNodes.RemoveAt(i);
            }
        }

        private void ForceDestroyRemainingNodes(bool spawnDestroyVfx)
        {
            for (int i = activeNodes.Count - 1; i >= 0; i--)
            {
                BossCaffeineInjector node = activeNodes[i];
                if (node == null || node.IsDestroyed)
                    continue;

                if (spawnDestroyVfx && nodeDestroyVfxPrefab != null)
                    Instantiate(nodeDestroyVfxPrefab, node.transform.position, Quaternion.identity);

                node.ForceDestroy();
            }

            activeNodes.Clear();
        }

        private void CleanupNodes()
        {
            ForceDestroyRemainingNodes(false);
            remainingNodeCount = 0;
        }

        private void FinishAndConsumePressure(string reason)
        {
            CleanupNodes();
            DestroyActiveTelegraph();
            ClearSuccessGroggy();
            ClearFailureSequence();

            telegraphActive = false;
            nodeChallengeActive = false;
            overloadActive = false;
            overloadRoutine = null;

            if (pressureGaugeController != null && !IsBossDead())
                pressureGaugeController.ConsumePressure(reason);

            if (debugLog)
                Debug.Log($"[BossPressureOverload] END | {reason}", this);
        }

        private void CancelActiveOverload(string reason, bool consumePressure)
        {
            if (overloadRoutine != null)
            {
                StopCoroutine(overloadRoutine);
                overloadRoutine = null;
            }

            CleanupNodes();
            DestroyActiveTelegraph();
            ClearSuccessGroggy();
            ClearFailureSequence();

            telegraphActive = false;
            nodeChallengeActive = false;
            overloadActive = false;
            currentChallengeElapsed = 0f;

            if (consumePressure && pressureGaugeController != null && !IsBossDead())
                pressureGaugeController.ConsumePressure(reason);

            if (debugLog)
                Debug.Log($"[BossPressureOverload] CANCEL | {reason}", this);
        }

        private bool IsBossDead()
        {
            if (partRootController != null && partRootController.IsBossDead)
                return true;

            return bossController != null && bossController.IsDead;
        }

        [ContextMenu("Debug/Start Pressure Overload")]
        private void DebugStartPressureOverload()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[BossPressureOverload] Play Mode에서 실행하세요.", this);
                return;
            }

            HandlePressureFilled();
        }
    }
}
