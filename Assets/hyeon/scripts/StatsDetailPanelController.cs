using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vampire
{
    public class StatsDetailPanelController : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject statsDetailPanel;

        [Header("Button Visual")]
        [SerializeField] private Button detailButton;
        [SerializeField] private Image buttonImage;
        [SerializeField] private TextMeshProUGUI buttonText;
        [SerializeField] private RectTransform buttonRect;

        [Header("Button Text")]
        [SerializeField] private string closedText = "전체 스탯";
        [SerializeField] private string openedText = "접기";

        [Header("Button Color")]
        [SerializeField] private Color closedColor = Color.white;
        [SerializeField] private Color openedColor = new Color(1f, 0.82f, 0.25f, 1f);

        [Header("Animation")]
        [SerializeField] private float closedScale = 1f;
        [SerializeField] private float openedScale = 1.08f;
        [SerializeField] private float pressedScale = 0.92f;
        [SerializeField] private float animationTime = 0.1f;

        private Coroutine animationRoutine;
        private bool isOpen;

        private void Awake()
        {
            ResolveReferences();

            if (statsDetailPanel != null)
            {
                statsDetailPanel.SetActive(false);
            }

            isOpen = false;
            ApplyButtonVisual(false);
        }

        private void OnEnable()
        {
            ResolveReferences();

            isOpen = statsDetailPanel != null && statsDetailPanel.activeSelf;
            ApplyButtonVisual(isOpen);
        }

        private void OnDisable()
        {
            if (statsDetailPanel != null)
            {
                statsDetailPanel.SetActive(false);
            }

            isOpen = false;
            ApplyButtonVisual(false);
        }

        private void ResolveReferences()
        {
            if (detailButton != null)
            {
                detailButton.transition = Selectable.Transition.None;

                if (buttonImage == null)
                {
                    buttonImage = detailButton.targetGraphic as Image;
                }

                if (buttonRect == null)
                {
                    buttonRect = detailButton.transform as RectTransform;
                }
            }
        }

        public void Toggle()
        {
            if (statsDetailPanel == null)
            {
                return;
            }

            isOpen = !statsDetailPanel.activeSelf;
            statsDetailPanel.SetActive(isOpen);

            PlayButtonAnimation();

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void Close()
        {
            if (statsDetailPanel != null)
            {
                statsDetailPanel.SetActive(false);
            }

            isOpen = false;
            ApplyButtonVisual(false);

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void PlayButtonAnimation()
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
            }

            animationRoutine = StartCoroutine(ButtonAnimationRoutine());
        }

        private IEnumerator ButtonAnimationRoutine()
        {
            if (buttonText != null)
            {
                buttonText.text = isOpen ? openedText : closedText;
            }

            if (buttonImage != null)
            {
                buttonImage.color = isOpen ? openedColor : closedColor;
            }

            if (buttonRect == null)
            {
                yield break;
            }

            float targetScale = isOpen ? openedScale : closedScale;

            Vector3 startScale = buttonRect.localScale;
            Vector3 downScale = Vector3.one * pressedScale;
            Vector3 endScale = Vector3.one * targetScale;

            float timer = 0f;

            while (timer < animationTime)
            {
                timer += Time.unscaledDeltaTime;
                float t = timer / animationTime;

                buttonRect.localScale = Vector3.Lerp(startScale, downScale, t);

                yield return null;
            }

            timer = 0f;

            while (timer < animationTime)
            {
                timer += Time.unscaledDeltaTime;
                float t = timer / animationTime;

                buttonRect.localScale = Vector3.Lerp(downScale, endScale, t);

                yield return null;
            }

            buttonRect.localScale = endScale;
            animationRoutine = null;
        }

        private void ApplyButtonVisual(bool opened)
        {
            if (buttonText != null)
            {
                buttonText.text = opened ? openedText : closedText;
            }

            if (buttonImage != null)
            {
                buttonImage.color = opened ? openedColor : closedColor;
            }

            if (buttonRect != null)
            {
                float targetScale = opened ? openedScale : closedScale;
                buttonRect.localScale = Vector3.one * targetScale;
            }
        }
    }
}