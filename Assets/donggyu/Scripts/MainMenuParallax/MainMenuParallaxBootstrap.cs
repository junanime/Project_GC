using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Donggyu.MainMenuParallax
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class MainMenuParallaxBootstrap : MonoBehaviour
    {
        private const float PixelsPerUnit = 100f;
        private const float ReferenceWidth = 1484f;
        private const float ReferenceHeight = 834f;
        private const float ReferenceAspect = ReferenceWidth / ReferenceHeight;
        private const string LayerAssetRoot = "Assets/donggyu/UIAssets/MainMenuParallax/";
        private const string UiAssetRoot = "Assets/donggyu/UIAssets/MainMenuParallax/UI/";
        private const string UiResourceRoot = "MainMenuParallax/UI/";

        [SerializeField] private bool rebuildInEditor = true;
        [SerializeField] private bool fitCameraOnBuild = true;
        [SerializeField] private bool useForegroundFrameLayer;
        [SerializeField] private bool useFrontParticleDuplicate;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

        private bool isBuilding;

        private void OnEnable()
        {
            BuildIfNeeded();
        }

        private void OnValidate()
        {
            if (!rebuildInEditor || isBuilding || Application.isPlaying)
            {
                return;
            }

#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled)
                {
                    Rebuild();
                }
            };
#endif
        }

        public void Rebuild()
        {
            ClearGeneratedObjects();
            BuildIfNeeded();
        }

        private void BuildIfNeeded()
        {
            if (isBuilding)
            {
                return;
            }

            isBuilding = true;

            try
            {
                Camera camera = EnsureCamera();
                Transform root = EnsureParallaxRoot();

                CreateLayer(root, "00_Background", "bg_body_cave.png", 0, 0.02f, 0.01f, 0.08f, 0f, 0.5f, 0f, 0f);
                CreateLayer(root, "01_BlueParticles_Back", "blue_particles_layer.png", 5, 0.08f, 0.03f, 0.12f, 0.02f, 0.5f, 0f, 0f);
                CreateLayer(root, "02_VirusLayer", "virus_layer.png", 10, 0.12f, 0.06f, 0.13f, 0.04f, 0.5f, 1.5f, 0.2f);
                CreateLayer(root, "03_RedBloodCells", "rbc_layer.png", 20, 0.18f, 0.08f, 0.16f, 0.05f, 0.5f, 2f, 0.25f);
                CreateLayer(root, "04_AssiCharacter", "assi_character.png", 30, 0.01f, 0.01f, 0.08f, 0.025f, 0.7f, 0.5f, 0.3f);
                if (useForegroundFrameLayer)
                {
                    CreateLayer(root, "05_ForegroundFrame", "fg_organic_frame.png", 40, 0.25f, 0.12f, 0.11f, 0f, 0.5f, 0f, 0f);
                }
                else
                {
                    CreateEmptyLayer(root, "05_ForegroundFrame");
                }

                if (useFrontParticleDuplicate)
                {
                    CreateLayer(root, "06_BlueParticles_Front", "blue_particles_layer.png", 50, 0.22f, 0.10f, 0.15f, 0.04f, 0.5f, 0f, 0f);
                }
                else
                {
                    CreateEmptyLayer(root, "06_BlueParticles_Front");
                }

                GameObject lightRays = CreateOptionalLightRays(root);
                EnsureCanvas();

                if (fitCameraOnBuild)
                {
                    FitCamera(camera);
                }

#if UNITY_EDITOR
                if (!Application.isPlaying && gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
#endif
            }
            finally
            {
                isBuilding = false;
            }
        }

        private Camera EnsureCamera()
        {
            GameObject cameraObject = GameObject.Find("Main Camera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
            }

            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera == null)
            {
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;

            MainMenuCameraDrift drift = cameraObject.GetComponent<MainMenuCameraDrift>();
            if (drift == null)
            {
                drift = cameraObject.AddComponent<MainMenuCameraDrift>();
            }

            drift.moveX = 0.12f;
            drift.moveY = 0.05f;
            drift.zoomAmount = 0.15f;
            drift.speed = 0.12f;

            return camera;
        }

        private Transform EnsureParallaxRoot()
        {
            GameObject root = GameObject.Find("ParallaxRoot");
            if (root == null)
            {
                root = new GameObject("ParallaxRoot");
            }

            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            return root.transform;
        }

        private GameObject CreateLayer(Transform root, string objectName, string assetName, int sortingOrder, float moveX, float moveY, float moveSpeed, float floatY, float floatSpeed, float rotateAmount, float rotateSpeed)
        {
            Transform existing = root.Find(objectName);
            if (existing != null)
            {
                DestroyObject(existing.gameObject);
            }

            GameObject layer = new GameObject(objectName);
            layer.transform.SetParent(root, false);
            layer.transform.localPosition = Vector3.zero;
            layer.transform.localRotation = Quaternion.identity;
            layer.transform.localScale = Vector3.one;

            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadLayerSprite(assetName);
            renderer.sortingOrder = sortingOrder;
            renderer.color = Color.white;

            MainMenuParallaxLayer parallax = layer.AddComponent<MainMenuParallaxLayer>();
            parallax.moveAmountX = moveX;
            parallax.moveAmountY = moveY;
            parallax.moveSpeed = moveSpeed;
            parallax.floatAmountY = floatY;
            parallax.floatSpeed = floatSpeed;
            parallax.rotateAmount = rotateAmount;
            parallax.rotateSpeed = rotateSpeed;

            return layer;
        }

        private GameObject CreateEmptyLayer(Transform root, string objectName)
        {
            Transform existing = root.Find(objectName);
            if (existing != null)
            {
                DestroyObject(existing.gameObject);
            }

            GameObject layer = new GameObject(objectName);
            layer.transform.SetParent(root, false);
            layer.transform.localPosition = Vector3.zero;
            layer.transform.localRotation = Quaternion.identity;
            layer.transform.localScale = Vector3.one;
            return layer;
        }

        private GameObject CreateOptionalLightRays(Transform root)
        {
            Transform existing = root.Find("07_LightRays");
            if (existing != null)
            {
                DestroyObject(existing.gameObject);
            }

            GameObject layer = new GameObject("07_LightRays");
            layer.transform.SetParent(root, false);
            layer.transform.localPosition = Vector3.zero;
            layer.transform.localRotation = Quaternion.identity;
            layer.transform.localScale = Vector3.one;

            Sprite sprite = LoadLayerSprite("light_rays_layer.png");
            if (sprite == null)
            {
                return layer;
            }

            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 60;
            renderer.color = new Color(1f, 1f, 1f, 0.6f);

            MainMenuParallaxLayer parallax = layer.AddComponent<MainMenuParallaxLayer>();
            parallax.moveAmountX = 0.03f;
            parallax.moveAmountY = 0.02f;
            parallax.moveSpeed = 0.06f;
            parallax.floatAmountY = 0f;
            parallax.floatSpeed = 0.5f;
            parallax.rotateAmount = 0f;
            parallax.rotateSpeed = 0f;

            LightRayPulse pulse = layer.AddComponent<LightRayPulse>();
            pulse.minAlpha = 0.45f;
            pulse.maxAlpha = 0.75f;
            pulse.speed = 0.4f;
            return layer;
        }

        private Canvas EnsureCanvas()
        {
            GameObject canvasObject = GameObject.Find("Canvas_UI");
            if (canvasObject == null)
            {
                canvasObject = new GameObject("Canvas_UI");
            }

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            {
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            BuildMenuUi(canvasObject.transform);
            return canvas;
        }

        private void BuildMenuUi(Transform canvasTransform)
        {
            Transform existingMenu = canvasTransform.Find("MenuGroup");
            if (existingMenu != null)
            {
                DestroyObject(existingMenu.gameObject);
            }

            Transform existingSettings = canvasTransform.Find("SettingsPanel");
            if (existingSettings != null)
            {
                DestroyObject(existingSettings.gameObject);
            }

            Transform existingCharacterSelect = canvasTransform.Find("CharacterSelectPanel");
            if (existingCharacterSelect != null)
            {
                DestroyObject(existingCharacterSelect.gameObject);
            }

            RectTransform menuGroup = CreateUiRect("MenuGroup", canvasTransform);
            menuGroup.anchorMin = new Vector2(0f, 0.5f);
            menuGroup.anchorMax = new Vector2(0f, 0.5f);
            menuGroup.pivot = new Vector2(0f, 0.5f);
            menuGroup.anchoredPosition = new Vector2(112f, -58f);
            menuGroup.sizeDelta = new Vector2(660f, 620f);

            CanvasGroup canvasGroup = menuGroup.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = Application.isPlaying ? 0f : 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            MainMenuUIController controller = menuGroup.gameObject.AddComponent<MainMenuUIController>();
            controller.menuGroup = canvasGroup;
            controller.fadeDuration = 0.8f;
            controller.hoverScale = 1.03f;
            controller.pressedScale = 0.97f;

            RectTransform buttonBackdrop = CreateUiRect("ButtonFocusBackdrop", menuGroup);
            buttonBackdrop.anchorMin = new Vector2(0.5f, 1f);
            buttonBackdrop.anchorMax = new Vector2(0.5f, 1f);
            buttonBackdrop.pivot = new Vector2(0.5f, 0.5f);
            buttonBackdrop.anchoredPosition = new Vector2(0f, -373f);
            buttonBackdrop.sizeDelta = new Vector2(570f, 420f);
            Image backdropImage = buttonBackdrop.gameObject.AddComponent<Image>();
            backdropImage.color = new Color32(0, 0, 0, 165);
            backdropImage.raycastTarget = false;
            Shadow backdropShadow = buttonBackdrop.gameObject.AddComponent<Shadow>();
            backdropShadow.effectColor = new Color32(0, 0, 0, 210);
            backdropShadow.effectDistance = new Vector2(16f, -16f);
            Outline backdropOutline = buttonBackdrop.gameObject.AddComponent<Outline>();
            backdropOutline.effectColor = new Color32(80, 15, 18, 115);
            backdropOutline.effectDistance = new Vector2(2f, -2f);

            RectTransform title = CreateUiImage(menuGroup, "TitleImage", "title_24hours", new Vector2(0.5f, 1f), new Vector2(0f, -12f), 660f);
            title.pivot = new Vector2(0.5f, 1f);

            RectTransform startButton = CreateImageButton(menuGroup, "StartButton", "btn_start", new Vector2(0.5f, 1f), new Vector2(0f, -236f), 465f, controller.StartGame);
            RectTransform settingsButton = CreateImageButton(menuGroup, "SettingsButton", "btn_settings", new Vector2(0.5f, 1f), new Vector2(0f, -372f), 465f, controller.OpenSettings);
            RectTransform exitButton = CreateImageButton(menuGroup, "ExitButton", "btn_exit", new Vector2(0.5f, 1f), new Vector2(0f, -508f), 465f, controller.ExitGame);

            RectTransform settingsPanel = CreateSettingsPanel(canvasTransform, controller);
            controller.settingsPanel = settingsPanel.gameObject;
            settingsPanel.gameObject.SetActive(false);

            RectTransform characterSelectPanel = CreateCharacterSelectPanel(canvasTransform, controller);
            controller.characterSelectPanel = characterSelectPanel.gameObject;
            characterSelectPanel.gameObject.SetActive(false);

            controller.FadeInMenu();
        }

        private void FitCamera(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            float imageWorldHeight = ReferenceHeight / PixelsPerUnit;
            float imageWorldWidth = ReferenceWidth / PixelsPerUnit;
            float screenAspect = (float)Screen.width / Mathf.Max(1f, Screen.height);
            if (!Application.isPlaying)
            {
                screenAspect = ReferenceAspect;
            }

            float sizeByHeight = imageWorldHeight * 0.5f;
            float sizeByWidth = imageWorldWidth / Mathf.Max(0.01f, screenAspect) * 0.5f;
            camera.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth);
        }

        private RectTransform CreateUiRect(string objectName, Transform parent)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private RectTransform CreateUiImage(RectTransform parent, string objectName, string assetName, Vector2 anchor, Vector2 anchoredPosition, float targetWidth)
        {
            RectTransform rect = CreateUiRect(objectName, parent);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;

            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = LoadUiSprite(assetName);
            image.preserveAspect = true;
            image.raycastTarget = false;

            ApplyAspectSize(rect, image.sprite, targetWidth);
            return rect;
        }

        private RectTransform CreateImageButton(RectTransform parent, string objectName, string assetName, Vector2 anchor, Vector2 anchoredPosition, float targetWidth, UnityAction action)
        {
            RectTransform rect = CreateUiImage(parent, objectName, assetName, anchor, anchoredPosition, targetWidth);

            Image image = rect.GetComponent<Image>();
            image.raycastTarget = true;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.96f);
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);

            rect.gameObject.AddComponent<MainMenuButtonFeedback>();
            return rect;
        }

        private RectTransform CreateSettingsPanel(Transform canvasTransform, MainMenuUIController controller)
        {
            RectTransform panel = CreateUiRect("SettingsPanel", canvasTransform);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(520f, 280f);

            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color32(8, 12, 20, 224);
            background.raycastTarget = true;

            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(122, 209, 245, 165);
            outline.effectDistance = new Vector2(2f, -2f);

            TextMeshProUGUI title = CreateTmpText(panel, "TitleText", "\uC124\uC815", 38, TextAlignmentOptions.Center, new Vector2(0f, 72f), new Vector2(440f, 60f));
            title.color = new Color32(244, 247, 252, 255);

            TextMeshProUGUI body = CreateTmpText(panel, "BodyText", "\uC138\uBD80 \uC635\uC158\uC740 \uB098\uC911\uC5D0 \uCD94\uAC00\uD558\uBA74 \uB429\uB2C8\uB2E4.", 20, TextAlignmentOptions.Center, new Vector2(0f, 10f), new Vector2(440f, 44f));
            body.color = new Color32(190, 205, 218, 255);

            RectTransform closeButton = CreateUiRect("CloseButton", panel);
            closeButton.anchorMin = new Vector2(0.5f, 0f);
            closeButton.anchorMax = new Vector2(0.5f, 0f);
            closeButton.pivot = new Vector2(0.5f, 0f);
            closeButton.anchoredPosition = new Vector2(0f, 30f);
            closeButton.sizeDelta = new Vector2(180f, 48f);

            Image closeImage = closeButton.gameObject.AddComponent<Image>();
            closeImage.color = new Color32(26, 54, 74, 235);
            closeImage.raycastTarget = true;

            Button button = closeButton.gameObject.AddComponent<Button>();
            button.targetGraphic = closeImage;
            button.onClick.AddListener(controller.CloseSettings);

            TextMeshProUGUI closeText = CreateTmpText(closeButton, "Label", "\uB2EB\uAE30", 22, TextAlignmentOptions.Center, Vector2.zero, new Vector2(180f, 48f));
            closeText.color = Color.white;
            return panel;
        }

        private RectTransform CreateCharacterSelectPanel(Transform canvasTransform, MainMenuUIController controller)
        {
            RectTransform panel = CreateUiRect("CharacterSelectPanel", canvasTransform);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = new Vector2(170f, 0f);
            panel.sizeDelta = new Vector2(760f, 520f);

            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color32(4, 6, 10, 230);
            background.raycastTarget = true;

            Shadow shadow = panel.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(0, 0, 0, 210);
            shadow.effectDistance = new Vector2(18f, -18f);

            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(217, 57, 47, 160);
            outline.effectDistance = new Vector2(2f, -2f);

            TextMeshProUGUI title = CreateTmpText(panel, "TitleText", "\uCE90\uB9AD\uD130 \uC120\uD0DD", 44, TextAlignmentOptions.Center, new Vector2(0f, 188f), new Vector2(620f, 64f));
            title.color = new Color32(255, 242, 219, 255);

            TextMeshProUGUI body = CreateTmpText(panel, "BodyText", "\uCD9C\uC804\uD560 \uB9C8\uC2A4\uCF54\uD2B8\uB97C \uC120\uD0DD\uD574\uC8FC\uC138\uC694.", 22, TextAlignmentOptions.Center, new Vector2(0f, 135f), new Vector2(620f, 42f));
            body.color = new Color32(205, 220, 232, 255);

            RectTransform choiceGroup = CreateUiRect("CharacterChoiceGroup", panel);
            choiceGroup.anchorMin = new Vector2(0.5f, 0.5f);
            choiceGroup.anchorMax = new Vector2(0.5f, 0.5f);
            choiceGroup.pivot = new Vector2(0.5f, 0.5f);
            choiceGroup.anchoredPosition = new Vector2(0f, -25f);
            choiceGroup.sizeDelta = new Vector2(660f, 190f);

            CreateTextButton(choiceGroup, "ShiniButton", "\uC2E0\uC774", new Vector2(-220f, 0f), new Vector2(190f, 148f), () => controller.SelectCharacter("\uC2E0\uC774"));
            CreateTextButton(choiceGroup, "AssiButton", "\uC544\uC2DC", new Vector2(0f, 0f), new Vector2(190f, 148f), () => controller.SelectCharacter("\uC544\uC2DC"));
            CreateTextButton(choiceGroup, "HyukyiButton", "\uD601\uC774", new Vector2(220f, 0f), new Vector2(190f, 148f), () => controller.SelectCharacter("\uD601\uC774"));

            RectTransform closeButton = CreateTextButton(panel, "CloseButton", "\uB2EB\uAE30", new Vector2(0f, -206f), new Vector2(180f, 48f), controller.CloseCharacterSelect);
            Image closeImage = closeButton.GetComponent<Image>();
            closeImage.color = new Color32(24, 34, 44, 238);

            return panel;
        }

        private RectTransform CreateTextButton(RectTransform parent, string objectName, string label, Vector2 anchoredPosition, Vector2 size, UnityAction action)
        {
            RectTransform buttonRect = CreateUiRect(objectName, parent);
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = anchoredPosition;
            buttonRect.sizeDelta = size;

            Image image = buttonRect.gameObject.AddComponent<Image>();
            image.color = new Color32(14, 18, 26, 240);
            image.raycastTarget = true;

            Outline outline = buttonRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(210, 169, 92, 175);
            outline.effectDistance = new Vector2(2f, -2f);

            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 0.72f, 1f);
            colors.pressedColor = new Color(0.9f, 0.8f, 0.62f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);

            TextMeshProUGUI text = CreateTmpText(buttonRect, "Label", label, 27, TextAlignmentOptions.Center, Vector2.zero, size);
            text.color = new Color32(255, 236, 194, 255);

            return buttonRect;
        }

        private TextMeshProUGUI CreateTmpText(RectTransform parent, string objectName, string textValue, int fontSize, TextAlignmentOptions alignment, Vector2 anchoredPosition, Vector2 size)
        {
            RectTransform rect = CreateUiRect(objectName, parent);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = textValue;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12;
            text.fontSizeMax = fontSize;
            text.raycastTarget = false;
            return text;
        }

        private void ApplyAspectSize(RectTransform rect, Sprite sprite, float targetWidth)
        {
            if (sprite == null)
            {
                rect.sizeDelta = new Vector2(targetWidth, targetWidth * 0.3f);
                return;
            }

            float ratio = sprite.rect.height / Mathf.Max(1f, sprite.rect.width);
            rect.sizeDelta = new Vector2(targetWidth, targetWidth * ratio);
        }

        private Sprite LoadLayerSprite(string assetName)
        {
#if UNITY_EDITOR
            string path = LayerAssetRoot + assetName;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null)
            {
                return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            }
#endif

            return null;
        }

        private Sprite LoadUiSprite(string assetName)
        {
            Texture2D resourceTexture = Resources.Load<Texture2D>(UiResourceRoot + assetName);
            if (resourceTexture != null)
            {
                return Sprite.Create(resourceTexture, new Rect(0f, 0f, resourceTexture.width, resourceTexture.height), new Vector2(0.5f, 0.5f), 100f);
            }

#if UNITY_EDITOR
            string path = UiAssetRoot + assetName + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null)
            {
                return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }
#endif

            Debug.LogWarning("[MainMenuParallax] Missing UI sprite: " + assetName);
            return null;
        }

        private void ClearGeneratedObjects()
        {
            DestroyNamedObject("ParallaxRoot");
            DestroyNamedObject("Canvas_UI");
            DestroyNamedObject("Main Camera");
        }

        private void DestroyNamedObject(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            if (target != null)
            {
                DestroyObject(target);
            }
        }

        private void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
