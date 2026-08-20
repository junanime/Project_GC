using System.Collections;
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

        [Header("Button Root")]
        [SerializeField] private RectTransform buttonRoot;

        [Header("Buttons")]
        [SerializeField] private Button expandButton;
        [SerializeField] private Button shrinkButton;

        [Header("Button Animation")]
        [SerializeField] private float normalButtonScale = 1f;
        [SerializeField] private float activeButtonScale = 1f;
        [SerializeField] private float pressedButtonScale = 0.85f;
        [SerializeField] private float buttonAnimationTime = 0.08f;

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

        private Vector3 expandButtonOriginalScale = Vector3.one;
        private Vector3 shrinkButtonOriginalScale = Vector3.one;

        private bool initialized;
        private bool isExpanded;

        private Coroutine buttonAnimationRoutine;

        private void Awake()
        {
            CacheNormalState();
            CacheButtonScales();
            ResolveButtonRoot();
            SetupButtons();

            RefreshButtons(true);
        }

        private void OnEnable()
        {
            RefreshButtons(true);
        }

        private void OnDisable()
        {
            if (isExpanded)
            {
                ShrinkMap();
            }
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

        private void CacheButtonScales()
        {
            if (expandButton != null)
            {
                expandButtonOriginalScale = expandButton.transform.localScale;
            }

            if (shrinkButton != null)
            {
                shrinkButtonOriginalScale = shrinkButton.transform.localScale;
            }
        }

        private void ResolveButtonRoot()
        {
            if (buttonRoot != null)
            {
                return;
            }

            if (expandButton != null && expandButton.transform.parent != null)
            {
                buttonRoot = expandButton.transform.parent as RectTransform;
                return;
            }

            if (shrinkButton != null && shrinkButton.transform.parent != null)
            {
                buttonRoot = shrinkButton.transform.parent as RectTransform;
            }
        }

        private void SetupButtons()
        {
            SetupButton(expandButton, OnExpandButtonClicked);
            SetupButton(shrinkButton, OnShrinkButtonClicked);
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

            UIButtonPressEffect oldPressEffect = button.GetComponent<UIButtonPressEffect>();

            if (oldPressEffect != null)
            {
                oldPressEffect.enabled = false;
            }
        }

        private void OnExpandButtonClicked()
        {
            PlayButtonAnimation(expandButton, ExpandMap);
        }

        private void OnShrinkButtonClicked()
        {
            PlayButtonAnimation(shrinkButton, ShrinkMap);
        }

        private void PlayButtonAnimation(Button clickedButton, System.Action afterAnimation)
        {
            if (buttonAnimationRoutine != null)
            {
                StopCoroutine(buttonAnimationRoutine);
            }

            buttonAnimationRoutine = StartCoroutine(
                ButtonAnimationRoutine(clickedButton, afterAnimation)
            );
        }

        private IEnumerator ButtonAnimationRoutine(Button clickedButton, System.Action afterAnimation)
        {
            RectTransform buttonRect = clickedButton != null
                ? clickedButton.transform as RectTransform
                : null;

            if (buttonRect == null)
            {
                afterAnimation?.Invoke();
                yield break;
            }

            clickedButton.interactable = false;

            Vector3 originalScale = GetOriginalButtonScale(clickedButton);
            Vector3 startScale = buttonRect.localScale;
            Vector3 pressedScale = originalScale * pressedButtonScale;

            float timer = 0f;

            while (timer < buttonAnimationTime)
            {
                timer += Time.unscaledDeltaTime;
                float t = timer / buttonAnimationTime;

                buttonRect.localScale = Vector3.Lerp(startScale, pressedScale, t);

                yield return null;
            }

            timer = 0f;

            while (timer < buttonAnimationTime)
            {
                timer += Time.unscaledDeltaTime;
                float t = timer / buttonAnimationTime;

                buttonRect.localScale = Vector3.Lerp(pressedScale, originalScale, t);

                yield return null;
            }

            buttonRect.localScale = originalScale;

            afterAnimation?.Invoke();

            clickedButton.interactable = true;
            buttonAnimationRoutine = null;
        }

        public void ExpandMap()
        {
            if (mapPanel == null)
            {
                return;
            }

            CacheNormalState();

            isExpanded = true;

            if (expandedParent != null)
            {
                mapPanel.SetParent(expandedParent, false);
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
            BringButtonRootToFront();

            RefreshButtons(false);
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

            BringButtonRootToFront();

            RefreshButtons(false);
        }

        private void RefreshButtons(bool immediate)
        {
            if (expandButton != null)
            {
                expandButton.gameObject.SetActive(!isExpanded);
                SetButtonScale(expandButton, !isExpanded);
            }

            if (shrinkButton != null)
            {
                shrinkButton.gameObject.SetActive(isExpanded);
                SetButtonScale(shrinkButton, isExpanded);
            }

            BringButtonRootToFront();
        }

        private void SetButtonScale(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.transform as RectTransform;

            if (rect == null)
            {
                return;
            }

            Vector3 originalScale = GetOriginalButtonScale(button);
            float targetScale = active ? activeButtonScale : normalButtonScale;

            rect.localScale = originalScale * targetScale;
        }

        private Vector3 GetOriginalButtonScale(Button button)
        {
            if (button == expandButton)
            {
                return expandButtonOriginalScale;
            }

            if (button == shrinkButton)
            {
                return shrinkButtonOriginalScale;
            }

            return Vector3.one;
        }

        private void BringButtonRootToFront()
        {
            if (buttonRoot != null)
            {
                buttonRoot.SetAsLastSibling();
                return;
            }

            if (expandButton != null && expandButton.transform.parent != null)
            {
                expandButton.transform.parent.SetAsLastSibling();
            }

            if (shrinkButton != null &&
                shrinkButton.transform.parent != null &&
                shrinkButton.transform.parent != expandButton.transform.parent)
            {
                shrinkButton.transform.parent.SetAsLastSibling();
            }
        }
    }
}