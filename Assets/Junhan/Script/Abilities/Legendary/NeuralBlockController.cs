using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 전설증강: 신경차단
    /// 일정 주기로 화면 안의 모든 몬스터에게 짧은 정지 상태를 부여한다.
    /// 신경차단이 활성화된 동안 새로 스폰된 몬스터도 남은 시간만큼 정지 상태에 들어간다.
    /// </summary>
    public class NeuralBlockController : MonoBehaviour
    {
        [Header("Neural Block / 신경차단")]
        [Tooltip("신경차단이 발동되는 주기입니다.")]
        [SerializeField] private float interval = 12f;

        [Tooltip("몬스터가 정지되는 시간입니다.")]
        [SerializeField] private float freezeDuration = 1f;

        [Tooltip("화면 밖 몬스터까지 살짝 포함할 여유값입니다. 0이면 정확히 화면 안만 포함합니다.")]
        [SerializeField] private float screenPadding = 0.08f;

        [Tooltip("몬스터 레이어입니다.")]
        [SerializeField] private LayerMask monsterLayer;

        [Tooltip("체크하면 신경차단 발동 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = false;

        private Camera mainCamera;
        private float timer = 0f;
        private float activeUntilTime = -1f;

        public bool IsBlocking => Time.time < activeUntilTime;

        public void Configure(
            float interval,
            float freezeDuration,
            float screenPadding,
            LayerMask monsterLayer,
            bool debugLog)
        {
            this.interval = Mathf.Max(0.1f, interval);
            this.freezeDuration = Mathf.Max(0.05f, freezeDuration);
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
                ActivateNeuralBlock();
            }

            // 신경차단이 활성화된 동안 새로 스폰된 몬스터도 남은 시간만큼 정지 상태에 넣는다.
            if (IsBlocking)
            {
                ApplyFreezeToScreenMonsters(activeUntilTime - Time.time);
            }
        }

        private void ActivateNeuralBlock()
        {
            activeUntilTime = Time.time + freezeDuration;

            if (debugLog)
            {
                Debug.Log($"[신경차단] 발동 | Duration={freezeDuration:0.00}s", this);
            }

            ApplyFreezeToScreenMonsters(freezeDuration);
        }

        private void ApplyFreezeToScreenMonsters(float remainingDuration)
        {
            if (remainingDuration <= 0f)
            {
                return;
            }

            Monster[] monsters = FindObjectsOfType<Monster>();

            for (int i = 0; i < monsters.Length; i++)
            {
                Monster monster = monsters[i];

                if (monster == null || !monster.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!IsMonsterLayerMatched(monster.gameObject))
                {
                    continue;
                }

                if (!IsInScreen(monster.transform.position))
                {
                    continue;
                }

                NeuralBlockedMonsterStatus status = monster.GetComponent<NeuralBlockedMonsterStatus>();

                if (status == null)
                {
                    status = monster.gameObject.AddComponent<NeuralBlockedMonsterStatus>();
                }

                status.Apply(remainingDuration);
            }
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