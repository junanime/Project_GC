using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class TabMapResizeController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private RectTransform mapPanel;

        [Header("Expanded Parent")]
        [SerializeField] private RectTransform expandedParent;

        [Header("Buttons")]
        [SerializeField] private Button expandButton;
        [SerializeField] private Button shrinkButton;

        [Header("Expand Setting")]
        [SerializeField] private float expandMultiplier = 1.6f;
        [SerializeField] private Vector2 maxExpandedSize = new Vector2(300f, 180f);
        [SerializeField] private Vector2 expandedPosition = Vector2.zero;

        private Transform originalParent;
        private int originalSiblingIndex;

        private Vector2 normalSize;
        private Vector2 normalPosition;
        private Vector2 normalAnchorMin;
        private Vector2 normalAnchorMax;
        private Vector2 normalPivot;
        private Vector3 normalScale;

        private bool initialized;
        private bool isExpanded;

        private void Awake()
        {
            CacheNormalState();

            SetupButton(expandButton, ExpandMap);
            SetupButton(shrinkButton, ShrinkMap);

            RefreshButtons();
        }

        private void OnEnable()
        {
            RefreshButtons();
        }

        private void CacheNormalState()
        {
            if (mapPanel == null || initialized)
            {
                return;
            }

            originalParent = mapPanel.parent;
            originalSiblingIndex = mapPanel.GetSiblingIndex();

            normalSize = mapPanel.sizeDelta;
            normalPosition = mapPanel.anchoredPosition;
            normalAnchorMin = mapPanel.anchorMin;
            normalAnchorMax = mapPanel.anchorMax;
            normalPivot = mapPanel.pivot;
            normalScale = mapPanel.localScale;

            initialized = true;
        }

        private void SetupButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.transition = Selectable.Transition.None;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);

            if (button.GetComponent<UIButtonPressEffect>() == null)
            {
                button.gameObject.AddComponent<UIButtonPressEffect>();
            }
        }

        public void ExpandMap()
        {
            if (mapPanel == null)
            {
                return;
            }

            CacheNormalState();

            isExpanded = true;

            RectTransform targetParent = expandedParent;

            if (targetParent != null)
            {
                mapPanel.SetParent(targetParent, false);
            }

            Vector2 targetSize = normalSize * expandMultiplier;
            targetSize.x = Mathf.Min(targetSize.x, maxExpandedSize.x);
            targetSize.y = Mathf.Min(targetSize.y, maxExpandedSize.y);

            mapPanel.anchorMin = new Vector2(0.5f, 0.5f);
            mapPanel.anchorMax = new Vector2(0.5f, 0.5f);
            mapPanel.pivot = new Vector2(0.5f, 0.5f);

            mapPanel.localScale = Vector3.one;
            mapPanel.sizeDelta = targetSize;
            mapPanel.anchoredPosition = expandedPosition;

            mapPanel.SetAsLastSibling();

            RefreshButtons();
        }

        public void ShrinkMap()
        {
            if (mapPanel == null)
            {
                return;
            }

            isExpanded = false;

            if (originalParent != null)
            {
                mapPanel.SetParent(originalParent, false);
                mapPanel.SetSiblingIndex(originalSiblingIndex);
            }

            mapPanel.anchorMin = normalAnchorMin;
            mapPanel.anchorMax = normalAnchorMax;
            mapPanel.pivot = normalPivot;

            mapPanel.localScale = normalScale;
            mapPanel.sizeDelta = normalSize;
            mapPanel.anchoredPosition = normalPosition;

            RefreshButtons();
        }

        private void RefreshButtons()
        {
            if (expandButton != null)
            {
                expandButton.gameObject.SetActive(!isExpanded);
            }

            if (shrinkButton != null)
            {
                shrinkButton.gameObject.SetActive(isExpanded);
            }
        }
    }
}