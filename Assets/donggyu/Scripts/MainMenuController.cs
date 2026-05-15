using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Donggyu
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        private const string CanvasName = "MainMenuCanvas";
        private const string BackgroundResourceName = "MainMenuBackground";
        private const string UiResourcePrefix = "UI/";
        private const string LevelSceneName = "Level 1";
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        [SerializeField] private string startSceneName = LevelSceneName;
        [SerializeField] private bool rebuildInEditor = true;
        [SerializeField] private bool hideLegacySceneCanvases = true;
        [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.8f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.8f;

        private readonly Color darkPanel = new Color32(8, 13, 22, 224);
        private readonly Color softPanel = new Color32(12, 18, 30, 190);
        private readonly Color gold = new Color32(232, 193, 105, 255);
        private readonly Color dimGold = new Color32(164, 130, 68, 180);
        private readonly Color blue = new Color32(24, 85, 190, 238);
        private readonly Color red = new Color32(219, 54, 38, 255);
        private readonly Color white = new Color32(242, 246, 255, 255);
        private readonly Color muted = new Color32(178, 190, 209, 255);

        private TMP_FontAsset defaultFont;
        private Sprite solidSprite;
        private Sprite circleSprite;
        private Sprite diamondSprite;
        private bool isRebuilding;
        private GameObject settingsPanel;
        private TextMeshProUGUI bgmValueText;
        private TextMeshProUGUI sfxValueText;

        private void OnEnable()
        {
            BuildMenu();
        }

        private void OnValidate()
        {
            if (isRebuilding || !rebuildInEditor || Application.isPlaying)
            {
                return;
            }

#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled)
                {
                    BuildMenu();
                }
            };
#endif
        }

        public void RebuildMainMenu()
        {
            BuildMenu();
        }

        public void StartGame()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("[24 Hours MainMenu] StartGame is ready. Enter Play Mode to load the gameplay scene.");
                return;
            }

            try
            {
                SceneManager.LoadScene(startSceneName);
            }
            catch (Exception exception)
            {
                Debug.LogError("[24 Hours MainMenu] Could not load scene '" + startSceneName + "'. Add it to Build Settings or update startSceneName.\n" + exception);
            }
        }

        public void OpenSettings()
        {
            GameObject panel = FindSettingsPanel();
            if (panel != null)
            {
                panel.SetActive(!panel.activeSelf);
            }
        }

        public void CloseSettings()
        {
            GameObject panel = FindSettingsPanel();
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        public void QuitGame()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("[24 Hours MainMenu] QuitGame is ready. In a build this will close the application.");
                return;
            }

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void BuildMenu()
        {
            if (isRebuilding)
            {
                return;
            }

            isRebuilding = true;

            try
            {
                ResolveSharedAssets();
                EnsureEventSystem();

                if (hideLegacySceneCanvases)
                {
                    HideLegacyCanvases();
                }

                DestroyChild(CanvasName);

                Canvas canvas = CreateCanvas();
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();

                BuildBackground(canvasRect);
            BuildUniversityLogo(canvasRect);
            BuildTitleLogo(canvasRect);
                BuildMainButtonGroup(canvasRect);
                BuildCharacterVisualGroup(canvasRect);
                BuildBottomPanels(canvasRect);
                BuildSettingsPanel(canvasRect);

#if UNITY_EDITOR
                if (!Application.isPlaying && gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
#endif
            }
            finally
            {
                isRebuilding = false;
            }
        }

        private Canvas CreateCanvas()
        {
            RectTransform rect = CreateRect(CanvasName, transform);
            SetStretch(rect, Vector2.zero, Vector2.zero);

            Canvas canvas = rect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = rect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            rect.gameObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private void BuildBackground(RectTransform parent)
        {
            RectTransform background = CreateRect("BackgroundImage", parent);
            SetStretch(background, Vector2.zero, Vector2.zero);

            Texture2D backgroundTexture = LoadBackgroundTexture();
            Image backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.sprite = CreateSpriteFromTexture(backgroundTexture, "DonggyuMainMenuBackgroundSprite");
            backgroundImage.color = Color.white;
            backgroundImage.preserveAspect = true;
            backgroundImage.raycastTarget = false;

            AspectRatioFitter fitter = background.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = backgroundTexture.width / (float)backgroundTexture.height;

            RectTransform darkWash = CreateRect("DarkWash", background);
            SetStretch(darkWash, Vector2.zero, Vector2.zero);
            Image darkImage = darkWash.gameObject.AddComponent<Image>();
            darkImage.sprite = solidSprite;
            darkImage.color = new Color32(4, 7, 12, 82);
            darkImage.raycastTarget = false;

            RectTransform leftVignette = CreateRect("LeftVignette", background);
            SetStretch(leftVignette, Vector2.zero, Vector2.zero);
            Image leftImage = leftVignette.gameObject.AddComponent<Image>();
            leftImage.sprite = solidSprite;
            leftImage.color = new Color32(0, 0, 0, 124);
            leftImage.raycastTarget = false;
        }

        private void BuildUniversityLogo(RectTransform parent)
        {
            RectTransform logo = CreateRect("UniversityLogoGroup", parent);
            SetAnchor(logo, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -30f), new Vector2(360f, 120f), new Vector2(0f, 1f));

            Image image = AddImage(logo, Color.white);
            image.sprite = LoadSprite("UniversityLogo");
            image.preserveAspect = true;
            image.raycastTarget = false;
            logo.gameObject.AddComponent<CanvasGroup>().alpha = 0.9f;
        }

        private void BuildTitleLogo(RectTransform parent)
        {
            RectTransform title = CreateRect("GameTitleLogo", parent);
            SetAnchor(title, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -138f), new Vector2(790f, 264f), new Vector2(0f, 1f));

            Image image = AddImage(title, Color.white);
            image.sprite = LoadSprite("GameTitleLogo");
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private void BuildMainButtonGroup(RectTransform parent)
        {
            RectTransform group = CreateRect("MainButtonGroup", parent);
            SetAnchor(group, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(80f, -72f), new Vector2(440f, 264f), new Vector2(0f, 0.5f));

            CreateMainButton(group, "StartButton", "\uAC8C\uC784 \uC2DC\uC791", "\u2694", true, new Vector2(0f, 95f), StartGame);
            CreateMainButton(group, "SettingsButton", "\uC124\uC815", "\u2699", false, new Vector2(0f, 0f), OpenSettings);
            CreateMainButton(group, "ExitButton", "\uC885\uB8CC", "\u23FB", false, new Vector2(0f, -95f), QuitGame);
        }

        private void BuildCharacterVisualGroup(RectTransform parent)
        {
            RectTransform group = CreateRect("CharacterVisualGroup", parent);
            SetStretch(group, new Vector2(0.31f, 0.15f), new Vector2(0.98f, 0.88f), Vector2.zero, Vector2.zero);
            group.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            CreateCharacterImage(group, "AsiImage", "AsiImage", new Vector2(0.30f, 0.03f), new Vector2(330f, 465f));
            CreateCharacterImage(group, "ShiniImage", "ShiniImage", new Vector2(0.55f, 0.00f), new Vector2(430f, 610f));
            CreateCharacterImage(group, "HyukyiImage", "HyukyiImage", new Vector2(0.80f, 0.05f), new Vector2(390f, 505f));
        }

        private void BuildBottomPanels(RectTransform parent)
        {
            RectTransform group = CreateRect("BottomPanelGroup", parent);
            SetStretch(group, new Vector2(0.04f, 0.045f), new Vector2(0.96f, 0.17f), Vector2.zero, Vector2.zero);

            RectTransform day = CreatePanelFromAsset(group, "DayInfoPanel", "DayInfoPanel_Frame", "DayInfoPanel_Icon", new Vector2(0f, 0f), new Vector2(0.31f, 1f));
            AddTextBlock(day, "TitleText_TMP", "DAY 00", new Vector2(92f, -20f), new Vector2(260f, 26f), 18f, FontStyles.Bold, gold, TextAlignmentOptions.Left);
            AddTextBlock(day, "BodyText_TMP", "\uC0C1\uD0DC \uC548\uB0B4 \uD14D\uC2A4\uD2B8", new Vector2(92f, -58f), new Vector2(330f, 52f), 17f, FontStyles.Normal, muted, TextAlignmentOptions.Left);

            RectTransform record = CreatePanelFromAsset(group, "RunRecordPanel", "RunRecordPanel_Frame", "RunRecordPanel_Icon", new Vector2(0.335f, 0f), new Vector2(0.655f, 1f));
            AddTextBlock(record, "TitleText_TMP", "\uD50C\uB808\uC774 \uAE30\uB85D", new Vector2(88f, -20f), new Vector2(250f, 26f), 18f, FontStyles.Bold, gold, TextAlignmentOptions.Left);
            AddTextBlock(record, "RecordLabelText_1_TMP", "\uCD5C\uACE0 \uC0DD\uC874", new Vector2(82f, -64f), new Vector2(135f, 26f), 15f, FontStyles.Normal, muted, TextAlignmentOptions.Center);
            AddTextBlock(record, "RecordValueText_1_TMP", "--:--", new Vector2(82f, -96f), new Vector2(135f, 26f), 17f, FontStyles.Bold, white, TextAlignmentOptions.Center);
            AddTextBlock(record, "RecordLabelText_2_TMP", "\uCC98\uCE58\uD55C \uBC14\uC774\uB7EC\uC2A4", new Vector2(235f, -64f), new Vector2(150f, 26f), 15f, FontStyles.Normal, muted, TextAlignmentOptions.Center);
            AddTextBlock(record, "RecordValueText_2_TMP", "--", new Vector2(235f, -96f), new Vector2(150f, 26f), 17f, FontStyles.Bold, white, TextAlignmentOptions.Center);
            AddTextBlock(record, "RecordLabelText_3_TMP", "\uD68D\uB4DD \uCF54\uC778", new Vector2(405f, -64f), new Vector2(125f, 26f), 15f, FontStyles.Normal, muted, TextAlignmentOptions.Center);
            AddTextBlock(record, "RecordValueText_3_TMP", "--", new Vector2(405f, -96f), new Vector2(125f, 26f), 17f, FontStyles.Bold, white, TextAlignmentOptions.Center);

            RectTransform tip = CreatePanelFromAsset(group, "TipPanel", "TipPanel_Frame", "TipPanel_Icon", new Vector2(0.68f, 0f), new Vector2(1f, 1f));
            AddTextBlock(tip, "TitleText_TMP", "\uD301", new Vector2(88f, -20f), new Vector2(220f, 26f), 18f, FontStyles.Bold, gold, TextAlignmentOptions.Left);
            AddTextBlock(tip, "BodyText_TMP", "\uD301 \uD14D\uC2A4\uD2B8", new Vector2(88f, -58f), new Vector2(350f, 52f), 17f, FontStyles.Normal, muted, TextAlignmentOptions.Left);
        }

        private void BuildSettingsPanel(RectTransform parent)
        {
            RectTransform panel = CreateRect("SettingsPanel", parent);
            SetAnchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 420f), new Vector2(0.5f, 0.5f));
            Image image = AddImage(panel, new Color32(7, 10, 17, 244));
            image.sprite = solidSprite;
            image.raycastTarget = true;
            AddOutline(panel.gameObject, gold, new Vector2(2f, -2f));
            AddShadow(panel.gameObject, new Color32(0, 0, 0, 210), new Vector2(0f, -10f));

            AddTextBlock(panel, "SettingsTitleText", "\uC124\uC815", new Vector2(42f, -34f), new Vector2(300f, 54f), 40f, FontStyles.Bold, white, TextAlignmentOptions.Left);
            CreateVolumeSlider(panel, "BgmVolumeSlider", "BGM", new Vector2(54f, -130f), bgmVolume, SetBgmVolume, out bgmValueText);
            CreateVolumeSlider(panel, "SfxVolumeSlider", "SFX", new Vector2(54f, -220f), sfxVolume, SetSfxVolume, out sfxValueText);
            CreateSmallButton(panel, "CloseSettingsButton", "\uB2EB\uAE30", new Vector2(0f, 52f), CloseSettings);

            settingsPanel = panel.gameObject;
            settingsPanel.SetActive(false);
        }

        private void CreateMainButton(RectTransform parent, string name, string label, string icon, bool selected, Vector2 anchoredPosition, UnityAction action)
        {
            RectTransform rect = CreateRect(name, parent);
            SetAnchor(rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), anchoredPosition, new Vector2(430f, 74f), new Vector2(0f, 0.5f));

            Image image = AddImage(rect, Color.white);
            image.sprite = LoadSprite(selected ? "MainButton_Selected" : "MainButton_Normal");
            image.preserveAspect = false;
            image.raycastTarget = true;
            AddShadow(rect.gameObject, new Color32(0, 0, 0, 190), new Vector2(0f, -6f));

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = selected ? blue : darkPanel;
            colors.highlightedColor = selected ? new Color32(38, 122, 244, 255) : new Color32(31, 38, 49, 245);
            colors.pressedColor = new Color32(232, 193, 105, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);

            AddTextBlock(rect, "IconText_TMP", icon, new Vector2(34f, -10f), new Vector2(64f, 54f), 32f, FontStyles.Bold, gold, TextAlignmentOptions.Center);
            AddTextBlock(rect, "LabelText_TMP", label, new Vector2(126f, -12f), new Vector2(210f, 50f), 30f, FontStyles.Bold, white, TextAlignmentOptions.Left);
            AddTextBlock(rect, "ArrowText_TMP", ">", new Vector2(364f, -13f), new Vector2(34f, 50f), 34f, FontStyles.Bold, selected ? gold : dimGold, TextAlignmentOptions.Center);
        }

        private RectTransform CreatePanelFromAsset(RectTransform parent, string name, string frameAsset, string iconAsset, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform panel = CreateRect(name, parent);
            panel.anchorMin = anchorMin;
            panel.anchorMax = anchorMax;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panel.pivot = new Vector2(0.5f, 0.5f);

            RectTransform background = CreateRect("BackgroundImage", panel);
            SetStretch(background, Vector2.zero, Vector2.zero);
            Image image = AddImage(background, Color.white);
            image.sprite = LoadSprite(frameAsset);
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;

            RectTransform icon = CreateRect("IconImage", panel);
            SetAnchor(icon, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(46f, 46f), new Vector2(0f, 1f));
            Image iconImage = AddImage(icon, Color.white);
            iconImage.sprite = LoadSprite(iconAsset);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            return panel;
        }

        private void CreateVolumeSlider(RectTransform parent, string name, string label, Vector2 position, float value, UnityAction<float> onChanged, out TextMeshProUGUI valueText)
        {
            AddTextBlock(parent, name + "Label", label, position, new Vector2(90f, 36f), 24f, FontStyles.Bold, gold, TextAlignmentOptions.Left);

            RectTransform sliderRect = CreateRect(name, parent);
            SetAnchor(sliderRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(position.x + 110f, position.y + 2f), new Vector2(330f, 28f), new Vector2(0f, 1f));
            Slider slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;

            RectTransform background = CreateRect("Background", sliderRect);
            SetStretch(background, Vector2.zero, Vector2.zero);
            Image backgroundImage = AddImage(background, new Color32(18, 25, 38, 255));
            backgroundImage.sprite = solidSprite;
            backgroundImage.raycastTarget = true;

            RectTransform fillArea = CreateRect("Fill Area", sliderRect);
            SetStretch(fillArea, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
            RectTransform fill = CreateRect("Fill", fillArea);
            SetStretch(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image fillImage = AddImage(fill, gold);
            fillImage.sprite = solidSprite;
            slider.fillRect = fill;

            RectTransform handle = CreateRect("Handle", sliderRect);
            SetAnchor(handle, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(26f, 34f), new Vector2(0.5f, 0.5f));
            Image handleImage = AddImage(handle, white);
            handleImage.sprite = solidSprite;
            handleImage.raycastTarget = true;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;

            valueText = AddTextBlock(parent, name + "ValueText", Mathf.RoundToInt(value * 100f).ToString("00"), new Vector2(position.x + 462f, position.y), new Vector2(70f, 36f), 22f, FontStyles.Bold, white, TextAlignmentOptions.Right);
            slider.onValueChanged.AddListener(onChanged);
        }

        private void CreateCharacterImage(RectTransform parent, string name, string assetName, Vector2 anchor, Vector2 size)
        {
            RectTransform glow = CreateRect(name + "_Glow", parent);
            SetAnchor(glow, anchor, anchor, new Vector2(0f, 30f), size + new Vector2(80f, 80f), new Vector2(0.5f, 0f));
            Image glowImage = AddImage(glow, new Color32(255, 175, 90, 40));
            glowImage.sprite = circleSprite;
            glowImage.raycastTarget = false;

            RectTransform rect = CreateRect(name, parent);
            SetAnchor(rect, anchor, anchor, Vector2.zero, size, new Vector2(0.5f, 0f));
            Image image = AddImage(rect, Color.white);
            image.sprite = LoadSprite(assetName);
            image.preserveAspect = true;
            image.raycastTarget = false;
            AddShadow(rect.gameObject, new Color32(0, 0, 0, 210), new Vector2(0f, -12f));
        }

        private void CreateSmallButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, UnityAction action)
        {
            RectTransform rect = CreateRect(name, parent);
            SetAnchor(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), anchoredPosition, new Vector2(210f, 58f), new Vector2(0.5f, 0f));
            Image image = AddImage(rect, darkPanel);
            image.sprite = solidSprite;
            image.raycastTarget = true;
            AddOutline(rect.gameObject, dimGold, new Vector2(2f, -2f));
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            TextMeshProUGUI text = AddText(rect, label, 24f, FontStyles.Bold, white, TextAlignmentOptions.Center);
            SetStretch(text.rectTransform, Vector2.zero, Vector2.zero);
        }

        private void CreateMascot(RectTransform parent, string name, Vector2 normalizedPosition, float scale, Color bodyColor, Color jacketColor, MascotWeapon weapon)
        {
            RectTransform root = CreateRect(name, parent);
            root.anchorMin = normalizedPosition;
            root.anchorMax = normalizedPosition;
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(260f * scale, 340f * scale);

            RectTransform glow = CreateRect("BackGlow", root);
            SetAnchor(glow, new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.38f), Vector2.zero, new Vector2(250f * scale, 250f * scale), new Vector2(0.5f, 0.5f));
            Image glowImage = AddImage(glow, new Color(bodyColor.r, bodyColor.g, bodyColor.b, 0.2f));
            glowImage.sprite = circleSprite;
            glowImage.raycastTarget = false;

            RectTransform body = CreateRect("Body", root);
            SetAnchor(body, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(174f * scale, 188f * scale), new Vector2(0.5f, 0.5f));
            Image bodyImage = AddImage(body, bodyColor);
            bodyImage.sprite = circleSprite;
            AddShadow(body.gameObject, new Color32(0, 0, 0, 180), new Vector2(0f, -8f));

            RectTransform crest = CreateRect("Crest", root);
            SetAnchor(crest, new Vector2(0.5f, 0.84f), new Vector2(0.5f, 0.84f), new Vector2(18f * scale, 0f), new Vector2(56f * scale, 76f * scale), new Vector2(0.5f, 0.5f));
            Image crestImage = AddImage(crest, bodyColor);
            crestImage.sprite = circleSprite;

            RectTransform jacket = CreateRect("Jacket", root);
            SetAnchor(jacket, new Vector2(0.5f, 0.34f), new Vector2(0.5f, 0.34f), Vector2.zero, new Vector2(165f * scale, 112f * scale), new Vector2(0.5f, 0.5f));
            AddImage(jacket, jacketColor).sprite = solidSprite;
            AddOutline(jacket.gameObject, new Color32(255, 255, 255, 130), new Vector2(2f, -2f));

            TextMeshProUGUI jacketMark = AddText(jacket, "W", 32f * scale, FontStyles.Bold, white, TextAlignmentOptions.Center);
            SetAnchor(jacketMark.rectTransform, new Vector2(0.6f, 0.42f), new Vector2(0.6f, 0.42f), Vector2.zero, new Vector2(54f * scale, 44f * scale), new Vector2(0.5f, 0.5f));

            CreateFacePart(root, "EyeLeft", new Vector2(0.41f, 0.62f), new Vector2(18f, 22f) * scale, Color.black);
            CreateFacePart(root, "EyeRight", new Vector2(0.59f, 0.62f), new Vector2(18f, 22f) * scale, Color.black);
            CreateFacePart(root, "CheekLeft", new Vector2(0.31f, 0.54f), new Vector2(38f, 24f) * scale, new Color32(255, 205, 205, 210));
            CreateFacePart(root, "CheekRight", new Vector2(0.69f, 0.54f), new Vector2(38f, 24f) * scale, new Color32(255, 205, 205, 210));

            RectTransform beak = CreateRect("Beak", root);
            SetAnchor(beak, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(42f * scale, 30f * scale), new Vector2(0.5f, 0.5f));
            Image beakImage = AddImage(beak, new Color32(255, 190, 37, 255));
            beakImage.sprite = diamondSprite;

            RectTransform leftFoot = CreateRect("LeftFoot", root);
            SetAnchor(leftFoot, new Vector2(0.38f, 0.06f), new Vector2(0.38f, 0.06f), Vector2.zero, new Vector2(50f * scale, 36f * scale), new Vector2(0.5f, 0.5f));
            AddImage(leftFoot, new Color32(38, 38, 42, 255)).sprite = circleSprite;

            RectTransform rightFoot = CreateRect("RightFoot", root);
            SetAnchor(rightFoot, new Vector2(0.62f, 0.06f), new Vector2(0.62f, 0.06f), Vector2.zero, new Vector2(50f * scale, 36f * scale), new Vector2(0.5f, 0.5f));
            AddImage(rightFoot, new Color32(38, 38, 42, 255)).sprite = circleSprite;

            CreateWeapon(root, weapon, scale);
        }

        private void CreateFacePart(RectTransform parent, string name, Vector2 normalizedPosition, Vector2 size, Color color)
        {
            RectTransform part = CreateRect(name, parent);
            SetAnchor(part, normalizedPosition, normalizedPosition, Vector2.zero, size, new Vector2(0.5f, 0.5f));
            Image image = AddImage(part, color);
            image.sprite = circleSprite;
        }

        private void CreateWeapon(RectTransform parent, MascotWeapon weapon, float scale)
        {
            if (weapon == MascotWeapon.Sword)
            {
                RectTransform sword = CreateRect("Sword", parent);
                SetAnchor(sword, new Vector2(0.72f, 0.42f), new Vector2(0.72f, 0.42f), Vector2.zero, new Vector2(34f * scale, 190f * scale), new Vector2(0.5f, 0f));
                sword.localRotation = Quaternion.Euler(0f, 0f, -38f);
                AddImage(sword, new Color32(245, 245, 255, 210)).sprite = solidSprite;
                AddOutline(sword.gameObject, new Color32(255, 120, 96, 240), new Vector2(2f, -2f));
            }
            else if (weapon == MascotWeapon.Staff)
            {
                RectTransform staff = CreateRect("Staff", parent);
                SetAnchor(staff, new Vector2(0.22f, 0.33f), new Vector2(0.22f, 0.33f), Vector2.zero, new Vector2(12f * scale, 220f * scale), new Vector2(0.5f, 0f));
                staff.localRotation = Quaternion.Euler(0f, 0f, 14f);
                AddImage(staff, gold).sprite = solidSprite;

                RectTransform orb = CreateRect("StaffOrb", parent);
                SetAnchor(orb, new Vector2(0.16f, 0.78f), new Vector2(0.16f, 0.78f), Vector2.zero, new Vector2(72f * scale, 72f * scale), new Vector2(0.5f, 0.5f));
                AddImage(orb, new Color32(255, 222, 89, 210)).sprite = circleSprite;
            }
            else
            {
                RectTransform blaster = CreateRect("Blaster", parent);
                SetAnchor(blaster, new Vector2(0.82f, 0.43f), new Vector2(0.82f, 0.43f), Vector2.zero, new Vector2(150f * scale, 52f * scale), new Vector2(0.5f, 0.5f));
                blaster.localRotation = Quaternion.Euler(0f, 0f, -7f);
                AddImage(blaster, new Color32(35, 45, 60, 255)).sprite = solidSprite;
                AddOutline(blaster.gameObject, new Color32(72, 166, 255, 255), new Vector2(2f, -2f));

                RectTransform muzzle = CreateRect("BlasterGlow", blaster);
                SetAnchor(muzzle, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(18f * scale, 0f), new Vector2(60f * scale, 60f * scale), new Vector2(0.5f, 0.5f));
                AddImage(muzzle, new Color32(66, 186, 255, 190)).sprite = circleSprite;
            }
        }

        private TextMeshProUGUI AddTextBlock(RectTransform parent, string name, string text, Vector2 topLeft, Vector2 size, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, size, new Vector2(0f, 1f));
            return AddText(rect, text, fontSize, style, color, alignment);
        }

        private TextMeshProUGUI AddText(RectTransform rect, string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            RectTransform textRect = rect;
            if (rect.GetComponent<Graphic>() != null)
            {
                textRect = CreateRect("Text", rect);
                SetStretch(textRect, Vector2.zero, Vector2.zero);
            }

            TextMeshProUGUI tmp = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = defaultFont;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableAutoSizing = true;
            tmp.enableWordWrapping = true;
            tmp.fontSizeMin = Mathf.Max(10f, fontSize * 0.55f);
            tmp.fontSizeMax = fontSize;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            return tmp;
        }

        private Image AddImage(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = solidSprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void AddOutline(GameObject target, Color color, Vector2 distance)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        private void AddShadow(GameObject target, Color color, Vector2 distance)
        {
            Shadow shadow = target.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private void SetBgmVolume(float value)
        {
            bgmVolume = value;
            if (bgmValueText != null)
            {
                bgmValueText.text = Mathf.RoundToInt(value * 100f).ToString("00");
            }
        }

        private void SetSfxVolume(float value)
        {
            sfxVolume = value;
            if (sfxValueText != null)
            {
                sfxValueText.text = Mathf.RoundToInt(value * 100f).ToString("00");
            }
        }

        private GameObject FindSettingsPanel()
        {
            if (settingsPanel != null)
            {
                return settingsPanel;
            }

            Transform canvas = transform.Find(CanvasName);
            Transform panel = canvas != null ? canvas.Find("SettingsPanel") : null;
            settingsPanel = panel != null ? panel.gameObject : null;
            return settingsPanel;
        }

        private Texture2D LoadBackgroundTexture()
        {
            Texture2D texture = Resources.Load<Texture2D>(BackgroundResourceName);
            return texture != null ? texture : CreateFallbackBackground();
        }

        private Sprite CreateSpriteFromTexture(Texture2D texture, string name)
        {
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private Texture2D CreateFallbackBackground()
        {
            Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float t = (float)x / (float)(texture.width - 1);
                    texture.SetPixel(x, y, Color.Lerp(new Color32(13, 10, 18, 255), new Color32(88, 23, 31, 255), t));
                }
            }

            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        private void ResolveSharedAssets()
        {
            defaultFont = ResolveFont();

            if (solidSprite == null)
            {
                solidSprite = CreateSolidSprite("DonggyuSolidSprite", Color.white);
            }

            if (circleSprite == null)
            {
                circleSprite = CreateCircleSprite("DonggyuCircleSprite");
            }

            if (diamondSprite == null)
            {
                diamondSprite = CreateDiamondSprite("DonggyuDiamondSprite");
            }
        }

        private Sprite LoadSprite(string resourceName)
        {
            Texture2D texture = Resources.Load<Texture2D>(UiResourcePrefix + resourceName);
            if (texture == null)
            {
                Debug.LogWarning("[24 Hours MainMenu] Missing UI resource: " + UiResourcePrefix + resourceName);
                return solidSprite;
            }

            return CreateSpriteFromTexture(texture, "Donggyu_" + resourceName);
        }

        private TMP_FontAsset ResolveFont()
        {
#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("NotoSansKR-Regular SDF t:TMP_FontAsset");
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null)
                {
                    return font;
                }
            }
#endif

            return TMP_Settings.defaultFontAsset;
        }

        private Sprite CreateSolidSprite(string name, Color color)
        {
            Texture2D texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private Sprite CreateCircleSprite(string name)
        {
            Texture2D texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(63.5f, 63.5f);
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(64f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private Sprite CreateDiamondSprite(string name)
        {
            Texture2D texture = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(47.5f, 47.5f);
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float dx = Mathf.Abs(x - center.x) / center.x;
                    float dy = Mathf.Abs(y - center.y) / center.y;
                    float alpha = Mathf.Clamp01(1f - Mathf.Max(0f, dx + dy - 0.88f) * 14f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 96f, 96f), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private void EnsureEventSystem()
        {
            EventSystem eventSystem = FindObjectOfType<EventSystem>();
            if (eventSystem != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemObject.transform.SetSiblingIndex(0);
        }

        private void HideLegacyCanvases()
        {
            Canvas[] canvases = FindObjectsOfType<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || canvas.transform.IsChildOf(transform) || canvas.gameObject.name == CanvasName)
                {
                    continue;
                }

                canvas.gameObject.SetActive(false);
            }
        }

        private RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private void SetAnchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private void SetStretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private void DestroyChild(string childName)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                return;
            }

            DestroyObject(child.gameObject);
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

        private enum MascotWeapon
        {
            Sword,
            Staff,
            Blaster
        }
    }
}
