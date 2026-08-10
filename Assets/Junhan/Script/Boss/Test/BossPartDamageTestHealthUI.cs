using TMPro;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 테스트용 파츠 보스의 HP를
    /// 기존 PointBar UI에 표시합니다.
    ///
    /// 보스가 런타임에 생성되므로
    /// 씬에서 RootController를 자동으로 찾아 연결합니다.
    /// </summary>
    public sealed class BossPartDamageTestHealthUI :
        MonoBehaviour
    {
        [Header("UI")]

        [Tooltip(
            "보스 HP를 표시할 PointBar입니다. " +
            "기존 경험치 바를 복제해서 사용하는 것을 권장합니다.")]
        [SerializeField]
        private PointBar bossHealthBar;

        [Tooltip(
            "BOSS 60 / 60 형식으로 표시할 TMP Text입니다.")]
        [SerializeField]
        private TextMeshProUGUI healthText;

        [Tooltip(
            "체력바 앞에 표시할 이름입니다.")]
        [SerializeField]
        private string bossLabel = "BOSS";

        [Header("보스 연결")]

        [Tooltip(
            "직접 지정할 테스트 보스 RootController입니다. " +
            "비워 두면 런타임에서 자동 탐색합니다.")]
        [SerializeField]
        private BossPartDamageTestRootController targetBoss;

        [Tooltip(
            "보스가 런타임에 생성될 때 자동으로 찾을지 여부입니다.")]
        [SerializeField]
        private bool autoFindBoss = true;

        [Tooltip(
            "보스 자동 탐색 간격입니다.")]
        [SerializeField, Min(0.05f)]
        private float findInterval = 0.2f;

        [Header("디버그")]

        [Tooltip(
            "보스 UI 연결 및 갱신 로그를 출력합니다.")]
        [SerializeField]
        private bool debugLog = false;

        private float nextFindTime;

        private float lastCurrentHealth =
            float.MinValue;

        private float lastMaxHealth =
            float.MinValue;

        private bool lastBossDead;

        private void Awake()
        {
            if (bossHealthBar == null)
            {
                bossHealthBar =
                    GetComponent<PointBar>();
            }
        }

        private void OnEnable()
        {
            ResetCachedValues();

            if (targetBoss != null)
            {
                RefreshUI(true);
            }
        }

        private void Update()
        {
            if (targetBoss == null)
            {
                if (autoFindBoss &&
                    Time.unscaledTime >= nextFindTime)
                {
                    nextFindTime =
                        Time.unscaledTime +
                        findInterval;

                    TryFindBoss();
                }

                return;
            }

            RefreshUI(false);
        }

        /// <summary>
        /// 외부에서 특정 보스를 직접 연결할 때 사용할 수 있습니다.
        /// </summary>
        public void Bind(
            BossPartDamageTestRootController boss)
        {
            targetBoss = boss;

            ResetCachedValues();

            RefreshUI(true);

            if (debugLog &&
                targetBoss != null)
            {
                Debug.Log(
                    "[BossPartHealthUI] 테스트 보스 연결 완료.",
                    this);
            }
        }

        private void TryFindBoss()
        {
            BossPartDamageTestRootController foundBoss =
                FindObjectOfType
                <
                    BossPartDamageTestRootController
                >();

            if (foundBoss == null)
            {
                return;
            }

            Bind(foundBoss);
        }

        private void RefreshUI(
            bool force)
        {
            if (targetBoss == null)
            {
                return;
            }

            float maxHealth =
                targetBoss.TotalMaxHealth;

            float currentHealth =
                targetBoss.CurrentBossHealth;

            bool bossDead =
                targetBoss.IsBossDead;

            // 파츠 등록이 아직 끝나지 않은 첫 프레임은 건너뜁니다.
            if (maxHealth <= 0f)
            {
                return;
            }

            currentHealth =
                Mathf.Clamp(
                    currentHealth,
                    0f,
                    maxHealth);

            bool changed =
                force ||
                !Mathf.Approximately(
                    currentHealth,
                    lastCurrentHealth) ||
                !Mathf.Approximately(
                    maxHealth,
                    lastMaxHealth) ||
                bossDead != lastBossDead;

            if (!changed)
            {
                return;
            }

            if (bossHealthBar != null)
            {
                // 플레이어 경험치 바와 같은 PointBar 방식.
                bossHealthBar.Setup(
                    currentHealth,
                    0f,
                    maxHealth);
            }

            if (healthText != null)
            {
                healthText.text =
                    $"{bossLabel} " +
                    $"{Mathf.CeilToInt(currentHealth)} / " +
                    $"{Mathf.CeilToInt(maxHealth)}";
            }

            lastCurrentHealth =
                currentHealth;

            lastMaxHealth =
                maxHealth;

            lastBossDead =
                bossDead;

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartHealthUI] 갱신 | " +
                    $"HP={currentHealth:0.##}/{maxHealth:0.##}, " +
                    $"Dead={bossDead}",
                    this);
            }
        }

        private void ResetCachedValues()
        {
            lastCurrentHealth =
                float.MinValue;

            lastMaxHealth =
                float.MinValue;

            lastBossDead = false;
        }
    }
}