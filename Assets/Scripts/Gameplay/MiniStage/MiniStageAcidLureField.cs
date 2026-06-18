using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 미니 스테이지 전용 위산 유인 필드.
    ///
    /// 기존 필드 이벤트용 위산 장판과 겹치지 않도록 별도 스크립트로 제작합니다.
    ///
    /// 이번 버전 핵심:
    /// - OnTriggerEnter2D에만 의존하지 않고, 장판 영역을 주기적으로 직접 스캔합니다.
    /// - 리스트 순회 중 리스트가 수정되어 발생하던 ArgumentOutOfRangeException을 방지합니다.
    /// - 위산 데미지로 죽은 몬스터뿐 아니라, 장판 위에서 플레이어가 처치한 몬스터도 카운트합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class MiniStageAcidLureField : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("위산 필드의 원형 스프라이트 렌더러입니다. 기존 위산 장판 스프라이트를 재사용하면 됩니다.")]
        [SerializeField] private SpriteRenderer acidSpriteRenderer;

        [Tooltip("몬스터 감지용 Trigger Collider입니다. 비워두면 이 오브젝트의 Collider2D를 자동 사용합니다.")]
        [SerializeField] private Collider2D triggerCollider;

        [Header("Kill Rule")]
        [Tooltip("이 필드가 완료되기 위해 필요한 몬스터 처치 수입니다. Room에서 넘겨준 값이 우선 적용됩니다.")]
        [SerializeField] private int requiredKillCount = 15;

        [Tooltip("위산 필드 안에 들어온 몬스터가 초당 받는 피해량입니다. 예: 5면 초당 5데미지입니다.")]
        [SerializeField] private float damagePerSecond = 5f;

        [Tooltip("위산 피해를 몇 초마다 줄지 정합니다. 1이면 1초마다, 0.25면 0.25초마다 나눠서 피해를 줍니다.")]
        [SerializeField] private float damageTickInterval = 1f;

        [Tooltip("보스 몬스터는 이 위산 필드의 처치 대상에서 제외합니다.")]
        [SerializeField] private bool ignoreBossMonsters = true;

        [Tooltip("대상 몬스터 레이어입니다. Nothing이면 레이어 검사를 하지 않고 Monster 컴포넌트만 확인합니다. 특정 몬스터가 안 잡히면 Nothing으로 두는 것을 추천합니다.")]
        [SerializeField] private LayerMask monsterLayer;

        [Header("Overlap Scan")]
        [Tooltip("OnTriggerEnter가 놓친 몬스터도 잡기 위해 장판 영역을 주기적으로 직접 검사합니다.")]
        [SerializeField] private bool useOverlapScan = true;

        [Tooltip("장판 영역 안의 몬스터를 다시 스캔하는 간격입니다. 0.1이면 0.1초마다 검사합니다.")]
        [SerializeField] private float overlapScanInterval = 0.1f;

        [Tooltip("플레이어가 장판 위에서 몬스터를 처치했을 때, 사망 이벤트가 조금 늦게 들어와도 카운트하기 위한 허용 시간입니다.")]
        [SerializeField] private float playerKillCountGraceSeconds = 2f;

        [Tooltip("몬스터가 장판 밖으로 나가도 사망 이벤트 감지를 위해 잠시 추적을 유지하는 시간입니다.")]
        [SerializeField] private float keepTrackingAfterExitSeconds = 2.5f;

        [Header("Field Color")]
        [Tooltip("이 위산 필드의 기본 색상입니다. 같은 스프라이트를 쓰더라도 필드마다 다른 RGB 색상을 지정할 수 있습니다.")]
        [SerializeField] private Color fieldColor = new Color(0.6f, 1f, 0.25f, 0.65f);

        [Tooltip("처치 진행도에 따라 위산 필드 색상을 변화시킬지 여부입니다.")]
        [SerializeField] private bool updateFieldColorByProgress = true;

        [Tooltip("완료 직전으로 갈수록 가까워지는 색상입니다.")]
        [SerializeField] private Color nearCompleteColor = new Color(1f, 0.9f, 0.2f, 0.45f);

        [Tooltip("필드 완료 시 사용할 색상입니다. Hide Field On Complete가 true면 잠깐 적용된 뒤 렌더러가 꺼질 수 있습니다.")]
        [SerializeField] private Color completedColor = new Color(0.25f, 0.25f, 0.25f, 0.15f);

        [Tooltip("필드가 완료되면 위산 스프라이트와 콜라이더를 비활성화합니다.")]
        [SerializeField] private bool hideFieldOnComplete = true;

        [Header("Render Order")]
        [Tooltip("위산 장판과 진행도 마커의 Sorting Layer/Order를 이 스크립트에서 강제로 적용할지 여부입니다.")]
        [SerializeField] private bool forceRenderOrder = true;

        [Tooltip("위산 장판 SpriteRenderer에 적용할 Sorting Layer 이름입니다. 현재 프로젝트에서는 Default를 추천합니다.")]
        [SerializeField] private string acidSortingLayerName = "Default";

        [Tooltip("위산 장판 SpriteRenderer에 적용할 Order in Layer입니다. 배경보다 위, 몬스터/플레이어보다 아래가 되도록 -700을 추천합니다.")]
        [SerializeField] private int acidSortingOrder = -700;

        [Tooltip("진행도 마커 SpriteRenderer에 적용할 Sorting Layer 이름입니다.")]
        [SerializeField] private string markerSortingLayerName = "Default";

        [Tooltip("진행도 마커 SpriteRenderer에 적용할 Order in Layer입니다. 위산 장판보다 살짝 위, 몬스터/플레이어보다 아래가 되도록 -690을 추천합니다.")]
        [SerializeField] private int markerSortingOrder = -690;

        [Header("Progress Markers")]
        [Tooltip("촛불처럼 하나씩 꺼질 진행도 마커들의 부모 Transform입니다. 비워두면 자동 생성 또는 직접 생성 없이 진행됩니다.")]
        [SerializeField] private Transform progressMarkerRoot;

        [Tooltip("진행도 마커 프리팹입니다. 단순 원형 SpriteRenderer 오브젝트를 넣으면 됩니다.")]
        [SerializeField] private GameObject progressMarkerPrefab;

        [Tooltip("Progress Marker Prefab이 있을 때, 필요한 개수만큼 자동 생성할지 여부입니다.")]
        [SerializeField] private bool autoCreateMarkersFromPrefab = true;

        [Tooltip("자동 생성되는 진행도 마커들이 위산 필드 중심에서 떨어지는 반지름입니다.")]
        [SerializeField] private float markerRingRadius = 1.8f;

        [Tooltip("자동 생성되는 진행도 마커의 로컬 스케일입니다.")]
        [SerializeField] private float markerScale = 0.15f;

        [Tooltip("자동 생성되는 마커의 시작 각도입니다. 90이면 위쪽부터 배치됩니다.")]
        [SerializeField] private float markerStartAngle = 90f;

        [Tooltip("몬스터 처치 시 진행도 마커를 SetActive(false)로 끌지 여부입니다.")]
        [SerializeField] private bool disableMarkerObjectOnKill = true;

        [Header("Debug")]
        [Tooltip("위산 필드 처치/완료 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        [Tooltip("장판 안에서 현재 감지된 몬스터 수를 주기적으로 로그로 출력합니다. 문제 확인용입니다.")]
        [SerializeField] private bool debugInsideMonsterCount = false;

        private readonly HashSet<Monster> trackedMonsters = new HashSet<Monster>();
        private readonly HashSet<Monster> currentInsideMonsters = new HashSet<Monster>();
        private readonly HashSet<Monster> countedMonsters = new HashSet<Monster>();

        private readonly Dictionary<Monster, float> nextDamageTickTimes = new Dictionary<Monster, float>();
        private readonly Dictionary<Monster, float> lastSeenInsideTimes = new Dictionary<Monster, float>();

        private readonly List<Monster> damageSnapshot = new List<Monster>();
        private readonly List<Monster> removeBuffer = new List<Monster>();
        private readonly List<GameObject> progressMarkers = new List<GameObject>();

        private MiniStageAcidLureRoom ownerRoom;
        private int currentKillCount;
        private bool isCompleted;
        private float nextOverlapScanTime;
        private float nextDebugCountLogTime;

        private static FieldInfo monsterCurrentHealthField;

        public int CurrentKillCount => currentKillCount;
        public int RequiredKillCount => requiredKillCount;
        public bool IsCompleted => isCompleted;

        private void Reset()
        {
            triggerCollider = GetComponent<Collider2D>();
            acidSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            monsterLayer = 0;
        }

        private void Awake()
        {
            ResolveReferences();
            CacheMonsterHealthField();
            ApplyRenderOrder();
        }

        private void OnValidate()
        {
            ResolveReferences();
            ApplyRenderOrder();
        }

        private void Update()
        {
            if (isCompleted)
            {
                return;
            }

            if (useOverlapScan && Time.time >= nextOverlapScanTime)
            {
                RefreshCurrentInsideMonsters();
                nextOverlapScanTime = Time.time + Mathf.Max(0.02f, overlapScanInterval);
            }

            TickDamageToCurrentMonstersInside();
            CleanupStaleTrackedMonsters();

            if (debugInsideMonsterCount && Time.time >= nextDebugCountLogTime)
            {
                Debug.Log(
                    $"[MiniStageAcidLureField] 현재 장판 내부 감지 몬스터 수: " +
                    $"{currentInsideMonsters.Count}, tracked={trackedMonsters.Count}, field={name}"
                );

                nextDebugCountLogTime = Time.time + 1f;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isCompleted)
            {
                return;
            }

            Monster monster;

            if (!TryGetValidTargetMonsterFromCollider(other, out monster, true))
            {
                return;
            }

            RegisterMonsterInside(monster);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (isCompleted)
            {
                return;
            }

            Monster monster;

            if (!TryGetValidTargetMonsterFromCollider(other, out monster, true))
            {
                return;
            }

            RegisterMonsterInside(monster);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Monster monster = other.GetComponentInParent<Monster>();

            if (monster == null)
            {
                return;
            }

            currentInsideMonsters.Remove(monster);

            // 여기서 바로 OnKilled 리스너를 제거하지 않습니다.
            // 플레이어가 장판 위에서 처치했을 때 Monster.OnKilled가 약간 늦게 호출될 수 있기 때문입니다.
            if (!lastSeenInsideTimes.ContainsKey(monster))
            {
                lastSeenInsideTimes[monster] = Time.time;
            }
        }

        public void Initialize(MiniStageAcidLureRoom owner, int roomRequiredKillCount)
        {
            ownerRoom = owner;
            requiredKillCount = Mathf.Max(1, roomRequiredKillCount);

            ResolveReferences();
            CacheMonsterHealthField();
            SetupProgressMarkers();
            ApplyRenderOrder();
        }

        public void ResetField()
        {
            gameObject.SetActive(true);

            currentKillCount = 0;
            isCompleted = false;
            nextOverlapScanTime = 0f;
            nextDebugCountLogTime = 0f;

            StopTrackingAllMonsters();

            currentInsideMonsters.Clear();
            countedMonsters.Clear();
            nextDamageTickTimes.Clear();
            lastSeenInsideTimes.Clear();

            ResolveReferences();
            SetupProgressMarkers();

            if (triggerCollider != null)
            {
                triggerCollider.enabled = true;
                triggerCollider.isTrigger = true;
            }

            if (acidSpriteRenderer != null)
            {
                acidSpriteRenderer.enabled = true;
                acidSpriteRenderer.color = fieldColor;
            }

            if (progressMarkerRoot != null)
            {
                progressMarkerRoot.gameObject.SetActive(true);
            }

            for (int i = 0; i < progressMarkers.Count; i++)
            {
                if (progressMarkers[i] == null)
                {
                    continue;
                }

                bool shouldBeActive = i < requiredKillCount;
                progressMarkers[i].SetActive(shouldBeActive);
            }

            RefreshCurrentInsideMonsters();
            ApplyRenderOrder();
            UpdateProgressVisual();

            if (debugLog)
            {
                Debug.Log(
                    $"[MiniStageAcidLureField] 필드 초기화: {name}, " +
                    $"requiredKillCount={requiredKillCount}, dps={damagePerSecond}, " +
                    $"tick={damageTickInterval}, color={fieldColor}"
                );
            }
        }

        public void CleanupField()
        {
            StopTrackingAllMonsters();

            currentInsideMonsters.Clear();
            countedMonsters.Clear();
            nextDamageTickTimes.Clear();
            lastSeenInsideTimes.Clear();
            damageSnapshot.Clear();
            removeBuffer.Clear();
        }

        public void ForceHideField()
        {
            isCompleted = true;

            StopTrackingAllMonsters();

            currentInsideMonsters.Clear();
            countedMonsters.Clear();
            nextDamageTickTimes.Clear();
            lastSeenInsideTimes.Clear();

            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }

            if (acidSpriteRenderer != null)
            {
                acidSpriteRenderer.enabled = false;
            }

            if (progressMarkerRoot != null)
            {
                progressMarkerRoot.gameObject.SetActive(false);
            }
        }

        private void ResolveReferences()
        {
            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<Collider2D>();
            }

            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }

            if (acidSpriteRenderer == null)
            {
                acidSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        private void RefreshCurrentInsideMonsters()
        {
            currentInsideMonsters.Clear();

            if (triggerCollider == null || !triggerCollider.enabled)
            {
                return;
            }

            Collider2D[] overlaps = GetOverlappingColliders();

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider2D hit = overlaps[i];

                if (hit == null)
                {
                    continue;
                }

                Monster monster;

                if (!TryGetValidTargetMonsterFromCollider(hit, out monster, true))
                {
                    continue;
                }

                RegisterMonsterInside(monster);
            }
        }

        private Collider2D[] GetOverlappingColliders()
        {
            CircleCollider2D circleCollider = triggerCollider as CircleCollider2D;

            if (circleCollider != null)
            {
                Vector2 center = transform.TransformPoint(circleCollider.offset);
                float radius = circleCollider.radius * Mathf.Max(
                    Mathf.Abs(transform.lossyScale.x),
                    Mathf.Abs(transform.lossyScale.y)
                );

                return Physics2D.OverlapCircleAll(center, radius);
            }

            BoxCollider2D boxCollider = triggerCollider as BoxCollider2D;

            if (boxCollider != null)
            {
                Vector2 center = transform.TransformPoint(boxCollider.offset);
                Vector2 size = new Vector2(
                    boxCollider.size.x * Mathf.Abs(transform.lossyScale.x),
                    boxCollider.size.y * Mathf.Abs(transform.lossyScale.y)
                );

                return Physics2D.OverlapBoxAll(center, size, transform.eulerAngles.z);
            }

            Bounds bounds = triggerCollider.bounds;

            return Physics2D.OverlapAreaAll(bounds.min, bounds.max);
        }

        private bool TryGetValidTargetMonsterFromCollider(
            Collider2D hit,
            out Monster monster,
            bool requireAlive
        )
        {
            monster = null;

            if (hit == null)
            {
                return false;
            }

            monster = hit.GetComponentInParent<Monster>();

            if (monster == null)
            {
                return false;
            }

            if (!monster.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (requireAlive && monster.HP <= 0f)
            {
                return false;
            }

            if (ignoreBossMonsters && monster is BossMonster)
            {
                return false;
            }

            if (monsterLayer.value != 0)
            {
                bool hitLayerMatched = (monsterLayer.value & (1 << hit.gameObject.layer)) != 0;
                bool monsterRootLayerMatched = (monsterLayer.value & (1 << monster.gameObject.layer)) != 0;

                if (!hitLayerMatched && !monsterRootLayerMatched)
                {
                    return false;
                }
            }

            return true;
        }

        private void RegisterMonsterInside(Monster monster)
        {
            if (monster == null)
            {
                return;
            }

            if (monster.HP <= 0f)
            {
                return;
            }

            currentInsideMonsters.Add(monster);
            lastSeenInsideTimes[monster] = Time.time;

            if (!trackedMonsters.Contains(monster))
            {
                trackedMonsters.Add(monster);
                monster.OnKilled.AddListener(OnTrackedMonsterKilled);
            }

            if (!nextDamageTickTimes.ContainsKey(monster))
            {
                nextDamageTickTimes[monster] = Time.time;
            }
        }

        private void TickDamageToCurrentMonstersInside()
        {
            float safeTickInterval = Mathf.Max(0.05f, damageTickInterval);
            float damageThisTick = Mathf.Max(0f, damagePerSecond) * safeTickInterval;

            if (damageThisTick <= 0f)
            {
                return;
            }

            damageSnapshot.Clear();

            foreach (Monster monster in currentInsideMonsters)
            {
                if (monster != null)
                {
                    damageSnapshot.Add(monster);
                }
            }

            for (int i = 0; i < damageSnapshot.Count; i++)
            {
                Monster monster = damageSnapshot[i];

                if (monster == null)
                {
                    continue;
                }

                if (!monster.gameObject.activeInHierarchy)
                {
                    RemoveMonsterFromCurrentSets(monster);
                    continue;
                }

                if (monster.HP <= 0f)
                {
                    TryCountMonsterForField(monster, false, true);
                    continue;
                }

                float nextTickTime;

                if (!nextDamageTickTimes.TryGetValue(monster, out nextTickTime))
                {
                    nextTickTime = Time.time;
                }

                if (Time.time < nextTickTime)
                {
                    continue;
                }

                nextDamageTickTimes[monster] = Time.time + safeTickInterval;

                if (damageThisTick >= monster.HP)
                {
                    DissolveMonsterByAcid(monster);
                }
                else
                {
                    monster.TakeDamage(damageThisTick, Vector2.zero, false);
                }
            }
        }

        private void DissolveMonsterByAcid(Monster monster)
        {
            if (monster == null)
            {
                return;
            }

            if (isCompleted)
            {
                return;
            }

            if (!monster.gameObject.activeInHierarchy || monster.HP <= 0f)
            {
                TryCountMonsterForField(monster, true, false);
                return;
            }

            // 위산 데미지로 막타를 넣는 경우 먼저 카운트 처리합니다.
            // 이후 Killed(false)가 늦게 호출되어도 countedMonsters로 중복 카운트를 막습니다.
            TryCountMonsterForField(monster, true, false);

            RemoveMonsterFromCurrentSets(monster);
            KillMonsterAsEnvironment(monster);
        }

        private void OnTrackedMonsterKilled(Monster monster)
        {
            if (monster == null)
            {
                return;
            }

            // 플레이어 공격으로 죽었더라도, 최근까지 장판 안에 있었으면 카운트합니다.
            TryCountMonsterForField(monster, false, true);
            UntrackMonster(monster);
        }

        private bool TryCountMonsterForField(
            Monster monster,
            bool killedByAcid,
            bool requireRecentInside
        )
        {
            if (monster == null)
            {
                return false;
            }

            if (isCompleted)
            {
                return false;
            }

            if (countedMonsters.Contains(monster))
            {
                return false;
            }

            if (requireRecentInside && !WasMonsterRecentlyInside(monster))
            {
                return false;
            }

            if (ownerRoom != null && !ownerRoom.TryRegisterAcidKill(this, monster))
            {
                return false;
            }

            countedMonsters.Add(monster);

            currentKillCount = Mathf.Clamp(currentKillCount + 1, 0, requiredKillCount);
            UpdateProgressVisual();

            ownerRoom?.NotifyMonsterDissolved(this, monster);

            if (debugLog)
            {
                string killSource = killedByAcid ? "위산 데미지" : "장판 위 플레이어 처치";

                Debug.Log(
                    $"[MiniStageAcidLureField] 몬스터 카운트 인정. " +
                    $"source={killSource}, field={name}, count={currentKillCount}/{requiredKillCount}, monster={monster.name}"
                );
            }

            if (currentKillCount >= requiredKillCount)
            {
                CompleteField();
            }

            return true;
        }

        private bool WasMonsterRecentlyInside(Monster monster)
        {
            if (monster == null)
            {
                return false;
            }

            if (currentInsideMonsters.Contains(monster))
            {
                return true;
            }

            float lastSeenTime;

            if (!lastSeenInsideTimes.TryGetValue(monster, out lastSeenTime))
            {
                return false;
            }

            float graceSeconds = Mathf.Max(0f, playerKillCountGraceSeconds);
            return Time.time - lastSeenTime <= graceSeconds;
        }

        /// <summary>
        /// 플레이어 처치 보상/킬 카운트로 들어가지 않도록 환경 처치로 몬스터를 제거합니다.
        /// 위산 막타는 Monster.TakeDamage()로 처리하지 않고, currentHealth를 0으로 만든 뒤 Killed(false)를 호출합니다.
        /// </summary>
        private void KillMonsterAsEnvironment(Monster monster)
        {
            if (monster == null)
            {
                return;
            }

            if (monsterCurrentHealthField != null)
            {
                monsterCurrentHealthField.SetValue(monster, 0f);
                monster.StartCoroutine(monster.Killed(false));
                return;
            }

            Debug.LogWarning(
                "[MiniStageAcidLureField] Monster.currentHealth 필드를 찾지 못했습니다. " +
                "임시로 Monster.TakeDamage() 막타 처리를 사용합니다."
            );

            monster.TakeDamage(monster.HP, Vector2.zero, false);
        }

        private void CompleteField()
        {
            if (isCompleted)
            {
                return;
            }

            isCompleted = true;

            currentInsideMonsters.Clear();
            nextDamageTickTimes.Clear();
            lastSeenInsideTimes.Clear();

            StopTrackingAllMonsters();

            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }

            if (acidSpriteRenderer != null)
            {
                acidSpriteRenderer.color = completedColor;

                if (hideFieldOnComplete)
                {
                    acidSpriteRenderer.enabled = false;
                }
            }

            if (progressMarkerRoot != null && hideFieldOnComplete)
            {
                progressMarkerRoot.gameObject.SetActive(false);
            }

            if (debugLog)
            {
                Debug.Log($"[MiniStageAcidLureField] 위산 필드 완료 및 비활성화: {name}");
            }

            ownerRoom?.NotifyFieldCompleted(this);
        }

        private void RemoveMonsterFromCurrentSets(Monster monster)
        {
            if (monster == null)
            {
                return;
            }

            currentInsideMonsters.Remove(monster);
            nextDamageTickTimes.Remove(monster);
        }

        private void UntrackMonster(Monster monster)
        {
            if (monster == null)
            {
                return;
            }

            if (trackedMonsters.Contains(monster))
            {
                monster.OnKilled.RemoveListener(OnTrackedMonsterKilled);
                trackedMonsters.Remove(monster);
            }

            currentInsideMonsters.Remove(monster);
            nextDamageTickTimes.Remove(monster);
            lastSeenInsideTimes.Remove(monster);
        }

        private void StopTrackingAllMonsters()
        {
            foreach (Monster monster in trackedMonsters)
            {
                if (monster != null)
                {
                    monster.OnKilled.RemoveListener(OnTrackedMonsterKilled);
                }
            }

            trackedMonsters.Clear();
        }

        private void CleanupStaleTrackedMonsters()
        {
            removeBuffer.Clear();

            foreach (Monster monster in trackedMonsters)
            {
                if (monster == null)
                {
                    removeBuffer.Add(monster);
                    continue;
                }

                if (!monster.gameObject.activeInHierarchy)
                {
                    removeBuffer.Add(monster);
                    continue;
                }

                if (currentInsideMonsters.Contains(monster))
                {
                    continue;
                }

                float lastSeenTime;

                if (!lastSeenInsideTimes.TryGetValue(monster, out lastSeenTime))
                {
                    lastSeenTime = Time.time;
                    lastSeenInsideTimes[monster] = lastSeenTime;
                }

                float keepSeconds = Mathf.Max(
                    playerKillCountGraceSeconds,
                    keepTrackingAfterExitSeconds
                );

                if (Time.time - lastSeenTime > keepSeconds)
                {
                    removeBuffer.Add(monster);
                }
            }

            for (int i = 0; i < removeBuffer.Count; i++)
            {
                UntrackMonster(removeBuffer[i]);
            }

            removeBuffer.Clear();
        }

        private void SetupProgressMarkers()
        {
            progressMarkers.Clear();

            if (progressMarkerRoot == null && progressMarkerPrefab == null)
            {
                return;
            }

            if (progressMarkerRoot == null)
            {
                GameObject markerRootObject = new GameObject("Progress Markers");
                markerRootObject.transform.SetParent(transform);
                markerRootObject.transform.localPosition = Vector3.zero;
                markerRootObject.transform.localRotation = Quaternion.identity;
                markerRootObject.transform.localScale = Vector3.one;
                progressMarkerRoot = markerRootObject.transform;
            }

            if (autoCreateMarkersFromPrefab && progressMarkerPrefab != null)
            {
                CreateMissingProgressMarkersFromPrefab();
            }

            for (int i = 0; i < progressMarkerRoot.childCount; i++)
            {
                Transform child = progressMarkerRoot.GetChild(i);

                if (child != null)
                {
                    progressMarkers.Add(child.gameObject);
                }
            }

            ApplyMarkerLayout();
            ApplyRenderOrder();
        }

        private void CreateMissingProgressMarkersFromPrefab()
        {
            if (progressMarkerRoot == null || progressMarkerPrefab == null)
            {
                return;
            }

            int safeCount = Mathf.Max(1, requiredKillCount);

            while (progressMarkerRoot.childCount < safeCount)
            {
                int markerIndex = progressMarkerRoot.childCount;

                GameObject marker = Instantiate(progressMarkerPrefab, progressMarkerRoot);
                marker.name = $"Progress Marker {markerIndex + 1:00}";
            }
        }

        private void ApplyMarkerLayout()
        {
            if (progressMarkerRoot == null)
            {
                return;
            }

            int safeCount = Mathf.Max(1, requiredKillCount);

            for (int i = 0; i < progressMarkerRoot.childCount; i++)
            {
                Transform marker = progressMarkerRoot.GetChild(i);

                if (marker == null)
                {
                    continue;
                }

                float angle = markerStartAngle + (360f / safeCount) * i;
                float radian = angle * Mathf.Deg2Rad;

                Vector3 localPosition = new Vector3(
                    Mathf.Cos(radian) * markerRingRadius,
                    Mathf.Sin(radian) * markerRingRadius,
                    0f
                );

                marker.localPosition = localPosition;
                marker.localRotation = Quaternion.identity;
                marker.localScale = Vector3.one * markerScale;

                marker.gameObject.SetActive(i < requiredKillCount);
            }
        }

        private void UpdateProgressVisual()
        {
            float progress = requiredKillCount > 0
                ? Mathf.Clamp01((float)currentKillCount / requiredKillCount)
                : 1f;

            if (acidSpriteRenderer != null)
            {
                if (updateFieldColorByProgress)
                {
                    acidSpriteRenderer.color = Color.Lerp(fieldColor, nearCompleteColor, progress);
                }
                else
                {
                    acidSpriteRenderer.color = fieldColor;
                }
            }

            for (int i = 0; i < progressMarkers.Count; i++)
            {
                GameObject marker = progressMarkers[i];

                if (marker == null)
                {
                    continue;
                }

                bool shouldRemainActive = i >= currentKillCount && i < requiredKillCount;

                if (disableMarkerObjectOnKill)
                {
                    marker.SetActive(shouldRemainActive);
                }
                else
                {
                    SpriteRenderer markerRenderer = marker.GetComponentInChildren<SpriteRenderer>();

                    if (markerRenderer != null)
                    {
                        Color color = markerRenderer.color;
                        color.a = shouldRemainActive ? 1f : 0.15f;
                        markerRenderer.color = color;
                    }
                }
            }

            ApplyRenderOrder();
        }

        private void ApplyRenderOrder()
        {
            if (!forceRenderOrder)
            {
                return;
            }

            if (acidSpriteRenderer != null)
            {
                acidSpriteRenderer.sortingLayerName = acidSortingLayerName;
                acidSpriteRenderer.sortingOrder = acidSortingOrder;
            }

            if (progressMarkerRoot == null)
            {
                return;
            }

            Renderer[] markerRenderers = progressMarkerRoot.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < markerRenderers.Length; i++)
            {
                if (markerRenderers[i] == null)
                {
                    continue;
                }

                markerRenderers[i].sortingLayerName = markerSortingLayerName;
                markerRenderers[i].sortingOrder = markerSortingOrder;
            }
        }

        private static void CacheMonsterHealthField()
        {
            if (monsterCurrentHealthField != null)
            {
                return;
            }

            monsterCurrentHealthField = typeof(Monster).GetField(
                "currentHealth",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        }

        private void OnDrawGizmosSelected()
        {
            Collider2D drawCollider = triggerCollider != null
                ? triggerCollider
                : GetComponent<Collider2D>();

            Gizmos.color = Color.green;

            CircleCollider2D circleCollider = drawCollider as CircleCollider2D;

            if (circleCollider != null)
            {
                Vector3 center = transform.TransformPoint(circleCollider.offset);
                float radius = circleCollider.radius * Mathf.Max(
                    Mathf.Abs(transform.lossyScale.x),
                    Mathf.Abs(transform.lossyScale.y)
                );

                Gizmos.DrawWireSphere(center, radius);
            }
            else if (drawCollider != null)
            {
                Gizmos.DrawWireCube(drawCollider.bounds.center, drawCollider.bounds.size);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, markerRingRadius);
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, markerRingRadius);
        }
    }
}