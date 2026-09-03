using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    /// <summary>
    /// 증강 선택창, 상점창처럼 화면을 덮는 모달 UI가 열렸을 때
    /// 미니맵이 패널 위로 올라오는 문제를 막기 위한 레이어 컨트롤러입니다.
    /// </summary>
    public class ModalMinimapLayerController : MonoBehaviour
    {
        public static ModalMinimapLayerController Instance { get; private set; }

        [Header("Mini Map Target")]
        [Tooltip("뒤로 보낼 미니맵 루트 오브젝트입니다. 미니맵 전체 패널/프레임 루트를 넣는 것을 권장합니다.")]
        [SerializeField] private GameObject miniMapRoot;

        [Tooltip("비워져 있을 때 이름에 MiniMap 또는 Minimap이 들어간 RawImage를 자동으로 찾아봅니다. 정확한 처리를 위해서는 직접 연결하는 것이 좋습니다.")]
        [SerializeField] private bool autoFindMiniMapIfEmpty = true;

        [Header("Canvas Control")]
        [Tooltip("미니맵 루트에 Canvas가 없으면 런타임에 Canvas를 추가해서 레이어 순서를 강제로 제어합니다.")]
        [SerializeField] private bool addCanvasIfMissing = true;

        [Tooltip("증강 선택창/상점창이 열렸을 때 미니맵에 적용할 Sorting Order입니다. 낮을수록 뒤로 갑니다.")]
        [SerializeField] private int modalSortingOrder = -100;

        [Header("Alpha")]
        [Tooltip("체크하면 미니맵 루트에 CanvasGroup을 붙여 알파값과 클릭 차단 여부를 제어합니다.")]
        [SerializeField] private bool controlCanvasGroup = true;

        [Tooltip("일반 게임 화면에서의 미니맵 알파값입니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float normalAlpha = 1f;

        [Tooltip("증강 선택창/상점창이 열렸을 때의 미니맵 알파값입니다. 검은 반투명 배경 뒤에 깔릴 것이므로 보통 1로 둬도 됩니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float modalAlpha = 1f;

        [Header("Debug")]
        [Tooltip("체크하면 미니맵 레이어 변경 로그를 Console에 출력합니다.")]
        [SerializeField] private bool debugLog = false;

        private Canvas miniMapCanvas;
        private CanvasGroup miniMapCanvasGroup;

        private bool originalCanvasStateCached = false;
        private bool originalOverrideSorting = false;
        private int originalSortingOrder = 0;

        private int modalOpenCount = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            ResolveMiniMapRoot();
            PrepareMiniMapCanvas();
            ApplyNormalLayer();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static void PushModalLayer()
        {
            if (Instance == null)
            {
                return;
            }

            Instance.PushModal();
        }

        public static void PopModalLayer()
        {
            if (Instance == null)
            {
                return;
            }

            Instance.PopModal();
        }

        private void PushModal()
        {
            modalOpenCount++;

            ResolveMiniMapRoot();
            PrepareMiniMapCanvas();
            ApplyModalLayer();

            if (debugLog)
            {
                Debug.Log($"[ModalMinimapLayerController] Modal Open Count: {modalOpenCount}");
            }
        }

        private void PopModal()
        {
            modalOpenCount = Mathf.Max(0, modalOpenCount - 1);

            if (modalOpenCount <= 0)
            {
                ApplyNormalLayer();
            }

            if (debugLog)
            {
                Debug.Log($"[ModalMinimapLayerController] Modal Open Count: {modalOpenCount}");
            }
        }

        private void ResolveMiniMapRoot()
        {
            if (miniMapRoot != null)
            {
                return;
            }

            if (!autoFindMiniMapIfEmpty)
            {
                return;
            }

            RawImage[] rawImages = FindObjectsOfType<RawImage>(true);

            for (int i = 0; i < rawImages.Length; i++)
            {
                RawImage rawImage = rawImages[i];

                if (rawImage == null)
                {
                    continue;
                }

                string lowerName = rawImage.gameObject.name.ToLower();

                if (lowerName.Contains("minimap") || lowerName.Contains("mini_map") || lowerName.Contains("mini map"))
                {
                    miniMapRoot = rawImage.gameObject;

                    if (debugLog)
                    {
                        Debug.Log($"[ModalMinimapLayerController] 자동으로 미니맵을 찾았습니다: {miniMapRoot.name}");
                    }

                    return;
                }
            }
        }

        private void PrepareMiniMapCanvas()
        {
            if (miniMapRoot == null)
            {
                return;
            }

            if (miniMapCanvas == null)
            {
                miniMapCanvas = miniMapRoot.GetComponent<Canvas>();

                if (miniMapCanvas == null && addCanvasIfMissing)
                {
                    miniMapCanvas = miniMapRoot.AddComponent<Canvas>();
                }
            }

            if (miniMapCanvas != null && !originalCanvasStateCached)
            {
                originalOverrideSorting = miniMapCanvas.overrideSorting;
                originalSortingOrder = miniMapCanvas.sortingOrder;
                originalCanvasStateCached = true;
            }

            if (controlCanvasGroup && miniMapCanvasGroup == null)
            {
                miniMapCanvasGroup = miniMapRoot.GetComponent<CanvasGroup>();

                if (miniMapCanvasGroup == null)
                {
                    miniMapCanvasGroup = miniMapRoot.AddComponent<CanvasGroup>();
                }
            }
        }

        private void ApplyModalLayer()
        {
            if (miniMapRoot == null)
            {
                return;
            }

            miniMapRoot.SetActive(true);

            if (miniMapCanvas != null)
            {
                miniMapCanvas.overrideSorting = true;
                miniMapCanvas.sortingOrder = modalSortingOrder;
            }

            if (miniMapCanvasGroup != null)
            {
                miniMapCanvasGroup.alpha = modalAlpha;
                miniMapCanvasGroup.interactable = false;
                miniMapCanvasGroup.blocksRaycasts = false;
            }
        }

        private void ApplyNormalLayer()
        {
            if (miniMapRoot == null)
            {
                return;
            }

            miniMapRoot.SetActive(true);

            if (miniMapCanvas != null && originalCanvasStateCached)
            {
                miniMapCanvas.overrideSorting = originalOverrideSorting;
                miniMapCanvas.sortingOrder = originalSortingOrder;
            }

            if (miniMapCanvasGroup != null)
            {
                miniMapCanvasGroup.alpha = normalAlpha;
                miniMapCanvasGroup.interactable = true;
                miniMapCanvasGroup.blocksRaycasts = false;
            }
        }
    }
}