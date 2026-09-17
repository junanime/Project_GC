using UnityEngine;

namespace Vampire
{
    /// <summary>Visual prefabs for independent cores. Multi-core masks never synthesize another color.</summary>
    [DisallowMultipleComponent]
    public class BossCoreVisualController : MonoBehaviour
    {
        [Header("Core State Reference")]

        [Tooltip(
            "현재 Core Mode와 Trait를 관리하는 BossCoreStateController입니다. " +
            "가능하면 Inspector에서 직접 연결하세요. " +
            "비워두면 현재 오브젝트/부모/자식에서 자동 탐색합니다.")]
        [SerializeField]
        private BossCoreStateController coreStateController;


        [Header("Core Visual Root")]

        [Tooltip(
            "코어 외형 Prefab을 생성할 부모 Transform입니다. " +
            "실제 Core 판정 오브젝트를 교체하지 말고, " +
            "그 아래에 빈 CoreVisualRoot를 만든 뒤 연결하는 것을 권장합니다.")]
        [SerializeField]
        private Transform coreVisualRoot;


        [Header("Phase 1 - Single Core Prefabs")]

        [Tooltip("Red Core 상태에서 표시할 시각용 Prefab입니다.")]
        [SerializeField]
        private GameObject redCorePrefab;

        [Tooltip("Yellow Core 상태에서 표시할 시각용 Prefab입니다.")]
        [SerializeField]
        private GameObject yellowCorePrefab;

        [Tooltip("Blue Core 상태에서 표시할 시각용 Prefab입니다.")]
        [SerializeField]
        private GameObject blueCorePrefab;


        [Header("Phase 2 - Dual Core Prefabs")]

        [Tooltip(
            "Orange Core 상태에서 표시할 Prefab입니다. " +
            "Orange is an independent core.")]
        [SerializeField]
        private GameObject orangeCorePrefab;

        [Tooltip(
            "Purple Core 상태에서 표시할 Prefab입니다. " +
            "Reserved legacy reference; not used for color mixing.")]
        [SerializeField]
        private GameObject purpleCorePrefab;

        [Tooltip(
            "Green Core 상태에서 표시할 Prefab입니다. " +
            "Green is an independent core.")]
        [SerializeField]
        private GameObject greenCorePrefab;


        [Header("Phase 3 - Rainbow Core Prefab")]

        [Tooltip(
            "Rainbow Core 상태에서 표시할 Prefab입니다. " +
            "Rainbow는 Red + Yellow + Blue 전체 Trait입니다.")]
        [SerializeField]
        private GameObject rainbowCorePrefab;


        [Header("Runtime - Read Only")]

        [Tooltip("현재 표시 중인 Core Mode입니다. 런타임 확인용입니다.")]
        [SerializeField]
        private BossCoreMode currentVisualMode =
            BossCoreMode.Single;

        [Tooltip("현재 표시 중인 Core Trait입니다. 런타임 확인용입니다.")]
        [SerializeField]
        private BossCoreTrait currentVisualTraits =
            BossCoreTrait.None;

        [Tooltip(
            "현재 생성되어 있는 시각용 Core Prefab 인스턴스입니다. " +
            "런타임 확인용입니다.")]
        [SerializeField]
        private GameObject currentVisualInstance;


        [Header("Debug")]

        [Tooltip(
            "Core Visual 변경 로그와 Prefab 누락 경고를 출력합니다.")]
        [SerializeField]
        private bool debugLog = false;


        private bool subscribed;


        public GameObject CurrentVisualInstance =>
            currentVisualInstance;

        public BossCoreTrait CurrentVisualTraits =>
            currentVisualTraits;


        private void Awake()
        {
            ResolveReferences();
        }


        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }


        private void Start()
        {
            // BossCoreStateController가 이미 초기화된 상태라면
            // 현재 상태를 즉시 외형에 반영합니다.
            RefreshFromCurrentCore();
        }


        private void OnDisable()
        {
            Unsubscribe();
        }


