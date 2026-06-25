using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 전설증강: 장기압착
    /// 일정 주기마다 화면 안 적 밀집 구역을 찾고, 그 위치에 압착장을 생성한다.
    /// </summary>
    public class OrganCompressionController : MonoBehaviour
    {
        [Header("Organ Compression / 장기압착")]
        [Tooltip("장기압착장이 생성되는 주기입니다.")]
        [SerializeField] private float interval = 20f;

        [Tooltip("적 밀집도를 계산할 때 사용하는 탐색 반경입니다.")]
        [SerializeField] private float clusterSearchRadius = 2.5f;

        [Tooltip("압착장 실제 피해/흡입 반경입니다.")]
        [SerializeField] private float fieldRadius = 2f;

        [Tooltip("압착장 유지 시간입니다.")]
        [SerializeField] private float fieldDuration = 4f;

        [Tooltip("압착장 안의 적에게 피해가 들어가는 간격입니다.")]
        [SerializeField] private float damageTickInterval = 0.5f;

        [Tooltip("피해 1틱당 데미지입니다.")]
        [SerializeField] private float damagePerTick = 3f;

        [Tooltip("중앙으로 끌어당기는 속도입니다.")]
        [SerializeField] private float pullSpeed = 2.5f;

        [Tooltip("화면 밖 몬스터까지 살짝 포함할 여유값입니다.")]
        [SerializeField] private float screenPadding = 0.08f;

        [Tooltip("몬스터 레이어입니다.")]
        [SerializeField] private LayerMask monsterLayer;

        [Tooltip("체크하면 장기압착 발동 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = false;

        private Camera mainCamera;
        private float timer = 0f;

        public void Configure(
            float interval,
            float clusterSearchRadius,
            float fieldRadius,
            float fieldDuration,
            float damageTickInterval,
            float damagePerTick,
            float pullSpeed,
            float screenPadding,
            LayerMask monsterLayer,
            bool debugLog)
        {
            this.interval = Mathf.Max(0.1f, interval);
            this.clusterSearchRadius = Mathf.Max(0.1f, clusterSearchRadius);
            this.fieldRadius = Mathf.Max(0.1f, fieldRadius);
            this.fieldDuration = Mathf.Max(0.1f, fieldDuration);
            this.damageTickInterval = Mathf.Max(0.05f, damageTickInterval);
            this.damagePerTick = Mathf.Max(0f, damagePerTick);
            this.pullSpeed = Mathf.Max(0f, pullSpeed);
            this.screenPadding = Mathf.Max(0f, screenPadding);
            this.monsterLayer = monsterLayer;
            this.debugLog = debugLog;

            mainCamera = Camera.main;
        }

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void Update()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            timer += Time.deltaTime;

            if (timer >= interval)
            {
                timer -= interval;
                SpawnOrganCompressionField();
            }
        }

        private void SpawnOrganCompressionField()
        {
            if (!TryFindBestClusterCenter(out Vector2 center, out int clusterCount))
            {
                if (debugLog)
                {
                    Debug.Log("[장기압착] 화면 안 몬스터가 없어 발동하지 않음", this);
                }

                return;
            }

            GameObject fieldObject = new GameObject("Organ Compression Field");
            fieldObject.transform.position = center;

            OrganCompressionField field = fieldObject.AddComponent<OrganCompressionField>();
            field.Init(
                center,
                fieldRadius,
                fieldDuration,
                damageTickInterval,
                damagePerTick,
                pullSpeed,
                monsterLayer
            );

            if (debugLog)
            {
                Debug.Log($"[장기압착] 생성 | Center={center} | ClusterCount={clusterCount}", this);
            }
        }

        private bool TryFindBestClusterCenter(out Vector2 bestCenter, out int bestCount)
        {
            bestCenter = Vector2.zero;
            bestCount = 0;

            Monster[] monsters = FindObjectsOfType<Monster>();

            for (int i = 0; i < monsters.Length; i++)
            {
                Monster candidate = monsters[i];

                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!IsMonsterLayerMatched(candidate.gameObject))
                {
                    continue;
                }

                Vector2 candidatePosition = candidate.CenterTransform != null
                    ? (Vector2)candidate.CenterTransform.position
                    : (Vector2)candidate.transform.position;

                if (!IsInScreen(candidatePosition))
                {
                    continue;
                }

                int count = CountMonstersAround(candidatePosition);

                if (count > bestCount)
                {
                    bestCount = count;
                    bestCenter = candidatePosition;
                }
            }

            return bestCount > 0;
        }

        private int CountMonstersAround(Vector2 center)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, clusterSearchRadius, monsterLayer);

            int count = 0;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];

                if (hit == null)
                {
                    continue;
                }

                Monster monster = hit.GetComponentInParent<Monster>();

                if (monster == null || !monster.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!IsInScreen(monster.transform.position))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private bool IsMonsterLayerMatched(GameObject target)
        {
            if (monsterLayer.value == 0)
            {
                return true;
            }

            return (monsterLayer.value & (1 << target.layer)) != 0;
        }

        private bool IsInScreen(Vector3 worldPosition)
        {
            if (mainCamera == null)
            {
                return true;
            }

            Vector3 viewportPoint = mainCamera.WorldToViewportPoint(worldPosition);

            if (viewportPoint.z < 0f)
            {
                return false;
            }

            return viewportPoint.x >= -screenPadding &&
                   viewportPoint.x <= 1f + screenPadding &&
                   viewportPoint.y >= -screenPadding &&
                   viewportPoint.y <= 1f + screenPadding;
        }
    }
}