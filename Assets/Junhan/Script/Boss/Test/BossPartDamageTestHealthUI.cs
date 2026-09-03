using TMPro;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 테스트용 파츠 보스의 전체 HP를
    /// 기존 PointBar UI에 표시합니다.
    ///
    /// 동작:
    /// - 보스가 없으면 UI 숨김
    /// - 살아 있는 테스트 보스를 찾으면 UI 표시
    /// - HP 변화 시 PointBar 및 텍스트 갱신
    /// - 보스 사망 시 UI 숨김
    /// - 이후 새로운 보스가 생성되면 다시 자동 연결
    ///
    /// UI GameObject 자체를 비활성화하지 않고
    /// CanvasGroup을 이용해 숨깁니다.
    /// 그래야 이 컴포넌트가 계속 Update를 실행하면서
    /// 다음 보스를 탐색할 수 있습니다.
    /// </summary>
    public sealed class BossPartDamageTestHealthUI :
        MonoBehaviour
    {
        [Header("UI")]

        [Tooltip(
            "보스 HP를 표시할 PointBar입니다. " +
            "기존 경험치 바를 복제한 PointBar를 연결합니다.")]
        [SerializeField]
        private PointBar bossHealthBar;

        [Tooltip(
            "BOSS 60 / 60 형식으로 표시할 TMP Text입니다.")]
        [SerializeField]
        private TextMeshProUGUI healthText;

        [Tooltip(
            "체력바 앞에 표시할 보스 이름입니다.")]
        [SerializeField]
        private string bossLabel = "BOSS";

        [Header("UI 표시/숨김")]

        [Tooltip(
            "보스 체력바 전체를 표시하거나 숨기는 CanvasGroup입니다. " +
            "비워 두면 같은 GameObject에서 찾고, 없으면 런타임에 자동 추가합니다.")]
        [SerializeField]
        private CanvasGroup canvasGroup;

        [Tooltip(
            "보스가 존재하지 않을 때 체력바를 자동으로 숨깁니다.")]
        [SerializeField]
        private bool hideWhenNoBoss = true;

        [Tooltip(
            "보스가 사망하면 체력바를 자동으로 숨깁니다.")]
        [SerializeField]
        private bool hideWhenBossDead = true;

        [Header("보스 연결")]

        [Tooltip(
            "직접 지정할 테스트 보스 RootController입니다. " +
            "비워 두면 런타임에서 자동 탐색합니다.")]
        [SerializeField]
        private BossPartDamageTestRootController targetBoss;

        [Tooltip(
            "런타임에 살아 있는 테스트 보스를 자동으로 찾습니다.")]
        [SerializeField]
        private bool autoFindBoss = true;

        [Tooltip(
            "보스 자동 탐색 간격입니다.")]
        [SerializeField, Min(0.05f)]
        private float findInterval = 0.2f;

        [Header("디버그")]

        [Tooltip(
            "보스 UI 연결, 표시, 숨김, 갱신 로그를 출력합니다.")]
        [SerializeField]
        private bool debugLog = false;

        private float nextFindTime;

        private float lastCurrentHealth =
            float.MinValue;

        private float lastMaxHealth =
            float.MinValue;

        private bool lastBossDead;

        private bool isVisible;

        private void Awake()
        {
            ResolveUIReferences();

            // 게임 시작 시에는 보스가 아직 없으므로
            // 우선 체력바를 숨겨둡니다.
            if (hideWhenNoBoss)
            {
                SetUIVisible(false);
            }
        }

        private void OnEnable()
        {
            ResolveUIReferences();

            ResetCachedValues();

            if (targetBoss != null &&
                !targetBoss.IsBossDead)
            {
                RefreshUI(true);
            }
            else if (hideWhenNoBoss)
            {
                SetUIVisible(false);
            }
        }

        private void Update()
        {
            // 기존에 연결했던 보스가 Destroy됐거나
            // 아직 아무 보스도 연결되지 않은 상태.
            if (targetBoss == null)
            {
                if (hideWhenNoBoss)
                {
                    SetUIVisible(false);
                }

                TryFindBossAtInterval();

                return;
            }

            // Core 파괴 등으로 보스 사망.
            if (targetBoss.IsBossDead)
            {
                HandleBossEnded();

                return;
            }

            RefreshUI(false);
        }

        /// <summary>
        /// 외부에서 특정 테스트 보스를 직접 연결할 때 사용합니다.
        /// </summary>
        public void Bind(
            BossPartDamageTestRootController boss)
        {
            if (boss == null)
            {
                return;
            }

            // 이미 죽은 보스에는 UI를 연결하지 않습니다.
            if (boss.IsBossDead)
            {
                return;
            }

            targetBoss = boss;

            ResetCachedValues();

            RefreshUI(true);

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartHealthUI] 보스 연결 완료 | " +
                    $"HP={targetBoss.CurrentBossHealth:0.##}/" +
                    $"{targetBoss.TotalMaxHealth:0.##}",
                    this);
            }
        }

        /// <summary>
        /// 일정 주기마다 살아 있는 테스트 보스를 찾습니다.
        /// </summary>
        private void TryFindBossAtInterval()
        {
            if (!autoFindBoss)
            {
                return;
            }

            if (Time.unscaledTime < nextFindTime)
            {
                return;
            }

            nextFindTime =
                Time.unscaledTime +
                findInterval;

            TryFindBoss();
        }

        /// <summary>
        /// 씬에 존재하는 RootController 중
        /// 아직 죽지 않은 보스를 찾아 연결합니다.
        ///
        /// FindObjectOfType 하나만 사용할 경우
        /// 이미 죽은 이전 테스트 보스를 다시 찾을 가능성이 있으므로
        /// 모든 RootController를 검사합니다.
        /// </summary>
        private void TryFindBoss()
        {
            BossPartDamageTestRootController[] bosses =
                FindObjectsOfType
                <
                    BossPartDamageTestRootController
                >();

            for (int i = 0;
                 i < bosses.Length;
                 i++)
            {
                BossPartDamageTestRootController boss =
                    bosses[i];

                if (boss == null)
                {
                    continue;
                }

                if (!boss.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (boss.IsBossDead)
                {
                    continue;
                }

                // 아직 파츠 초기화가 끝나지 않아
                // 최대 HP가 0인 첫 프레임은 건너뜁니다.
                if (boss.TotalMaxHealth <= 0f)
                {
                    continue;
                }

                Bind(boss);

                return;
            }
        }

        /// <summary>
        /// 현재 연결된 보스의 HP를 UI에 반영합니다.
        /// </summary>
        private void RefreshUI(
            bool force)
        {
            if (targetBoss == null)
            {
                return;
            }

            // 사망 보스는 0 HP를 잠깐 보여주는 대신
            // 바로 체력바를 숨깁니다.
            if (targetBoss.IsBossDead)
            {
                HandleBossEnded();

                return;
            }

            float maxHealth =
                targetBoss.TotalMaxHealth;

            float currentHealth =
                targetBoss.CurrentBossHealth;

            // 파츠 등록이 아직 끝나지 않은 첫 프레임.
            if (maxHealth <= 0f)
            {
                return;
            }

            currentHealth =
                Mathf.Clamp(
                    currentHealth,
                    0f,
                    maxHealth);

            // 유효한 살아 있는 보스를 발견했으므로
            // HP바를 표시합니다.
            SetUIVisible(true);

            bool changed =
                force ||
                !Mathf.Approximately(
                    currentHealth,
                    lastCurrentHealth) ||
                !Mathf.Approximately(
                    maxHealth,
                    lastMaxHealth) ||
                targetBoss.IsBossDead != lastBossDead;

            if (!changed)
            {
                return;
            }

            if (bossHealthBar != null)
            {
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
                targetBoss.IsBossDead;

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartHealthUI] 갱신 | " +
                    $"HP={currentHealth:0.##}/" +
                    $"{maxHealth:0.##}",
                    this);
            }
        }

        /// <summary>
        /// 현재 연결된 보스가 사망했을 때 호출합니다.
        /// UI를 숨기고 참조를 비워
        /// 이후 생성되는 새로운 보스를 다시 찾을 수 있게 합니다.
        /// </summary>
        private void HandleBossEnded()
        {
            if (debugLog &&
                targetBoss != null)
            {
                Debug.Log(
                    "[BossPartHealthUI] 보스 사망 감지 → " +
                    "체력바를 숨기고 연결을 해제합니다.",
                    this);
            }

            if (hideWhenBossDead)
            {
                SetUIVisible(false);
            }

            targetBoss = null;

            ResetCachedValues();

            nextFindTime =
                Time.unscaledTime +
                findInterval;
        }

        /// <summary>
        /// PointBar와 CanvasGroup 참조를 확보합니다.
        /// </summary>
        private void ResolveUIReferences()
        {
            if (bossHealthBar == null)
            {
                bossHealthBar =
                    GetComponent<PointBar>();
            }

            if (canvasGroup == null)
            {
                canvasGroup =
                    GetComponent<CanvasGroup>();
            }

            if (canvasGroup == null)
            {
                canvasGroup =
                    gameObject.AddComponent<CanvasGroup>();

                if (debugLog)
                {
                    Debug.Log(
                        "[BossPartHealthUI] CanvasGroup이 없어 " +
                        "런타임에 자동 추가했습니다.",
                        this);
                }
            }
        }

        /// <summary>
        /// 체력바의 실제 표시 여부를 제어합니다.
        ///
        /// GameObject.SetActive(false)를 사용하지 않는 이유는
        /// 스크립트 자체가 비활성화되면 다음 보스를 자동 탐색할 수 없기 때문입니다.
        /// </summary>
        private void SetUIVisible(
            bool visible)
        {
            ResolveUIReferences();

            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha =
                visible
                    ? 1f
                    : 0f;

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (isVisible == visible)
            {
                return;
            }

            isVisible = visible;

            if (debugLog)
            {
                Debug.Log(
                    visible
                        ? "[BossPartHealthUI] HP바 표시"
                        : "[BossPartHealthUI] HP바 숨김",
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