using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 기존 StageEventDirector를 건드리지 않고,
    /// 추가 필드 이벤트 3종을 따로 관리하는 보조 디렉터입니다.
    ///
    /// 담당 이벤트:
    /// 1. 산성 역류 파도
    /// 2. 연동운동 기류
    /// 3. 커피수혈 타임
    ///
    /// 기존 StageEventDirector가 이미 몬스터 증가 / 골드 / 위산분비를 관리하므로,
    /// 이 스크립트는 같은 오브젝트에 추가해서 확장용으로 사용합니다.
    /// </summary>
    public class AdvancedStageFieldEventDirector : MonoBehaviour
    {
        [System.Serializable]
        public class EventStartTimeRange
        {
            [Tooltip("랜덤 시작 최소 시간입니다. 단위는 분입니다. 2 = 2분")]
            public float minStartMinute = 2f;

            [Tooltip("랜덤 시작 최대 시간입니다. 단위는 분입니다. 4 = 4분")]
            public float maxStartMinute = 4f;

            public float GetRandomStartTimeSeconds()
            {
                float min = Mathf.Min(minStartMinute, maxStartMinute);
                float max = Mathf.Max(minStartMinute, maxStartMinute);
                return Random.Range(min, max) * 60f;
            }

            public bool IsValid()
            {
                float min = Mathf.Min(minStartMinute, maxStartMinute);
                float max = Mathf.Max(minStartMinute, maxStartMinute);
                return max > 0f && !Mathf.Approximately(min, max);
            }
        }

        [System.Serializable]
        public class AcidRefluxWaveEvent
        {
            [Header("Event Info")]
            [Tooltip("UI에 표시될 이벤트 이름입니다.")]
            public string eventName = "산성 역류 파도";

            [Tooltip("체크되어 있으면 이벤트가 발동됩니다.")]
            public bool enabled = true;

            [Header("Timing")]
            [Tooltip("랜덤 시간을 쓰지 않을 때 사용하는 고정 시작 시간입니다. 단위는 초입니다.")]
            public float startTime = 90f;

            [Tooltip("체크하면 아래 시간 범위 중 하나를 골라 랜덤 시작 시간을 정합니다.")]
            public bool useRandomStartTime = true;

            [Tooltip("랜덤 시작 시간 범위입니다. 단위는 분입니다.")]
            public List<EventStartTimeRange> startTimeRanges = new List<EventStartTimeRange>();

            [Header("Wave Pattern")]
            [Tooltip("산성 파도가 총 몇 번 지나갈지 정합니다. 요청 기획 기준 기본값은 4회입니다.")]
            public int waveCount = 4;

            [Tooltip("각 파도 사이의 간격입니다.")]
            public float intervalBetweenWaves = 1.2f;

            [Tooltip("파도가 지나가기 전 경고가 표시되는 시간입니다.")]
            public float warningDuration = 1.0f;

            [Tooltip("파도가 화면을 가로지르는 데 걸리는 시간입니다.")]
            public float travelDuration = 1.4f;

            [Tooltip("카메라 화면 기준 파도 높이입니다. 값이 클수록 세로로 두꺼운 파도가 됩니다.")]
            public float waveHeight = 2.0f;

            [Tooltip("화면 밖에서 파도가 시작/종료될 여유 거리입니다.")]
            public float screenPadding = 2.0f;

            [Header("Damage")]
            [Tooltip("파도에 닿았을 때 플레이어와 몬스터가 받는 피해입니다.")]
            public float damage = 8f;

            [Tooltip("같은 파도 안에서 같은 대상에게 다시 피해를 줄 수 있는 최소 간격입니다.")]
            public float damageCooldownPerTarget = 0.6f;

            [Tooltip("파도에 맞았을 때 밀려나는 힘입니다.")]
            public float knockbackPower = 3.5f;

            [Header("Visual")]
            [Tooltip("파도 색상입니다. 별도 프리팹 없이 생성할 때 SpriteRenderer 색상으로 사용됩니다.")]
            public Color waveColor = new Color(0.55f, 1f, 0.1f, 0.55f);

            [Tooltip("경고 영역 색상입니다.")]
            public Color warningColor = new Color(1f, 0.2f, 0.1f, 0.35f);

            [Header("Runtime")]
            [HideInInspector] public bool started;
            [HideInInspector] public bool finished;
            [HideInInspector] public float resolvedStartTime;
        }

        [System.Serializable]
        public class PeristalsisDriftEvent
        {
            [Header("Event Info")]
            [Tooltip("UI에 표시될 이벤트 이름입니다.")]
            public string eventName = "연동운동 기류";

            [Tooltip("체크되어 있으면 이벤트가 발동됩니다.")]
            public bool enabled = true;

            [Header("Timing")]
            [Tooltip("랜덤 시간을 쓰지 않을 때 사용하는 고정 시작 시간입니다. 단위는 초입니다.")]
            public float startTime = 150f;

            [Tooltip("이벤트 전체 지속 시간입니다.")]
            public float duration = 14f;

            [Tooltip("체크하면 아래 시간 범위 중 하나를 골라 랜덤 시작 시간을 정합니다.")]
            public bool useRandomStartTime = true;

            [Tooltip("랜덤 시작 시간 범위입니다. 단위는 분입니다.")]
            public List<EventStartTimeRange> startTimeRanges = new List<EventStartTimeRange>();

            [Header("Drift")]
            [Tooltip("왼쪽/오른쪽 쏠림 방향이 바뀌는 주기입니다.")]
            public float directionSwitchInterval = 4f;

            [Tooltip("플레이어에게 적용할 쏠림 힘입니다.")]
            public float playerDriftForce = 7f;

            [Tooltip("몬스터에게 적용할 쏠림 힘입니다.")]
            public float monsterDriftForce = 5f;

            [Tooltip("쏠리는 방향으로 계속 밀리는 최대 속도 제한입니다.")]
            public float maxAddedVelocity = 4f;

            [Header("Camera Tilt")]
            [Tooltip("체크하면 이벤트 중 카메라를 살짝 기울여 위장이 기울어진 듯한 연출을 합니다.")]
            public bool enableCameraTilt = true;

            [Tooltip("카메라가 좌우로 기울어지는 최대 각도입니다.")]
            public float cameraTiltAngle = 7f;

            [Tooltip("카메라 기울기 보간 속도입니다.")]
            public float cameraTiltLerpSpeed = 5f;

            [Header("Runtime")]
            [HideInInspector] public bool started;
            [HideInInspector] public bool finished;
            [HideInInspector] public float resolvedStartTime;
            [HideInInspector] public float elapsed;

            // 카메라 Tilt 효과음이 이 이벤트에서 이미 재생됐는지 기록합니다.
            [HideInInspector] public bool tiltSfxPlayed;
        }

        [System.Serializable]
        public class CoffeeTransfusionEvent
        {
            [Header("Event Info")]
            [Tooltip("UI에 표시될 이벤트 이름입니다.")]
            public string eventName = "커피수혈 타임";

            [Tooltip("체크되어 있으면 이벤트가 발동됩니다.")]
            public bool enabled = true;

            [Header("Timing")]
            [Tooltip("랜덤 시간을 쓰지 않을 때 사용하는 고정 시작 시간입니다. 단위는 초입니다.")]
            public float startTime = 210f;

            [Tooltip("이벤트 전체 지속 시간입니다.")]
            public float duration = 18f;

            [Tooltip("체크하면 아래 시간 범위 중 하나를 골라 랜덤 시작 시간을 정합니다.")]
            public bool useRandomStartTime = true;

            [Tooltip("랜덤 시작 시간 범위입니다. 단위는 분입니다.")]
            public List<EventStartTimeRange> startTimeRanges = new List<EventStartTimeRange>();

            [Header("Coffee Wave")]
            [Tooltip("커피 파도가 화면을 지나가기 전 경고 시간입니다.")]
            public float warningDuration = 0.6f;

            [Tooltip("커피 파도가 화면을 가로지르는 데 걸리는 시간입니다.")]
            public float travelDuration = 1.6f;

            [Tooltip("커피 파도 높이입니다.")]
            public float waveHeight = 7f;

            [Tooltip("화면 밖에서 파도가 시작/종료될 여유 거리입니다.")]
            public float screenPadding = 2f;

            [Header("Monster Buff")]
            [Tooltip("커피수혈 중 몬스터에게 추가되는 이동 보정 힘입니다.")]
            public float monsterExtraMoveForce = 2.5f;

            [Tooltip("커피수혈 중 몬스터 Rigidbody 속도 최대 보정치입니다.")]
            public float monsterMaxAddedVelocity = 2.0f;

            [Tooltip("몬스터에게 덧씌울 커피 색상입니다. 알파값으로 덮인 정도를 조절합니다.")]
            public Color coffeeOverlayColor = new Color(0.45f, 0.22f, 0.08f, 0.55f);

            [Tooltip("커피 버프를 몇 초마다 새로 스캔해서 새로 생긴 몬스터에게도 적용할지 정합니다.")]
            public float monsterScanInterval = 0.5f;

            [Header("Visual")]
            [Tooltip("커피 파도 색상입니다.")]
            public Color coffeeWaveColor = new Color(0.35f, 0.16f, 0.05f, 0.65f);

            [Tooltip("커피 파도 경고 색상입니다.")]
            public Color warningColor = new Color(0.55f, 0.28f, 0.08f, 0.35f);

            [Header("Runtime")]
            [HideInInspector] public bool started;
            [HideInInspector] public bool finished;
            [HideInInspector] public float resolvedStartTime;
            [HideInInspector] public float monsterScanTimer;
        }

        [Header("References")]
        [Tooltip("현재 스테이지 시간을 가져오기 위한 LevelManager입니다. 비워두면 씬에서 자동으로 찾습니다.")]
        [SerializeField] private LevelManager levelManager;

        [Tooltip("이벤트 시작 알림 UI입니다. 비워두면 씬에서 자동으로 찾습니다.")]
        [SerializeField] private StageEventToastUI eventToastUI;

        [Tooltip("이벤트 시각 오브젝트를 묶어둘 부모입니다. 비워두면 이 오브젝트 아래에 생성됩니다.")]
        [SerializeField] private Transform runtimeVisualRoot;

        [Header("UI Message")]
        [Tooltip("{0} 위치에 이벤트 이름이 들어갑니다.")]
        [SerializeField] private string eventStartMessageFormat = "{0} 이벤트가 시작됐습니다!";

        [Header("1. Acid Reflux Wave Events / 산성 역류 파도")]
        [SerializeField] private List<AcidRefluxWaveEvent> acidRefluxWaveEvents = new List<AcidRefluxWaveEvent>();

        [Header("2. Peristalsis Drift Events / 연동운동 기류")]
        [SerializeField] private List<PeristalsisDriftEvent> peristalsisDriftEvents = new List<PeristalsisDriftEvent>();

        [Header("3. Coffee Transfusion Events / 커피수혈 타임")]
        [SerializeField] private List<CoffeeTransfusionEvent> coffeeTransfusionEvents = new List<CoffeeTransfusionEvent>();

        [Header("Preparation Debug")]
        [Tooltip("플레이 시작 시 고급 필드 이벤트들의 등록 개수와 실제 시작 시간을 로그로 출력합니다.")]
        [SerializeField] private bool logPreparedEvents = true;
        [Header("Visual Sorting")]
        [Tooltip("체크하면 산성 파도/커피 파도 시각 오브젝트의 Sorting Layer와 Order in Layer를 코드에서 강제로 적용합니다.")]
        [SerializeField] private bool forceEventVisualSorting = true;

        [Tooltip("산성 파도/커피 파도에 적용할 Sorting Layer 이름입니다. 먼저 Default로 테스트하고, 안 보이면 Monster Full로 바꿔보세요.")]
        [SerializeField] private string eventVisualSortingLayerName = "Default";

        [Tooltip("산성 파도/커피 파도의 Order in Layer입니다. 값이 클수록 앞에 보입니다.")]
        [SerializeField] private int eventVisualSortingOrder = 5000;

        [Tooltip("파도 시각 오브젝트가 너무 투명하게 설정됐을 때 테스트용으로 최소 알파값을 보정합니다.")]
        [SerializeField] private float eventVisualMinimumAlpha = 0.45f;

        [Tooltip("파도 시각 오브젝트 생성 정보를 콘솔에 출력합니다.")]
        [SerializeField] private bool logWaveVisualCreation = true;
        [Header("Debug")]
        [Tooltip("이벤트 시작/종료 로그를 출력합니다.")]
        [SerializeField] private bool logEventState = true;

        [Tooltip("연동운동 기류 힘 적용 로그를 자세히 출력합니다. 테스트 후 꺼두는 것을 추천합니다.")]
        [SerializeField] private bool logDriftDetail = false;

        private Camera mainCamera;
        private Quaternion originalCameraRotation;
        private bool hasOriginalCameraRotation;
        private Coroutine activeAcidRefluxRoutine;
        private Coroutine activeCoffeeWaveRoutine;

        private void Start()
        {
            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }

            if (eventToastUI == null)
            {
                eventToastUI = FindObjectOfType<StageEventToastUI>();
            }

            if (runtimeVisualRoot == null)
            {
                runtimeVisualRoot = transform;
            }

            mainCamera = Camera.main;

            if (mainCamera != null)
            {
                originalCameraRotation = mainCamera.transform.rotation;
                hasOriginalCameraRotation = true;
            }

            PrepareAllEvents();
        }

        private void Update()
        {
            if (levelManager == null)
            {
                return;
            }

            float currentTime = levelManager.CurrentLevelTime;

            for (int i = 0; i < acidRefluxWaveEvents.Count; i++)
            {
                UpdateAcidRefluxWaveEvent(acidRefluxWaveEvents[i], currentTime);
            }

            for (int i = 0; i < peristalsisDriftEvents.Count; i++)
            {
                UpdatePeristalsisDriftEvent(peristalsisDriftEvents[i], currentTime);
            }

            for (int i = 0; i < coffeeTransfusionEvents.Count; i++)
            {
                UpdateCoffeeTransfusionEvent(coffeeTransfusionEvents[i], currentTime);
            }

            RestoreCameraTiltIfNoActiveDrift();
        }

        private void FixedUpdate()
        {
            for (int i = 0; i < peristalsisDriftEvents.Count; i++)
            {
                PeristalsisDriftEvent driftEvent = peristalsisDriftEvents[i];

                if (driftEvent == null || !driftEvent.enabled || !driftEvent.started || driftEvent.finished)
                {
                    continue;
                }

                ApplyPeristalsisDrift(driftEvent);
            }
        }

        private void PrepareAllEvents()
        {
            for (int i = 0; i < acidRefluxWaveEvents.Count; i++)
            {
                AcidRefluxWaveEvent waveEvent = acidRefluxWaveEvents[i];

                if (waveEvent == null)
                {
                    continue;
                }

                waveEvent.started = false;
                waveEvent.finished = false;
                waveEvent.resolvedStartTime = ResolveStartTime(
                    waveEvent.eventName,
                    waveEvent.startTime,
                    waveEvent.useRandomStartTime,
                    waveEvent.startTimeRanges);
            }

            for (int i = 0; i < peristalsisDriftEvents.Count; i++)
            {
                PeristalsisDriftEvent driftEvent = peristalsisDriftEvents[i];

                if (driftEvent == null)
                {
                    continue;
                }

                driftEvent.started = false;
                driftEvent.finished = false;
                driftEvent.elapsed = 0f;
                driftEvent.tiltSfxPlayed = false;
                driftEvent.resolvedStartTime = ResolveStartTime(
                    driftEvent.eventName,
                    driftEvent.startTime,
                    driftEvent.useRandomStartTime,
                    driftEvent.startTimeRanges);
            }

            for (int i = 0; i < coffeeTransfusionEvents.Count; i++)
            {
                CoffeeTransfusionEvent coffeeEvent = coffeeTransfusionEvents[i];

                if (coffeeEvent == null)
                {
                    continue;
                }

                coffeeEvent.started = false;
                coffeeEvent.finished = false;
                coffeeEvent.monsterScanTimer = 0f;
                coffeeEvent.resolvedStartTime = ResolveStartTime(
                    coffeeEvent.eventName,
                    coffeeEvent.startTime,
                    coffeeEvent.useRandomStartTime,
                    coffeeEvent.startTimeRanges);
            }
            if (logPreparedEvents)
            {
                Debug.Log(
                    $"[AdvancedStageEvent] 준비 완료 | " +
                    $"산성역류={acidRefluxWaveEvents.Count}개, " +
                    $"연동운동={peristalsisDriftEvents.Count}개, " +
                    $"커피수혈={coffeeTransfusionEvents.Count}개");

                for (int i = 0; i < acidRefluxWaveEvents.Count; i++)
                {
                    AcidRefluxWaveEvent e = acidRefluxWaveEvents[i];
                    if (e != null)
                    {
                        Debug.Log($"[AdvancedStageEvent] 산성 역류 #{i} | Enabled={e.enabled} | Start={e.resolvedStartTime:F1}s | Random={e.useRandomStartTime}");
                    }
                }

                for (int i = 0; i < peristalsisDriftEvents.Count; i++)
                {
                    PeristalsisDriftEvent e = peristalsisDriftEvents[i];
                    if (e != null)
                    {
                        Debug.Log($"[AdvancedStageEvent] 연동운동 #{i} | Enabled={e.enabled} | Start={e.resolvedStartTime:F1}s | Random={e.useRandomStartTime} | Duration={e.duration:F1}s");
                    }
                }

                for (int i = 0; i < coffeeTransfusionEvents.Count; i++)
                {
                    CoffeeTransfusionEvent e = coffeeTransfusionEvents[i];
                    if (e != null)
                    {
                        Debug.Log($"[AdvancedStageEvent] 커피수혈 #{i} | Enabled={e.enabled} | Start={e.resolvedStartTime:F1}s | Random={e.useRandomStartTime} | Duration={e.duration:F1}s");
                    }
                }
            }
        }

        private float ResolveStartTime(
            string eventName,
            float fallbackStartTime,
            bool useRandomStartTime,
            List<EventStartTimeRange> ranges)
        {
            if (!useRandomStartTime)
            {
                return Mathf.Max(0f, fallbackStartTime);
            }

            List<EventStartTimeRange> validRanges = new List<EventStartTimeRange>();

            if (ranges != null)
            {
                for (int i = 0; i < ranges.Count; i++)
                {
                    if (ranges[i] != null && ranges[i].IsValid())
                    {
                        validRanges.Add(ranges[i]);
                    }
                }
            }

            if (validRanges.Count == 0)
            {
                Debug.LogWarning(
                    $"[AdvancedStageEvent] {eventName}: Use Random Start Time이 켜져 있지만 유효한 Start Time Range가 없습니다. " +
                    $"fallback Start Time {fallbackStartTime:F1}초를 사용합니다.");

                return Mathf.Max(0f, fallbackStartTime);
            }

            EventStartTimeRange selectedRange = validRanges[Random.Range(0, validRanges.Count)];
            float resolvedTime = selectedRange.GetRandomStartTimeSeconds();

            if (logEventState)
            {
                Debug.Log($"[AdvancedStageEvent] {eventName} random start resolved: {resolvedTime:F1}s ({resolvedTime / 60f:F2}min)");
            }

            return Mathf.Max(0f, resolvedTime);
        }

        private void UpdateAcidRefluxWaveEvent(AcidRefluxWaveEvent waveEvent, float currentTime)
        {
            if (waveEvent == null || !waveEvent.enabled || waveEvent.finished)
            {
                return;
            }

            if (currentTime < waveEvent.resolvedStartTime)
            {
                return;
            }

            if (!waveEvent.started)
            {
                waveEvent.started = true;
                ShowEventStartedUI(waveEvent.eventName);

                if (logEventState)
                {
                    Debug.Log($"[AdvancedStageEvent] Start: {waveEvent.eventName} | time={currentTime:F1}s");
                }

                activeAcidRefluxRoutine = StartCoroutine(AcidRefluxWaveRoutine(waveEvent));
            }
        }

        private IEnumerator AcidRefluxWaveRoutine(AcidRefluxWaveEvent waveEvent)
        {
            int safeWaveCount = Mathf.Max(1, waveEvent.waveCount);

            for (int i = 0; i < safeWaveCount; i++)
            {
                bool moveLeftToRight = i % 2 == 0;

                yield return SpawnMovingWave(
                    eventName: waveEvent.eventName,
                    moveLeftToRight: moveLeftToRight,
                    warningDuration: waveEvent.warningDuration,
                    travelDuration: waveEvent.travelDuration,
                    waveHeight: waveEvent.waveHeight,
                    screenPadding: waveEvent.screenPadding,
                    damage: waveEvent.damage,
                    damageCooldownPerTarget: waveEvent.damageCooldownPerTarget,
                    knockbackPower: waveEvent.knockbackPower,
                    affectPlayer: true,
                    affectMonsters: true,
                    waveColor: waveEvent.waveColor,
                    warningColor: waveEvent.warningColor,
                    visualOnly: false,
                    travelStartSfxId: GameAudioManager.GameSfxId.AcidRefluxWavePass);

                if (i < safeWaveCount - 1)
                {
                    yield return new WaitForSeconds(Mathf.Max(0f, waveEvent.intervalBetweenWaves));
                }
            }

            waveEvent.finished = true;
            activeAcidRefluxRoutine = null;

            if (logEventState)
            {
                Debug.Log($"[AdvancedStageEvent] End: {waveEvent.eventName}");
            }
        }

        private void UpdatePeristalsisDriftEvent(PeristalsisDriftEvent driftEvent, float currentTime)
        {
            if (driftEvent == null)
            {
                return;
            }

            if (!driftEvent.enabled)
            {
                return;
            }

            if (driftEvent.finished)
            {
                return;
            }

            if (currentTime < driftEvent.resolvedStartTime)
            {
                return;
            }

            if (!driftEvent.started)
            {
                driftEvent.started = true;
                driftEvent.elapsed = 0f;

                ShowEventStartedUI(driftEvent.eventName);

                Debug.Log(
                    $"[AdvancedStageEvent] Start: {driftEvent.eventName} | " +
                    $"time={currentTime:F1}s | duration={driftEvent.duration:F1}s | " +
                    $"switch={driftEvent.directionSwitchInterval:F1}s");
            }

            driftEvent.elapsed += Time.deltaTime;

            if (driftEvent.elapsed >= driftEvent.duration)
            {
                driftEvent.finished = true;

                Debug.Log($"[AdvancedStageEvent] End: {driftEvent.eventName}");

                return;
            }

            ApplyCameraTilt(driftEvent);
        }

        private void ApplyPeristalsisDrift(PeristalsisDriftEvent driftEvent)
        {
            Vector2 driftDirection = GetCurrentDriftDirection(driftEvent);

            Character player = levelManager != null ? levelManager.PlayerCharacter : null;

            if (player != null)
            {
                Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();

                ApplyDriftToRigidbody(
                    playerRb,
                    driftDirection,
                    driftEvent.playerDriftForce,
                    driftEvent.maxAddedVelocity);
            }
            else
            {
                Debug.LogWarning("[AdvancedStageEvent] 연동운동 기류: PlayerCharacter를 찾지 못했습니다.");
            }

            Monster[] monsters = FindObjectsOfType<Monster>();

            for (int i = 0; i < monsters.Length; i++)
            {
                if (monsters[i] == null)
                {
                    continue;
                }

                Rigidbody2D monsterRb = monsters[i].GetComponent<Rigidbody2D>();

                ApplyDriftToRigidbody(
                    monsterRb,
                    driftDirection,
                    driftEvent.monsterDriftForce,
                    driftEvent.maxAddedVelocity);
            }

            if (logDriftDetail)
            {
                Debug.Log(
                    $"[AdvancedStageEvent] 연동운동 기류 적용 | " +
                    $"dir={driftDirection} | monsters={monsters.Length}");
            }
        }

        private Vector2 GetCurrentDriftDirection(PeristalsisDriftEvent driftEvent)
        {
            float safeSwitchInterval = Mathf.Max(0.1f, driftEvent.directionSwitchInterval);
            int phase = Mathf.FloorToInt(driftEvent.elapsed / safeSwitchInterval);

            // 짝수 페이즈: 왼쪽, 홀수 페이즈: 오른쪽
            return phase % 2 == 0 ? Vector2.left : Vector2.right;
        }

        private void ApplyDriftToRigidbody(
            Rigidbody2D targetRb,
            Vector2 direction,
            float force,
            float maxAddedVelocity)
        {
            if (targetRb == null)
            {
                return;
            }

            Vector2 addedVelocity = direction.normalized * force * Time.fixedDeltaTime;
            targetRb.velocity += addedVelocity;

            float projectedSpeed = Vector2.Dot(targetRb.velocity, direction.normalized);
            float safeMax = Mathf.Max(0.1f, maxAddedVelocity);

            if (projectedSpeed > safeMax)
            {
                Vector2 excess = direction.normalized * (projectedSpeed - safeMax);
                targetRb.velocity -= excess;
            }
        }

        private void ApplyCameraTilt(PeristalsisDriftEvent driftEvent)
        {
            if (!driftEvent.enableCameraTilt)
            {
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (mainCamera == null)
            {
                return;
            }

            if (!hasOriginalCameraRotation)
            {
                originalCameraRotation = mainCamera.transform.rotation;
                hasOriginalCameraRotation = true;
            }

            Vector2 direction = GetCurrentDriftDirection(driftEvent);
            float sign = direction.x < 0f ? -1f : 1f;

            // 기존 카메라 회전을 덮어쓰지 않고, 원래 회전값에 기울기만 더한다.
            Quaternion targetRotation =
                originalCameraRotation * Quaternion.Euler(0f, 0f, sign * driftEvent.cameraTiltAngle);

            mainCamera.transform.rotation = Quaternion.Lerp(
                mainCamera.transform.rotation,
                targetRotation,
                Time.deltaTime * Mathf.Max(0.1f, driftEvent.cameraTiltLerpSpeed));

            // 카메라가 존재하고 실제 Tilt 처리가 처음 실행된 순간에만
            // 연동운동 Tilt 효과음을 1회 재생합니다.
            if (!driftEvent.tiltSfxPlayed)
            {
                driftEvent.tiltSfxPlayed = true;

                GameAudioManager.PlaySfx(
                    GameAudioManager.GameSfxId.PeristalsisTilt
                );
            }
        }

        private void RestoreCameraTiltIfNoActiveDrift()
        {
            bool hasActiveDrift = false;

            for (int i = 0; i < peristalsisDriftEvents.Count; i++)
            {
                PeristalsisDriftEvent driftEvent = peristalsisDriftEvents[i];

                if (driftEvent != null && driftEvent.enabled && driftEvent.started && !driftEvent.finished)
                {
                    hasActiveDrift = true;
                    break;
                }
            }

            if (hasActiveDrift)
            {
                return;
            }

            if (mainCamera == null || !hasOriginalCameraRotation)
            {
                return;
            }

            mainCamera.transform.rotation = Quaternion.Lerp(
                mainCamera.transform.rotation,
                originalCameraRotation,
                Time.deltaTime * 5f);
        }

        private void UpdateCoffeeTransfusionEvent(CoffeeTransfusionEvent coffeeEvent, float currentTime)
        {
            if (coffeeEvent == null || !coffeeEvent.enabled || coffeeEvent.finished)
            {
                return;
            }

            if (currentTime < coffeeEvent.resolvedStartTime)
            {
                return;
            }

            if (!coffeeEvent.started)
            {
                coffeeEvent.started = true;
                coffeeEvent.monsterScanTimer = 0f;
                ShowEventStartedUI(coffeeEvent.eventName);

                if (logEventState)
                {
                    Debug.Log($"[AdvancedStageEvent] Start: {coffeeEvent.eventName} | time={currentTime:F1}s");
                }

                activeCoffeeWaveRoutine = StartCoroutine(CoffeeWaveRoutine(coffeeEvent));
            }

            if (currentTime >= coffeeEvent.resolvedStartTime + coffeeEvent.duration)
            {
                coffeeEvent.finished = true;
                RemoveAllCoffeeBuffs();

                if (logEventState)
                {
                    Debug.Log($"[AdvancedStageEvent] End: {coffeeEvent.eventName}");
                }

                return;
            }

            coffeeEvent.monsterScanTimer += Time.deltaTime;

            if (coffeeEvent.monsterScanTimer >= coffeeEvent.monsterScanInterval)
            {
                coffeeEvent.monsterScanTimer = 0f;

                float remainingDuration =
                    Mathf.Max(0.1f, coffeeEvent.resolvedStartTime + coffeeEvent.duration - currentTime);

                ApplyCoffeeBuffToActiveMonsters(coffeeEvent, remainingDuration);
            }
        }

        private IEnumerator CoffeeWaveRoutine(CoffeeTransfusionEvent coffeeEvent)
        {
            yield return SpawnMovingWave(
                eventName: coffeeEvent.eventName,
                moveLeftToRight: true,
                warningDuration: coffeeEvent.warningDuration,
                travelDuration: coffeeEvent.travelDuration,
                waveHeight: coffeeEvent.waveHeight,
                screenPadding: coffeeEvent.screenPadding,
                damage: 0f,
                damageCooldownPerTarget: 999f,
                knockbackPower: 0f,
                affectPlayer: false,
                affectMonsters: false,
                waveColor: coffeeEvent.coffeeWaveColor,
                warningColor: coffeeEvent.warningColor,
                visualOnly: true,
                travelStartSfxId: GameAudioManager.GameSfxId.CoffeeTransfusionPour);

            activeCoffeeWaveRoutine = null;
        }

        private void ApplyCoffeeBuffToActiveMonsters(
    CoffeeTransfusionEvent coffeeEvent,
    float remainingDuration)
        {
            Monster[] monsters = FindObjectsOfType<Monster>();

            for (int i = 0; i < monsters.Length; i++)
            {
                Monster monster = monsters[i];

                if (monster == null)
                {
                    continue;
                }

                CoffeeMonsterBuffRuntime buff = monster.GetComponent<CoffeeMonsterBuffRuntime>();

                if (buff == null)
                {
                    buff = monster.gameObject.AddComponent<CoffeeMonsterBuffRuntime>();
                }

                Character player = levelManager != null ? levelManager.PlayerCharacter : null;

                buff.ApplyOrRefresh(
                    player,
                    remainingDuration,
                    coffeeEvent.monsterExtraMoveForce,
                    coffeeEvent.monsterMaxAddedVelocity,
                    coffeeEvent.coffeeOverlayColor);
            }
        }

        private void RemoveAllCoffeeBuffs()
        {
            CoffeeMonsterBuffRuntime[] buffs = FindObjectsOfType<CoffeeMonsterBuffRuntime>();

            for (int i = buffs.Length - 1; i >= 0; i--)
            {
                if (buffs[i] != null)
                {
                    buffs[i].RemoveBuffAndDestroy();
                }
            }
        }

        private IEnumerator SpawnMovingWave(
            string eventName,
            bool moveLeftToRight,
            float warningDuration,
            float travelDuration,
            float waveHeight,
            float screenPadding,
            float damage,
            float damageCooldownPerTarget,
            float knockbackPower,
            bool affectPlayer,
            bool affectMonsters,
            Color waveColor,
            Color warningColor,
            bool visualOnly,
            GameAudioManager.GameSfxId? travelStartSfxId = null)
        {
            // 실제 피해 파도일 때만 위험 경고음.
            // 커피수혈의 VisualOnly 파도에는 재생하지 않습니다.
            if (!visualOnly)
            {
                GameAudioManager.PlaySfx(
                    GameAudioManager.GameSfxId.DangerWave
                );
            }
            Rect cameraRect = GetCameraWorldRect(screenPadding);

            float centerY = (cameraRect.yMin + cameraRect.yMax) * 0.5f;
            float width = 1.3f;
            float safeHeight = Mathf.Max(0.5f, waveHeight);

            Vector3 startPosition;
            Vector3 endPosition;

            if (moveLeftToRight)
            {
                startPosition = new Vector3(cameraRect.xMin, centerY, 0f);
                endPosition = new Vector3(cameraRect.xMax, centerY, 0f);
            }
            else
            {
                startPosition = new Vector3(cameraRect.xMax, centerY, 0f);
                endPosition = new Vector3(cameraRect.xMin, centerY, 0f);
            }

            GameObject warningObject = CreateWaveVisualObject(
                eventName + "_Warning",
                startPosition,
                new Vector2(width, safeHeight),
                warningColor);

            if (warningObject != null)
            {
                StageEventMovingWaveZone warningZone = warningObject.GetComponent<StageEventMovingWaveZone>();
                warningZone.InitVisualOnly(
                    startPosition,
                    endPosition,
                    warningDuration,
                    warningColor);
            }

            yield return new WaitForSeconds(Mathf.Max(0f, warningDuration));

            if (warningObject != null)
            {
                Destroy(warningObject);
            }

            GameObject waveObject = CreateWaveVisualObject(
                eventName + "_Wave",
                startPosition,
                new Vector2(width, safeHeight),
                waveColor);

            if (waveObject != null)
            {
                StageEventMovingWaveZone waveZone = waveObject.GetComponent<StageEventMovingWaveZone>();

                if (visualOnly)
                {
                    waveZone.InitVisualOnly(
                        startPosition,
                        endPosition,
                        travelDuration,
                        waveColor);
                }
                else
                {
                    Vector2 knockbackDirection = moveLeftToRight ? Vector2.right : Vector2.left;

                    waveZone.InitDamageWave(
                        startPosition,
                        endPosition,
                        travelDuration,
                        waveColor,
                        damage,
                        damageCooldownPerTarget,
                        knockbackDirection * knockbackPower,
                        affectPlayer,
                        affectMonsters);
                }

                // 경고가 끝난 뒤 실제 파도 오브젝트가 생성되고
                // 이동을 시작하는 순간에만 해당 전용 효과음을 1회 재생합니다.
                if (travelStartSfxId.HasValue)
                {
                    GameAudioManager.PlaySfx(
                        travelStartSfxId.Value
                    );
                }
            }

            yield return new WaitForSeconds(Mathf.Max(0.05f, travelDuration));

            if (waveObject != null)
            {
                Destroy(waveObject);
            }
        }

        private GameObject CreateWaveVisualObject(
    string objectName,
    Vector3 position,
    Vector2 size,
    Color color)
        {
            GameObject obj = new GameObject(objectName);

            if (runtimeVisualRoot != null)
            {
                obj.transform.SetParent(runtimeVisualRoot, true);
            }

            obj.transform.position = position;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer spriteRenderer = obj.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = CreateWhiteSprite();

            Color visibleColor = color;

            // 인스펙터에서 알파가 낮게 들어가 있어도 테스트 단계에서는 확실히 보이게 최소 알파를 보정한다.
            if (visibleColor.a < eventVisualMinimumAlpha)
            {
                visibleColor.a = eventVisualMinimumAlpha;
            }

            spriteRenderer.color = visibleColor;

            if (forceEventVisualSorting)
            {
                spriteRenderer.sortingLayerName = eventVisualSortingLayerName;
                spriteRenderer.sortingOrder = eventVisualSortingOrder;
            }
            else
            {
                spriteRenderer.sortingOrder = eventVisualSortingOrder;
            }

            // URP/2D 환경에서 기본 SpriteRenderer가 확실히 보이도록 명시적으로 Sprites/Default 재질을 넣는다.
            Shader spriteShader = Shader.Find("Sprites/Default");

            if (spriteShader != null)
            {
                spriteRenderer.material = new Material(spriteShader);
            }

            BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = Vector2.one;

            obj.AddComponent<StageEventMovingWaveZone>();

            if (logWaveVisualCreation)
            {
                Debug.Log(
                    $"[AdvancedStageEvent Visual] Create {objectName} | " +
                    $"pos={position} | scale={obj.transform.localScale} | " +
                    $"color={spriteRenderer.color} | " +
                    $"sorting={spriteRenderer.sortingLayerName}/{spriteRenderer.sortingOrder} | " +
                    $"parent={(runtimeVisualRoot != null ? runtimeVisualRoot.name : "None")}");
            }

            return obj;
        }

        private Rect GetCameraWorldRect(float padding)
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (mainCamera == null)
            {
                Character player = levelManager != null ? levelManager.PlayerCharacter : null;
                Vector3 center = player != null ? player.transform.position : Vector3.zero;
                return new Rect(center.x - 10f, center.y - 6f, 20f, 12f);
            }

            // 2D 카메라가 z = -10 같은 위치에 있는 경우를 고려해서,
            // 카메라로부터 월드 평면까지의 거리를 넣는다.
            float distanceFromCamera = Mathf.Abs(mainCamera.transform.position.z);

            Vector3 bottomLeft =
                mainCamera.ViewportToWorldPoint(new Vector3(0f, 0f, distanceFromCamera));

            Vector3 topRight =
                mainCamera.ViewportToWorldPoint(new Vector3(1f, 1f, distanceFromCamera));

            float xMin = Mathf.Min(bottomLeft.x, topRight.x) - padding;
            float xMax = Mathf.Max(bottomLeft.x, topRight.x) + padding;
            float yMin = Mathf.Min(bottomLeft.y, topRight.y) - padding;
            float yMax = Mathf.Max(bottomLeft.y, topRight.y) + padding;

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private Sprite CreateWhiteSprite()
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
        }

        private void ShowEventStartedUI(string eventName)
        {
            string safeEventName = string.IsNullOrEmpty(eventName) ? "스테이지" : eventName;
            string message = string.Format(eventStartMessageFormat, safeEventName);

            // 산성 역류 / 연동운동 / 커피수혈 모두
            // 동일한 필드 이벤트 시작 효과음을 이벤트당 1회 재생합니다.
            GameAudioManager.PlaySfx(
                GameAudioManager.GameSfxId.FieldEventStart
            );

            if (eventToastUI != null)
            {
                eventToastUI.Show(message);
            }

            if (logEventState)
            {
                Debug.Log($"[AdvancedStageEvent UI] {message}");
            }
        }
    }
}