        /// <summary>
        /// 현재 BossCoreStateController 상태를 다시 읽어
        /// Core Visual을 강제로 동기화합니다.
        /// </summary>
        public void RefreshFromCurrentCore()
        {
            ResolveReferences();

            if (coreStateController == null)
            {
                if (debugLog)
                {
                    Debug.LogWarning(
                        "[BossCoreVisual] " +
                        "BossCoreStateController를 찾지 못했습니다.",
                        this);
                }

                return;
            }

            ApplyCoreVisual(
                coreStateController.CurrentMode,
                coreStateController.ActiveTraits);
        }


        private void ResolveReferences()
        {
            if (coreStateController == null)
            {
                coreStateController =
                    GetComponent<BossCoreStateController>();

                if (coreStateController == null)
                {
                    coreStateController =
                        GetComponentInParent
                        <BossCoreStateController>();
                }

                if (coreStateController == null)
                {
                    coreStateController =
                        GetComponentInChildren
                        <BossCoreStateController>(true);
                }
            }

            if (coreVisualRoot == null)
            {
                coreVisualRoot = transform;
            }
        }


        private void Subscribe()
        {
            if (subscribed ||
                coreStateController == null)
            {
                return;
            }

            coreStateController.CoreChanged +=
                HandleCoreChanged;

            subscribed = true;
        }


        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (coreStateController != null)
            {
                coreStateController.CoreChanged -=
                    HandleCoreChanged;
            }

            subscribed = false;
        }


        private void HandleCoreChanged(
            BossCoreMode mode,
            BossCoreTrait traits)
        {
            ApplyCoreVisual(mode, traits);
        }


        private void ApplyCoreVisual(
            BossCoreMode mode,
            BossCoreTrait traits)
        {
            // 같은 상태라면 불필요하게 재생성하지 않습니다.
            if (currentVisualInstance != null &&
                currentVisualMode == mode &&
                currentVisualTraits == traits)
            {
                return;
            }

            currentVisualMode = mode;
            currentVisualTraits = traits;

            ClearCurrentVisual();
            Transform parent = coreVisualRoot != null ? coreVisualRoot : transform;
            currentVisualInstance = new GameObject("Active Core Visuals");
            currentVisualInstance.transform.SetParent(parent, false);
            BossCoreTrait[] singles = { BossCoreTrait.Red, BossCoreTrait.Orange, BossCoreTrait.Yellow, BossCoreTrait.Green, BossCoreTrait.Blue };
            int count = 0;
            foreach (var single in singles) if ((traits & single) != 0) count++;
            int index = 0;
            foreach (var single in singles)
            {
                if ((traits & single) == 0) continue;
                GameObject prefab = GetPrefabFor(BossCoreMode.Single, single);
                if (prefab == null) continue;
                var instance = Instantiate(prefab, currentVisualInstance.transform);
                instance.transform.localPosition = new Vector3((index++ - (count - 1) * 0.5f) * 0.8f, 0, 0);
                instance.transform.localRotation = Quaternion.identity;
            }
        }

        private GameObject GetPrefabFor(
            BossCoreMode mode,
            BossCoreTrait traits)
        {
            // Phase 3 / Rainbow
            if (mode == BossCoreMode.Rainbow ||
                traits == BossCoreTrait.All)
            {
                return rainbowCorePrefab;
            }

            // Phase 1
            if (traits == BossCoreTrait.Red)
            {
                return redCorePrefab;
            }

            if (traits == BossCoreTrait.Yellow)
            {
                return yellowCorePrefab;
            }

            if (traits == BossCoreTrait.Blue)
            {
                return blueCorePrefab;
            }

            if (traits == BossCoreTrait.Orange) return orangeCorePrefab;
            if (traits == BossCoreTrait.Green) return greenCorePrefab;
            return null;
        }


        private void ClearCurrentVisual()
        {
            if (currentVisualInstance == null)
            {
                return;
            }

            // Destroy는 프레임 끝에 처리되므로
            // 먼저 비활성화해서 두 Core가 한 프레임 겹쳐 보이는 것을 막습니다.
            currentVisualInstance.SetActive(false);

            Destroy(currentVisualInstance);

            currentVisualInstance = null;
        }
    }
}