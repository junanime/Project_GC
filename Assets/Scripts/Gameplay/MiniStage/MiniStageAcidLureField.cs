using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 미니 스테이지 전용 위산 유인 필드.
    ///
    /// 기존 필드 이벤트용 위산 장판과 겹치지 않도록 별도 스크립트로 제작합니다.
    /// 이 필드는 플레이어 피해용이 아니라, 몬스터를 유도해서 녹이는 기믹용 장판입니다.
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

        [Tooltip("대상 몬스터 레이어입니다. 0이면 레이어 검사를 하지 않고 Monster 컴포넌트만 확인합니다.")]
        [SerializeField] private LayerMask monsterLayer;

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

        private readonly List<Monster> monstersInside = new List<Monster>();
        private readonly Dictionary<Monster, float> nextDamageTickTimes = new Dictionary<Monster, float>();
        private readonly List<GameObject> progressMarkers = new List<GameObject>();

        private MiniStageAcidLureRoom ownerRoom;
        private int currentKillCount;
        private bool isCompleted;

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
        }

        private void Update()
        {
            if (isCompleted)
            {
                return;
            }

            TickDamageToMonstersInside();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isCompleted)
            {
                return;
            }

            Monster monster = other.GetComponentInParent<Monster>();

            if (!IsValidTargetMonster(monster, other.gameObject.layer))
            {
                return;
            }

            if (!monstersInside.Contains(monster))
            {
                monstersInside.Add(monster);
            }

            if (!nextDamageTickTimes.ContainsKey(monster))
            {
                nextDamageTickTimes[monster] = Time.time;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Monster monster = other.GetComponentInParent<Monster>();

            if (monster == null)
            {
                return;
            }

            monstersInside.Remove(monster);
            nextDamageTickTimes.Remove(monster);
        }

        public void Initialize(MiniStageAcidLureRoom owner, int roomRequiredKillCount)
        {
            ownerRoom = owner;
            requiredKillCount = Mathf.Max(1, roomRequiredKillCount);

            ResolveReferences();
            CacheMonsterHealthField();
            SetupProgressMarkers();
        }

        public void ResetField()
        {
            gameObject.SetActive(true);

            currentKillCount = 0;
            isCompleted = false;

            monstersInside.Clear();
            nextDamageTickTimes.Clear();

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

            UpdateProgressVisual();

            if (debugLog)
            {
                Debug.Log(
                    $"[MiniStageAcidLureField] 필드 초기화: {name}, " +
                    $"requiredKillCount={requiredKillCount}, dps={damagePerSecond}, color={fieldColor}"
                );
            }
        }

        public void CleanupField()
        {
            monstersInside.Clear();
            nextDamageTickTimes.Clear();
        }

        public void ForceHideField()
        {
            isCompleted = true;
            monstersInside.Clear();
            nextDamageTickTimes.Clear();

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

        private bool IsValidTargetMonster(Monster monster, int hitObjectLayer)
        {
            if (monster == null)
            {
                return false;
            }

            if (!monster.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (monster.HP <= 0f)
            {
                return false;
            }

            if (ignoreBossMonsters && monster is BossMonster)
            {
                return false;
            }

            if (monsterLayer.value != 0)
            {
                bool hitLayerMatched = (monsterLayer.value & (1 << hitObjectLayer)) != 0;
                bool monsterRootLayerMatched = (monsterLayer.value & (1 << monster.gameObject.layer)) != 0;

                if (!hitLayerMatched && !monsterRootLayerMatched)
                {
                    return false;
                }
            }

            return true;
        }

        private void TickDamageToMonstersInside()
        {
            float safeTickInterval = Mathf.Max(0.05f, damageTickInterval);
            float damageThisTick = Mathf.Max(0f, damagePerSecond) * safeTickInterval;

            if (damageThisTick <= 0f)
            {
                return;
            }

            for (int i = monstersInside.Count - 1; i >= 0; i--)
            {
                Monster monster = monstersInside[i];

                if (!IsValidTargetMonster(monster, monster != null ? monster.gameObject.layer : 0))
                {
                    monstersInside.RemoveAt(i);

                    if (monster != null)
                    {
                        nextDamageTickTimes.Remove(monster);
                    }

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
                return;
            }

            if (ownerRoom != null && !ownerRoom.TryRegisterAcidKill(this, monster))
            {
                return;
            }

            monstersInside.Remove(monster);
            nextDamageTickTimes.Remove(monster);

            KillMonsterAsEnvironment(monster);

            currentKillCount = Mathf.Clamp(currentKillCount + 1, 0, requiredKillCount);
            UpdateProgressVisual();

            ownerRoom?.NotifyMonsterDissolved(this, monster);

            if (debugLog)
            {
                Debug.Log(
                    $"[MiniStageAcidLureField] 몬스터 위산 처치. " +
                    $"field={name}, count={currentKillCount}/{requiredKillCount}, monster={monster.name}"
                );
            }

            if (currentKillCount >= requiredKillCount)
            {
                CompleteField();
            }
        }

        /// <summary>
        /// 플레이어 처치 보상/킬 카운트로 들어가지 않도록 환경 처치로 몬스터를 제거합니다.
        /// 기존 Monster.TakeDamage()에 막타를 넣지 않고,
        /// currentHealth를 0으로 만든 뒤 Killed(false)를 호출합니다.
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
            monstersInside.Clear();
            nextDamageTickTimes.Clear();

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
                Vector3 center = transform.position + (Vector3)circleCollider.offset;
                float radius = circleCollider.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
                Gizmos.DrawWireSphere(center, radius);
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