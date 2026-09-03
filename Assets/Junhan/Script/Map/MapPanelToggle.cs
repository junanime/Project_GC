using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Vampire
{
    /// <summary>
    /// Tab 키로 전체 지도 패널을 열고 닫는 컨트롤러.
    ///
    /// 현재는 지도 페이지만 열지만,
    /// 나중에 아이템/증강/스탯/전체 지도 탭을 관리하는
    /// 패널 컨트롤러로 확장할 수 있다.
    /// </summary>
    public class MapPanelToggle : MonoBehaviour
    {
        // ========================================
        // References
        // ========================================

        [Header("References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private ExplorationMapSystem mapSystem;

        [Tooltip(
            "MapPanel에 붙어 있는 TabMapResizeController. " +
            "TAB을 닫을 때 확대된 지도를 원래 위치로 복구하는 데 사용합니다."
        )]
        [SerializeField]
        private TabMapResizeController mapResizeController;


        // ========================================
        // Input
        // ========================================

        [Header("Input")]
        [SerializeField] private Key toggleKey = Key.Tab;
        [SerializeField] private bool holdToOpen = false;
        [SerializeField] private bool closeWithEscape = true;


        // ========================================
        // Pause
        // ========================================

        [Header("Pause Option")]

        [Tooltip(
            "true면 지도 패널이 열렸을 때 게임 시간을 멈춥니다. " +
            "현재는 false 추천입니다."
        )]
        [SerializeField]
        private bool pauseGameWhileOpen = false;


        // ========================================
        // Start State
        // ========================================

        [Header("Start State")]
        [SerializeField] private bool openOnStart = false;


        // ========================================
        // Runtime
        // ========================================

        private bool isOpen = false;
        private float previousTimeScale = 1f;


        // ========================================
        // Unity
        // ========================================

        private void Awake()
        {
            ResolveReferences();

            SetOpen(
                openOnStart,
                false
            );
        }


        private void Update()
        {
            if (Keyboard.current == null)
            {
                return;
            }


            KeyControl keyControl =
                Keyboard.current[toggleKey];


            if (keyControl == null)
            {
                return;
            }


            // ========================================
            // Hold 방식
            // ========================================

            if (holdToOpen)
            {
                SetOpen(
                    keyControl.isPressed
                );

                return;
            }


            // ========================================
            // Tab Toggle
            // ========================================

            if (keyControl.wasPressedThisFrame)
            {
                SetOpen(
                    !isOpen
                );
            }


            // ========================================
            // ESC Close
            // ========================================

            if (isOpen &&
                closeWithEscape &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetOpen(false);
            }
        }


        // ========================================
        // References
        // ========================================

        private void ResolveReferences()
        {
            // ExplorationMapSystem
            if (mapSystem == null)
            {
                mapSystem =
                    FindObjectOfType<ExplorationMapSystem>();
            }


            // TabMapResizeController
            //
            // 원래 MapPanel은 panelRoot 밑에 있으므로
            // 우선 panelRoot 내부에서 찾는다.
            if (mapResizeController == null &&
                panelRoot != null)
            {
                mapResizeController =
                    panelRoot.GetComponentInChildren
                        <TabMapResizeController>(true);
            }


            // 혹시 panelRoot에서 못 찾은 경우
            // Scene 전체에서 한 번 더 찾는다.
            if (mapResizeController == null)
            {
                mapResizeController =
                    FindObjectOfType
                        <TabMapResizeController>(true);
            }
        }


        // ========================================
        // Set Open
        // ========================================

        public void SetOpen(bool open)
        {
            SetOpen(
                open,
                true
            );
        }


        private void SetOpen(
            bool open,
            bool applyPause)
        {
            // 이미 같은 상태라면 종료
            if (isOpen == open &&
                panelRoot != null &&
                panelRoot.activeSelf == open)
            {
                return;
            }


            // ========================================
            // ★ TAB 닫기 전에 확대 지도 원위치
            // ========================================

            if (!open)
            {
                RestoreExpandedMap();
            }


            // ========================================
            // 상태 변경
            // ========================================

            isOpen = open;


            // ========================================
            // 전체 TAB 패널
            // ========================================

            if (panelRoot != null)
            {
                panelRoot.SetActive(
                    isOpen
                );
            }


            // ========================================
            // Map System
            // ========================================

            if (mapSystem != null)
            {
                mapSystem.SetFullMapPanelVisible(
                    isOpen
                );
            }


            // ========================================
            // Pause
            // ========================================

            if (!applyPause ||
                !pauseGameWhileOpen)
            {
                return;
            }


            if (isOpen)
            {
                previousTimeScale =
                    Time.timeScale;

                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale =
                    previousTimeScale;
            }
        }


        // ========================================
        // Restore Expanded Map
        // ========================================

        /// <summary>
        /// + 버튼으로 확대되어 다른 부모로 이동한 MapPanel을
        /// TAB 패널을 닫기 전에 원래 위치/크기로 되돌립니다.
        /// </summary>
        private void RestoreExpandedMap()
        {
            // 참조가 없는 경우 다시 찾아본다.
            if (mapResizeController == null)
            {
                ResolveReferences();
            }


            if (mapResizeController == null)
            {
                return;
            }


            // 확대 상태일 때만 축소
            if (mapResizeController.IsExpanded)
            {
                mapResizeController.ShrinkMap();
            }
        }


        // ========================================
        // Public
        // ========================================

        public void Toggle()
        {
            SetOpen(
                !isOpen
            );
        }


        public bool IsOpen()
        {
            return isOpen;
        }
    }
}