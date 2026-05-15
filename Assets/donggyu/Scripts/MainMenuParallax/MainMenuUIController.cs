using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Donggyu.MainMenuParallax
{
    public class MainMenuUIController : MonoBehaviour
    {
        public CanvasGroup menuGroup;
        public GameObject settingsPanel;
        public GameObject characterSelectPanel;
        public float fadeDuration = 0.8f;
        public float hoverScale = 1.03f;
        public float pressedScale = 0.97f;
        public float scaleLerpSpeed = 12f;

        private Coroutine fadeRoutine;

        private void OnEnable()
        {
            FadeInMenu();
        }

        public void StartGame()
        {
            OpenCharacterSelect();
        }

        public void OpenCharacterSelect()
        {
            if (characterSelectPanel == null)
            {
                Debug.Log("\uAC8C\uC784\uC2DC\uC791 \uBC84\uD2BC \uD074\uB9AD");
                return;
            }

            CloseSettings();
            SetMenuVisible(false);
            characterSelectPanel.SetActive(true);
        }

        public void CloseCharacterSelect()
        {
            if (characterSelectPanel != null)
            {
                characterSelectPanel.SetActive(false);
            }

            SetMenuVisible(true);
        }

        public void SelectCharacter(string characterName)
        {
            Debug.Log("\uCE90\uB9AD\uD130 \uC120\uD0DD: " + characterName);
        }

        public void OpenSettings()
        {
            if (settingsPanel == null)
            {
                Debug.Log("\uC124\uC815 \uBC84\uD2BC \uD074\uB9AD");
                return;
            }

            settingsPanel.SetActive(!settingsPanel.activeSelf);
        }

        public void CloseSettings()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }

        public void ExitGame()
        {
#if UNITY_EDITOR
            Debug.Log("\uC885\uB8CC \uBC84\uD2BC \uD074\uB9AD");
#else
            Application.Quit();
#endif
        }

        public void FadeInMenu()
        {
            if (menuGroup == null)
            {
                return;
            }

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            fadeRoutine = StartCoroutine(FadeInRoutine());
        }

        private void SetMenuVisible(bool visible)
        {
            if (menuGroup == null)
            {
                return;
            }

            menuGroup.alpha = visible ? 1f : 0f;
            menuGroup.interactable = visible;
            menuGroup.blocksRaycasts = visible;
        }

        private IEnumerator FadeInRoutine()
        {
            menuGroup.alpha = 0f;
            menuGroup.interactable = true;
            menuGroup.blocksRaycasts = true;
            menuGroup.transform.localScale = Vector3.one * 0.985f;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeDuration));
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                menuGroup.alpha = eased;
                menuGroup.transform.localScale = Vector3.Lerp(Vector3.one * 0.985f, Vector3.one, eased);
                yield return null;
            }

            menuGroup.alpha = 1f;
            menuGroup.transform.localScale = Vector3.one;
            fadeRoutine = null;
        }
    }

    public class MainMenuButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private MainMenuUIController controller;
        private RectTransform rectTransform;
        private Vector3 targetScale = Vector3.one;

        private void Awake()
        {
            rectTransform = transform as RectTransform;
            controller = GetComponentInParent<MainMenuUIController>();
        }

        private void Update()
        {
            if (rectTransform == null || controller == null)
            {
                return;
            }

            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.unscaledDeltaTime * controller.scaleLerpSpeed);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (controller != null)
            {
                targetScale = Vector3.one * controller.hoverScale;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetScale = Vector3.one;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (controller != null)
            {
                targetScale = Vector3.one * controller.pressedScale;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (controller != null)
            {
                targetScale = Vector3.one * controller.hoverScale;
            }
        }
    }
}
