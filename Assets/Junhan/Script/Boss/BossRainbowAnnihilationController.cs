using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 크리피커피 3페이즈 Rainbow Core 전용 전멸기 컨트롤러입니다.
    /// 기존 BossCoreStateController의 RainbowHighlightChanged 이벤트를 사용합니다.
    /// Highlight된 색은 위험 지점, 나머지 색은 안전 지점입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossRainbowAnnihilationController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("보스 페이즈, 플레이어, 보스 중심 위치와 이동 잠금을 제공합니다. 비워 두면 자동 탐색합니다.")]
        [SerializeField] private BossController bossController;

        [Tooltip("Rainbow Highlight 상태와 이벤트를 제공합니다. 비워 두면 자동 탐색합니다.")]
        [SerializeField] private BossCoreStateController coreStateController;

        [Header("Auto Trigger / 자동 발동")]
        [Tooltip("체크하면 3페이즈 Rainbow Highlight 이벤트를 받아 자동 발동합니다.")]
        [SerializeField] private bool autoTriggerFromRainbowHighlight = true;

        [Tooltip("Rainbow Highlight 몇 회마다 전멸기를 발동할지 정합니다.")]
        [SerializeField, Min(1)] private int highlightsPerAnnihilation = 3;

        [Tooltip("연속 발동 방지를 위한 최소 간격입니다.")]
        [SerializeField, Min(0f)] private float minimumSecondsBetweenAnnihilations = 10f;

        [Header("Sequence / 전멸기 진행")]
        [Tooltip("색상 지점이 나타난 뒤 최종 판정까지의 대기시간입니다.")]
        [SerializeField, Min(0.1f)] private float telegraphDuration = 3f;

        [Tooltip("전멸기 진행 중 보스 이동을 잠급니다.")]
        [SerializeField] private bool lockBossMovementDuringSequence = true;

        [Tooltip("전멸기 진행 중 보스 몸통 접촉 피해를 막습니다.")]
        [SerializeField] private bool suppressBossContactDamageDuringSequence = true;

        [Tooltip("판정 후 지점을 잠깐 남겨 결과를 확인하는 시간입니다.")]
        [SerializeField, Min(0f)] private float resultLingerDuration = 0.35f;

        [Header("Color Zones / 색상 지점")]
        [Tooltip("보스 중심에서 각 Red / Yellow / Blue 지점 중심까지의 거리입니다.")]
        [SerializeField, Min(0.1f)] private float zoneDistanceFromBoss = 4.5f;

        [Tooltip("각 색상 지점의 판정 반경입니다.")]
        [SerializeField, Min(0.1f)] private float zoneRadius = 1.4f;

        [Tooltip("Red 지점 배치 각도입니다. 90이면 보스 위쪽입니다.")]
        [SerializeField] private float redZoneAngle = 90f;

        [Tooltip("Yellow 지점 배치 각도입니다.")]
        [SerializeField] private float yellowZoneAngle = 210f;

        [Tooltip("Blue 지점 배치 각도입니다.")]
        [SerializeField] private float blueZoneAngle = 330f;

        [Tooltip("체크하면 어떤 색상 지점에도 들어가지 않았을 때 실패 처리합니다.")]
        [SerializeField] private bool outsideAllZonesIsLethal = true;

        [Header("Lethal Damage / 실패 피해")]
        [Tooltip("기존 Caffeine Failure Wave와 같은 방식의 치명 피해 배율입니다.")]
        [SerializeField, Min(0.1f)] private float lethalDamageMultiplier = 2f;

        [Header("Runtime Zone Visual / 임시 시각화")]
        [Tooltip("체크하면 별도 아트 없이 LineRenderer 원으로 지점을 표시합니다.")]
        [SerializeField] private bool drawRuntimeZones = true;

        [Tooltip("원형 LineRenderer 선분 수입니다.")]
        [SerializeField, Range(24, 180)] private int zoneSegments = 64;

        [Tooltip("안전 지점 LineRenderer 두께입니다.")]
        [SerializeField, Min(0.01f)] private float safeZoneLineWidth = 0.16f;

        [Tooltip("위험 지점 LineRenderer 두께입니다.")]
        [SerializeField, Min(0.01f)] private float dangerZoneLineWidth = 0.30f;

        [Tooltip("안전 지점 알파값입니다.")]
        [SerializeField, Range(0.05f, 1f)] private float safeZoneAlpha = 0.45f;

        [Tooltip("위험 지점 최소 알파값입니다.")]
        [SerializeField, Range(0.05f, 1f)] private float dangerZoneMinAlpha = 0.55f;

        [Tooltip("위험 지점 최대 알파값입니다.")]
        [SerializeField, Range(0.05f, 1f)] private float dangerZoneMaxAlpha = 1f;

        [Tooltip("위험 지점 점멸 속도입니다.")]
        [SerializeField, Min(0.1f)] private float dangerPulseSpeed = 6f;

        [Tooltip("색상 지점 Sorting Order입니다.")]
        [SerializeField] private int zoneSortingOrder = 930;

        [Tooltip("Red 지점 색상입니다.")]
        [SerializeField] private Color redZoneColor = new Color(1f, 0.15f, 0.12f, 1f);

        [Tooltip("Yellow 지점 색상입니다.")]
        [SerializeField] private Color yellowZoneColor = new Color(1f, 0.82f, 0.12f, 1f);

        [Tooltip("Blue 지점 색상입니다.")]
        [SerializeField] private Color blueZoneColor = new Color(0.18f, 0.55f, 1f, 1f);

        [Header("Runtime - Read Only")]
        [Tooltip("현재 Rainbow Annihilation 진행 여부입니다.")]
        [SerializeField] private bool annihilationActive;

        [Tooltip("현재 전멸기의 위험 Core Trait입니다.")]
        [SerializeField] private BossCoreTrait capturedDangerTraits = BossCoreTrait.None;

        [Tooltip("현재까지 누적된 Rainbow Highlight 횟수입니다.")]
        [SerializeField] private int rainbowHighlightCount;

        [Tooltip("마지막 Rainbow Annihilation 시작 시간입니다.")]
        [SerializeField] private float lastAnnihilationStartTime = -999f;

        [Tooltip("마지막 판정에서 플레이어가 안전했는지 표시합니다.")]
        [SerializeField] private bool lastResultSafe;

        [Tooltip("마지막 판정에서 치명 피해를 요청했는지 표시합니다.")]
        [SerializeField] private bool lastPlayerHit;

        [Tooltip("RainbowHighlightChanged 이벤트 구독 상태입니다.")]
        [SerializeField] private bool highlightEventSubscribed;

        [Header("Debug")]
        [Tooltip("Context Menu 테스트에서 사용할 위험 속성입니다. 기본 Red + Yellow이면 Blue만 안전합니다.")]
        [SerializeField]
        private BossCoreTrait debugDangerTraits =
            BossCoreTrait.Red | BossCoreTrait.Yellow;

        [Tooltip("전멸기 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private sealed class ZoneRuntime
        {
            public BossCoreTrait Trait;
            public GameObject Root;
            public LineRenderer LineRenderer;
            public Material RuntimeMaterial;
            public Vector3 Position;
            public Color BaseColor;
            public bool IsDanger;
        }

        private readonly List<ZoneRuntime> activeZones = new List<ZoneRuntime>();

        private BossCoreStateController subscribedCoreStateController;
        private BossController subscribedBossController;
        private Coroutine annihilationRoutine;

        private bool movementLockApplied;
        private bool contactSuppressApplied;
        private bool corePatternLockApplied;
        private bool actionLockApplied;

        public bool IsAnnihilationActive => annihilationActive;
        public BossCoreTrait CapturedDangerTraits => capturedDangerTraits;
        public int RainbowHighlightCount => rainbowHighlightCount;
        public bool LastResultSafe => lastResultSafe;
        public bool LastPlayerHit => lastPlayerHit;

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
            if (bossController == null || coreStateController == null)
                ResolveReferences();

            SubscribeEvents();

            if (!annihilationActive)
                return;

            MaintainRuntimeLocks();
            UpdateDangerZonePulse();

            if (IsBossDead())
                CancelAnnihilation("Boss Dead", true);
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            CancelAnnihilation("Component Disabled", false);
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            CancelAnnihilation("Component Destroyed", false);
        }

        private void ResolveReferences()
        {
            Transform topRoot = transform.root != null ? transform.root : transform;

            if (bossController == null)
                bossController = topRoot.GetComponentInChildren<BossController>(true);

            if (coreStateController == null)
                coreStateController = topRoot.GetComponentInChildren<BossCoreStateController>(true);
        }

        private void SubscribeEvents()
        {
            SubscribeHighlightEvent();
            SubscribePhaseEvent();
        }

        private void SubscribeHighlightEvent()
        {
            if (subscribedCoreStateController == coreStateController && highlightEventSubscribed)
                return;

            if (subscribedCoreStateController != null && highlightEventSubscribed)
                subscribedCoreStateController.RainbowHighlightChanged -= HandleRainbowHighlightChanged;

            subscribedCoreStateController = coreStateController;
            highlightEventSubscribed = false;

            if (subscribedCoreStateController == null)
                return;

            subscribedCoreStateController.RainbowHighlightChanged += HandleRainbowHighlightChanged;
            highlightEventSubscribed = true;
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
            if (subscribedCoreStateController != null && highlightEventSubscribed)
                subscribedCoreStateController.RainbowHighlightChanged -= HandleRainbowHighlightChanged;

            if (subscribedBossController != null)
                subscribedBossController.PhaseChanged -= HandlePhaseChanged;

            subscribedCoreStateController = null;
            subscribedBossController = null;
            highlightEventSubscribed = false;
        }

        private void HandleRainbowHighlightChanged(BossCoreTrait highlightedTraits)
        {
            if (!autoTriggerFromRainbowHighlight ||
                annihilationActive ||
                highlightedTraits == BossCoreTrait.None)
                return;

            ResolveReferences();

            if (bossController == null || coreStateController == null)
                return;

            if (bossController.CurrentPhase != 3 ||
                coreStateController.CurrentMode != BossCoreMode.Rainbow)
                return;

            rainbowHighlightCount++;

            int requiredCount = Mathf.Max(1, highlightsPerAnnihilation);

            if (debugLog)
            {
                Debug.Log(
                    $"[BossRainbowAnnihilation] Rainbow Highlight Count | " +
                    $"{rainbowHighlightCount}/{requiredCount} | Traits={highlightedTraits}",
                    this);
            }

            if (rainbowHighlightCount < requiredCount)
                return;

            if (Time.time <
                lastAnnihilationStartTime + Mathf.Max(0f, minimumSecondsBetweenAnnihilations))
                return;

            rainbowHighlightCount = 0;
            StartAnnihilation(NormalizeDangerTraits(highlightedTraits), false);
        }

        private void HandlePhaseChanged(int newPhase)
        {
            rainbowHighlightCount = 0;

            if (newPhase != 3 && annihilationActive)
                CancelAnnihilation($"Phase Changed -> {newPhase}", true);
        }

        private void StartAnnihilation(BossCoreTrait dangerTraits, bool ignorePhaseForDebug)
        {
            if (annihilationActive || IsBossDead())
                return;

            ResolveReferences();

            if (bossController == null || bossController.PlayerCharacter == null)
            {
                Debug.LogWarning(
                    "[BossRainbowAnnihilation] BossController 또는 PlayerCharacter를 찾지 못했습니다.",
                    this);
                return;
            }

            if (!ignorePhaseForDebug)
            {
                if (bossController.CurrentPhase != 3 ||
                    coreStateController == null ||
                    coreStateController.CurrentMode != BossCoreMode.Rainbow)
                    return;
            }

            dangerTraits = NormalizeDangerTraits(dangerTraits);

            if (annihilationRoutine != null)
                StopCoroutine(annihilationRoutine);

            annihilationRoutine = StartCoroutine(AnnihilationRoutine(dangerTraits));
        }

        private IEnumerator AnnihilationRoutine(BossCoreTrait dangerTraits)
        {
            annihilationActive = true;
            capturedDangerTraits = dangerTraits;
            lastResultSafe = false;
            lastPlayerHit = false;
            lastAnnihilationStartTime = Time.time;

            ApplyRuntimeLocks();

            Vector3 origin = bossController.BossCenterPosition;
            CreateColorZones(origin, dangerTraits);

            if (debugLog)
            {
                Debug.LogWarning(
                    $"[BossRainbowAnnihilation] START | Danger={dangerTraits}, " +
                    $"Telegraph={telegraphDuration:0.##}s",
                    this);
            }

            float elapsed = 0f;
            while (elapsed < Mathf.Max(0.1f, telegraphDuration))
            {
                if (IsBossDead())
                {
                    CancelAnnihilation("Boss Dead During Telegraph", true);
                    yield break;
                }

                MaintainRuntimeLocks();
                StopBossMotion();

                elapsed += Time.deltaTime;
                yield return null;
            }

            ResolvePlayerResult();

            if (resultLingerDuration > 0f)
            {
                float linger = 0f;
                while (linger < resultLingerDuration)
                {
                    if (IsBossDead())
                        break;

                    MaintainRuntimeLocks();
                    StopBossMotion();
                    linger += Time.deltaTime;
                    yield return null;
                }
            }

            FinishAnnihilation("Resolved");
        }

        private void ResolvePlayerResult()
        {
            if (bossController == null || bossController.PlayerCharacter == null)
                return;

            Character player = bossController.PlayerCharacter;
            Transform playerTransform =
                player.CenterTransform != null ? player.CenterTransform : player.transform;

            ZoneRuntime enteredZone = FindContainingZone(playerTransform.position);

            bool isSafe = enteredZone == null
                ? !outsideAllZonesIsLethal
                : !enteredZone.IsDanger;

            lastResultSafe = isSafe;

            if (isSafe)
            {
                if (debugLog)
                {
                    Debug.LogWarning(
                        $"[BossRainbowAnnihilation] SAFE | " +
                        $"Zone={(enteredZone != null ? enteredZone.Trait.ToString() : "Outside")}",
                        this);
                }
                return;
            }

            ApplyLethalDamage(player, enteredZone);
        }

        private ZoneRuntime FindContainingZone(Vector2 playerPosition)
        {
            float finalRadius = Mathf.Max(0.1f, zoneRadius);
            float radiusSq = finalRadius * finalRadius;

            for (int i = 0; i < activeZones.Count; i++)
            {
                ZoneRuntime zone = activeZones[i];

                if (zone == null || zone.Root == null)
                    continue;

                Vector2 delta = playerPosition - (Vector2)zone.Position;

                if (delta.sqrMagnitude <= radiusSq)
                    return zone;
            }

            return null;
        }

        private void ApplyLethalDamage(Character player, ZoneRuntime enteredZone)
        {
            if (player == null)
                return;

            lastPlayerHit = true;

            float beforeHp = player.CurrentHealth;

            float lethalDamage = Mathf.Max(
                1f,
                player.CurrentHealth +
                player.CurrentArmor +
                player.MaxHealth * Mathf.Max(0.1f, lethalDamageMultiplier) +
                1f);

            player.TakeDamage(lethalDamage, Vector2.zero, false);

            float afterHp = player.CurrentHealth;

            if (debugLog)
            {
                Debug.LogWarning(
                    $"[BossRainbowAnnihilation] PLAYER HIT | " +
                    $"Zone={(enteredZone != null ? enteredZone.Trait.ToString() : "Outside")}, " +
                    $"Danger={capturedDangerTraits}, Requested={lethalDamage:0.##}, " +
                    $"HP={beforeHp:0.##}->{afterHp:0.##}",
                    this);
            }
        }

        private void CreateColorZones(Vector3 origin, BossCoreTrait dangerTraits)
        {
            CleanupZones();

            CreateZone(
                origin,
                BossCoreTrait.Red,
                redZoneAngle,
                redZoneColor,
                IsTraitIncluded(dangerTraits, BossCoreTrait.Red));

            CreateZone(
                origin,
                BossCoreTrait.Yellow,
                yellowZoneAngle,
                yellowZoneColor,
                IsTraitIncluded(dangerTraits, BossCoreTrait.Yellow));

            CreateZone(
                origin,
                BossCoreTrait.Blue,
                blueZoneAngle,
                blueZoneColor,
                IsTraitIncluded(dangerTraits, BossCoreTrait.Blue));
        }

        private void CreateZone(
            Vector3 origin,
            BossCoreTrait trait,
            float angleDegrees,
            Color baseColor,
            bool isDanger)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(
                Mathf.Cos(rad),
                Mathf.Sin(rad),
                0f) * Mathf.Max(0.1f, zoneDistanceFromBoss);

            Vector3 zonePosition = origin + offset;

            ZoneRuntime zone = new ZoneRuntime
            {
                Trait = trait,
                Position = zonePosition,
                BaseColor = baseColor,
                IsDanger = isDanger
            };

            if (drawRuntimeZones)
            {
                GameObject zoneObject = new GameObject($"Boss_RainbowZone_{trait}");
                zoneObject.transform.position = zonePosition;

                LineRenderer line = zoneObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = Mathf.Clamp(zoneSegments, 24, 180);

                float width = isDanger ? dangerZoneLineWidth : safeZoneLineWidth;
                line.startWidth = Mathf.Max(0.01f, width);
                line.endWidth = line.startWidth;
                line.numCapVertices = 0;
                line.numCornerVertices = 2;
                line.sortingOrder = zoneSortingOrder;

                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    Material runtimeMaterial = new Material(shader);
                    runtimeMaterial.name = $"Runtime_RainbowZone_{trait}";
                    line.sharedMaterial = runtimeMaterial;
                    zone.RuntimeMaterial = runtimeMaterial;
                }

                zone.Root = zoneObject;
                zone.LineRenderer = line;

                UpdateZoneCircle(zone);
                ApplyZoneColor(
                    zone,
                    isDanger ? dangerZoneMaxAlpha : safeZoneAlpha);
            }

            activeZones.Add(zone);

            if (debugLog)
            {
                Debug.Log(
                    $"[BossRainbowAnnihilation] ZONE | {trait} = " +
                    $"{(isDanger ? "DANGER" : "SAFE")} | Pos={zonePosition}",
                    this);
            }
        }

        private void UpdateZoneCircle(ZoneRuntime zone)
        {
            if (zone == null || zone.LineRenderer == null)
                return;

            LineRenderer line = zone.LineRenderer;
            int count = Mathf.Max(24, line.positionCount);
            float radius = Mathf.Max(0.1f, zoneRadius);

            for (int i = 0; i < count; i++)
            {
                float angle = (float)i / count * Mathf.PI * 2f;

                line.SetPosition(
                    i,
                    new Vector3(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius,
                        0f));
            }
        }

        private void UpdateDangerZonePulse()
        {
            if (!annihilationActive || !drawRuntimeZones)
                return;

            float pulse01 =
                0.5f +
                0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, dangerPulseSpeed));

            float dangerAlpha = Mathf.Lerp(
                Mathf.Clamp01(dangerZoneMinAlpha),
                Mathf.Clamp01(dangerZoneMaxAlpha),
                pulse01);

            for (int i = 0; i < activeZones.Count; i++)
            {
                ZoneRuntime zone = activeZones[i];

                if (zone == null || zone.LineRenderer == null)
                    continue;

                ApplyZoneColor(
                    zone,
                    zone.IsDanger ? dangerAlpha : safeZoneAlpha);
            }
        }

        private void ApplyZoneColor(ZoneRuntime zone, float alpha)
        {
            if (zone == null || zone.LineRenderer == null)
                return;

            Color color = zone.BaseColor;
            color.a = Mathf.Clamp01(alpha);

            zone.LineRenderer.startColor = color;
            zone.LineRenderer.endColor = color;
        }

        private void CleanupZones()
        {
            for (int i = activeZones.Count - 1; i >= 0; i--)
            {
                ZoneRuntime zone = activeZones[i];

                if (zone == null)
                    continue;

                if (zone.Root != null)
                    Destroy(zone.Root);

                if (zone.RuntimeMaterial != null)
                    Destroy(zone.RuntimeMaterial);
            }

            activeZones.Clear();
        }

        private void ApplyRuntimeLocks()
        {
            if (bossController != null)
            {
                bossController.SetExternalActionLock(true);
                actionLockApplied = true;

                if (lockBossMovementDuringSequence)
                {
                    bossController.SetExternalMovementLock(true);
                    movementLockApplied = true;
                }

                if (suppressBossContactDamageDuringSequence)
                {
                    bossController.SetSuppressContactDamage(true);
                    contactSuppressApplied = true;
                }
            }

            if (coreStateController != null)
            {
                coreStateController.SetPatternLocked(true);
                corePatternLockApplied = true;
            }
        }

        private void MaintainRuntimeLocks()
        {
            if (!annihilationActive)
            {
                return;
            }

            if (actionLockApplied &&
                bossController != null)
            {
                bossController.SetExternalActionLock(true);
            }

            if (movementLockApplied &&
                bossController != null)
            {
                bossController.SetExternalMovementLock(true);
            }

            if (contactSuppressApplied &&
                bossController != null)
            {
                bossController.SetSuppressContactDamage(true);
            }

            if (corePatternLockApplied &&
                coreStateController != null)
            {
                coreStateController.SetPatternLocked(true);
            }
        }

        private void ClearRuntimeLocks()
        {
            if (corePatternLockApplied &&
                coreStateController != null)
            {
                coreStateController.SetPatternLocked(false);
            }

            if (contactSuppressApplied &&
                bossController != null)
            {
                bossController.SetSuppressContactDamage(false);
            }

            if (movementLockApplied &&
                bossController != null)
            {
                bossController.SetExternalMovementLock(false);
            }

            if (actionLockApplied &&
                bossController != null)
            {
                bossController.SetExternalActionLock(false);
            }

            corePatternLockApplied = false;
            contactSuppressApplied = false;
            movementLockApplied = false;
            actionLockApplied = false;
        }

        private void StopBossMotion()
        {
            if (bossController == null || bossController.Rigidbody == null)
                return;

            bossController.Rigidbody.velocity = Vector2.zero;
            bossController.Rigidbody.angularVelocity = 0f;
        }

        private void FinishAnnihilation(string reason)
        {
            CleanupZones();
            ClearRuntimeLocks();

            annihilationActive = false;
            capturedDangerTraits = BossCoreTrait.None;
            annihilationRoutine = null;

            if (debugLog)
                Debug.Log($"[BossRainbowAnnihilation] END | {reason}", this);
        }

        private void CancelAnnihilation(string reason, bool stopRoutine)
        {
            if (stopRoutine && annihilationRoutine != null)
                StopCoroutine(annihilationRoutine);

            annihilationRoutine = null;

            CleanupZones();
            ClearRuntimeLocks();

            annihilationActive = false;
            capturedDangerTraits = BossCoreTrait.None;

            if (debugLog)
                Debug.Log($"[BossRainbowAnnihilation] CANCEL | {reason}", this);
        }

        private bool IsBossDead()
        {
            return bossController != null && bossController.IsDead;
        }

        private bool IsTraitIncluded(BossCoreTrait value, BossCoreTrait trait)
        {
            return (value & trait) != BossCoreTrait.None;
        }

        private BossCoreTrait NormalizeDangerTraits(BossCoreTrait value)
        {
            BossCoreTrait normalized = value & BossCoreTrait.All;

            if (normalized == BossCoreTrait.None || normalized == BossCoreTrait.All)
            {
                normalized =
                    BossCoreTrait.Red |
                    BossCoreTrait.Yellow;
            }

            return normalized;
        }

        [ContextMenu("Debug/Start Rainbow Annihilation")]
        private void DebugStartRainbowAnnihilation()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[BossRainbowAnnihilation] Play Mode에서 실행하세요.",
                    this);
                return;
            }

            StartAnnihilation(
                NormalizeDangerTraits(debugDangerTraits),
                true);
        }

        [ContextMenu("Debug/Cancel Rainbow Annihilation")]
        private void DebugCancelRainbowAnnihilation()
        {
            if (!Application.isPlaying)
                return;

            CancelAnnihilation("Debug Context Menu", true);

        }
    }
}
