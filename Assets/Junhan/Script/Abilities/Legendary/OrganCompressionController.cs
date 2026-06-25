using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 전설증강: 장기압착
    /// 일정 주기마다 화면 안 적 밀집 구역을 찾고, 그 위치에 압착장을 생성한다.
    /// root 레이어가 monsterLayer와 달라도 자식 Collider 레이어 또는 fallback Monster 검색으로 작동하게 처리한다.
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

        [Tooltip("몬스터 레이어입니다. root 레이어뿐 아니라 자식 Collider 레이어까지 검사합니다.")]
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
                    Debug.Log("[장기압착] 화면 안 Monster 컴포넌트를 찾지 못해 발동하지 않음", this);
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

            List<Monster> screenMonsters = GetScreenMonsters();

            if (screenMonsters.Count <= 0)
            {
                return false;
            }

            for (int i = 0; i < screenMonsters.Count; i++)
            {
                Monster candidate = screenMonsters[i];

                if (candidate == null)
                {
                    continue;
                }

                Vector2 candidatePosition = GetMonsterWorldPosition(candidate);
                int count = CountMonstersAround(candidatePosition, screenMonsters);

                if (count > bestCount)
                {
                    bestCount = count;
                    bestCenter = candidatePosition;
                }
            }

            // 밀집도가 1이어도 발동되게 둔다.
            // 화면 안에 몬스터가 1마리만 있어도 그 몬스터 위치에 압착장을 생성한다.
            return bestCount > 0;
        }

        private int CountMonstersAround(Vector2 center, List<Monster> screenMonsters)
        {
            int count = 0;

            for (int i = 0; i < screenMonsters.Count; i++)
            {
                Monster monster = screenMonsters[i];

                if (monster == null || !monster.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector2 monsterPosition = GetMonsterWorldPosition(monster);

                if (Vector2.Distance(center, monsterPosition) <= clusterSearchRadius)
                {
                    count++;
                }
            }

            return count;
        }

        private List<Monster> GetScreenMonsters()
        {
            List<Monster> layerMatchedTargets = new List<Monster>();
            List<Monster> fallbackTargets = new List<Monster>();

            Monster[] monsters = FindObjectsOfType<Monster>();

            for (int i = 0; i < monsters.Length; i++)
            {
                Monster monster = monsters[i];

                if (monster == null || !monster.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector2 monsterPosition = GetMonsterWorldPosition(monster);

                if (!IsInScreen(monsterPosition))
                {
                    continue;
                }

                fallbackTargets.Add(monster);

                if (IsMonsterLayerMatched(monster))
                {
                    layerMatchedTargets.Add(monster);
                }
            }

            // 레이어 매칭 대상이 있으면 그걸 쓰고,
            // root/child 레이어 불일치 때문에 0마리면 화면 안 Monster 전체를 사용한다.
            if (layerMatchedTargets.Count > 0)
            {
                return layerMatchedTargets;
            }

            return fallbackTargets;
        }

        private Vector2 GetMonsterWorldPosition(Monster monster)
        {
            if (monster != null && monster.CenterTransform != null)
            {
                return monster.CenterTransform.position;
            }

            return monster != null ? monster.transform.position : Vector2.zero;
        }

        private bool IsMonsterLayerMatched(Monster monster)
        {
            if (monster == null)
            {
                return false;
            }

            if (monsterLayer.value == 0)
            {
                return true;
            }

            if ((monsterLayer.value & (1 << monster.gameObject.layer)) != 0)
            {
                return true;
            }

            Collider2D[] colliders = monster.GetComponentsInChildren<Collider2D>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D col = colliders[i];

                if (col == null)
                {
                    continue;
                }

                if ((monsterLayer.value & (1 << col.gameObject.layer)) != 0)
                {
                    return true;
                }
            }

            return false;
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