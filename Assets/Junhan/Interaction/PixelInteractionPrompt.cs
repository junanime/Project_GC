using UnityEngine;

namespace Vampire
{
    /// <summary>A shared, non-interactive E key sprite. Owners supply their actual interaction eligibility.</summary>
    [DisallowMultipleComponent]
    public sealed class PixelInteractionPrompt : MonoBehaviour
    {
        private SpriteRenderer keyRenderer;
        private Canvas boundKeyCanvas;
        private TMPro.TextMeshProUGUI boundKeyText;
        private SpriteRenderer[] bodyRenderers;
        private Transform legacyRoot;
        private bool requestedVisible;
        private Component focusOwner;
        private bool wasFocused;
        private LineRenderer activeBorder;
        private Material borderMaterial;
        private float shownAt;
        private const float WorldWidth = 0.48f;
        // Half an Ashi body width from the visible object top to keycap center.
        public const float ObjectGap = 0.41f;

        public static void Show(Component owner, bool visible, GameObject oldGuide = null, Component interactionOwner = null)
        {
            if (oldGuide != null && oldGuide != owner.gameObject) oldGuide.SetActive(false);
            var prompt = owner.GetComponent<PixelInteractionPrompt>();
            if (prompt == null && !visible) return;
            if (prompt == null) prompt = owner.gameObject.AddComponent<PixelInteractionPrompt>();
            prompt.focusOwner = interactionOwner != null ? interactionOwner : owner;
            if (oldGuide != null) prompt.legacyRoot = oldGuide.transform;
            if (visible && !prompt.requestedVisible) prompt.shownAt = Time.unscaledTime;
            prompt.requestedVisible = visible;
            prompt.Refresh();
        }

        private void Refresh()
        {
            bool visible = requestedVisible && isActiveAndEnabled && Time.timeScale > 0f;
            if (!visible)
            {
                if (keyRenderer != null) keyRenderer.enabled = false;
                if (boundKeyCanvas != null) boundKeyCanvas.gameObject.SetActive(false);
                if (activeBorder != null) activeBorder.enabled = false;
                wasFocused = false;
                return;
            }
            if (keyRenderer == null)
            {
                var sprite = Resources.Load<Sprite>("Interaction/KeycapE_Pixel");
                if (sprite == null)
                {
                    Debug.LogError("Missing interaction sprite: Interaction/KeycapE_Pixel", this);
                    return;
                }
                bodyRenderers = GetComponentsInChildren<SpriteRenderer>(true);
                var key = new GameObject("Pixel E Interaction Prompt");
                key.transform.SetParent(transform, false);
                keyRenderer = key.AddComponent<SpriteRenderer>();
                keyRenderer.sprite = sprite;
                keyRenderer.sortingLayerName = "Monster Full";
                keyRenderer.sortingOrder = 32760;
            }
            keyRenderer.enabled = true;
            UpdateBoundKey();
            bool focused = InteractionFocus.IsFocused(focusOwner);
            if (focused && !wasFocused) shownAt = Time.unscaledTime;
            wasFocused = focused;
            keyRenderer.color = focused ? Color.white : new Color(.65f, .65f, .65f, .7f);
            if (activeBorder == null) CreateBorder();
            activeBorder.enabled = focused;
            float top = float.NegativeInfinity;
            float centerX = transform.position.x;
            foreach (var body in bodyRenderers)
            {
                if (body == null || body.sprite == null || !body.enabled || !body.gameObject.activeInHierarchy ||
                    (legacyRoot != null && body.transform.IsChildOf(legacyRoot)) ||
                    body == keyRenderer ||
                    body.GetComponent<SyringeAugmentVfx>() != null ||
                    body.GetComponentInParent<PixelInteractionPrompt>() != this ||
                    body.name.ToLowerInvariant().Contains("shadow")) continue;
                var visibleBounds = PromptSpriteGeometry.WorldBounds(body);
                if (visibleBounds.max.y > top) { top = visibleBounds.max.y; centerX = visibleBounds.center.x; }
            }
            if (float.IsNegativeInfinity(top)) top = transform.position.y;
            float press = focused ? EvaluatePress(Time.unscaledTime - shownAt) : 0f;
            keyRenderer.transform.position = new Vector3(centerX,
                top + ObjectGap - press * 0.035f, transform.position.z);
            keyRenderer.transform.rotation = Quaternion.identity;
            Vector3 parentScale = transform.lossyScale;
            float scale = WorldWidth / Mathf.Max(0.001f, keyRenderer.sprite.bounds.size.x);
            float verticalScale = scale * (1f - 0.10f * press);
            keyRenderer.transform.localScale = new Vector3(
                Mathf.Abs(parentScale.x) > 0.001f ? scale / parentScale.x : scale,
                Mathf.Abs(parentScale.y) > 0.001f ? verticalScale / parentScale.y : verticalScale, 1f);
        }

        void UpdateBoundKey()
        {
            var binding=GamePreferences.Current.keyboard;
            var key=binding.Get(GameKeyAction.Interact);
            if(key==KeyCode.None)key=binding.Get(GameKeyAction.Interact,1);
            bool custom=key!=KeyCode.E;
            if(!custom){if(boundKeyCanvas!=null)boundKeyCanvas.gameObject.SetActive(false);return;}
            if(boundKeyCanvas==null)
            {
                var go=new GameObject("Bound interaction key",typeof(RectTransform),typeof(Canvas));
                go.transform.SetParent(keyRenderer.transform,false);
                var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(100,90);
                rect.localScale=Vector3.one*(keyRenderer.sprite.bounds.size.x/100f);
                boundKeyCanvas=go.GetComponent<Canvas>();boundKeyCanvas.renderMode=RenderMode.WorldSpace;
                boundKeyCanvas.overrideSorting=true;boundKeyCanvas.sortingLayerID=keyRenderer.sortingLayerID;boundKeyCanvas.sortingOrder=32761;
                var face=new GameObject("Key face",typeof(RectTransform),typeof(IdentityShape));face.transform.SetParent(go.transform,false);
                var fr=(RectTransform)face.transform;fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.offsetMin=fr.offsetMax=Vector2.zero;
                var shape=face.GetComponent<IdentityShape>();shape.frame=true;shape.radius=12;shape.color=new Color(.17f,.12f,.08f);shape.insetColor=new Color(1,.93f,.69f);shape.raycastTarget=false;
                var text=new GameObject("Assigned key",typeof(RectTransform),typeof(TMPro.TextMeshProUGUI));text.transform.SetParent(go.transform,false);
                boundKeyText=text.GetComponent<TMPro.TextMeshProUGUI>();
                var tr=boundKeyText.rectTransform;tr.anchorMin=new Vector2(.06f,.10f);tr.anchorMax=new Vector2(.94f,.90f);tr.offsetMin=tr.offsetMax=Vector2.zero;
                if(ApothecaryUI.Instance?.Config?.font!=null)boundKeyText.font=ApothecaryUI.Instance.Config.font;
                boundKeyText.alignment=TMPro.TextAlignmentOptions.Center;boundKeyText.color=new Color(.13f,.10f,.08f);boundKeyText.enableAutoSizing=true;boundKeyText.fontSizeMax=48;boundKeyText.fontSizeMin=13;boundKeyText.raycastTarget=false;
            }
            boundKeyCanvas.gameObject.SetActive(true);keyRenderer.enabled=false;
            boundKeyText.text=KeyboardBindingValues.KeyLabel(key);
        }

        private void CreateBorder()
        {
            var go = new GameObject("Active E golden border");
            go.transform.SetParent(keyRenderer.transform, false);
            activeBorder = go.AddComponent<LineRenderer>();
            activeBorder.useWorldSpace = false; activeBorder.loop = true;
            activeBorder.sortingLayerID = keyRenderer.sortingLayerID;
            activeBorder.sortingOrder = keyRenderer.sortingOrder + 1;
            borderMaterial = new Material(Shader.Find("Sprites/Default"));
            activeBorder.sharedMaterial = borderMaterial;
            activeBorder.startColor = activeBorder.endColor = new Color(.78f, .52f, .035f, 1f);
            Vector2 ext = keyRenderer.sprite.bounds.extents * .90f;
            float bevel = Mathf.Min(ext.x, ext.y) * .18f;
            // LineRenderer width is measured in world units, even with local-space vertices.
            activeBorder.startWidth = activeBorder.endWidth = WorldWidth * .025f;
            activeBorder.positionCount = 8;
            activeBorder.SetPositions(new[] {
                new Vector3(-ext.x + bevel, ext.y), new Vector3(ext.x - bevel, ext.y),
                new Vector3(ext.x, ext.y - bevel), new Vector3(ext.x, -ext.y + bevel),
                new Vector3(ext.x - bevel, -ext.y), new Vector3(-ext.x + bevel, -ext.y),
                new Vector3(-ext.x, -ext.y + bevel), new Vector3(-ext.x, ext.y - bevel) });
        }

        // Rest, short downstroke, brief hold, then a soft release. No changes to input timing.
        public static float EvaluatePress(float elapsed)
        {
            float phase = Mathf.Repeat(Mathf.Max(0f, elapsed), 1.35f);
            if (phase < 0.70f) return 0f;
            if (phase < 0.82f) return Mathf.SmoothStep(0f, 1f, (phase - 0.70f) / 0.12f);
            if (phase < 0.94f) return 1f;
            if (phase < 1.14f) return 1f - Mathf.SmoothStep(0f, 1f, (phase - 0.94f) / 0.20f);
            return 0f;
        }

        private void LateUpdate() { Refresh(); }
        private void OnDisable()
        {
            requestedVisible = false;
            if (boundKeyCanvas != null) boundKeyCanvas.gameObject.SetActive(false);
            if (keyRenderer != null) keyRenderer.enabled = false;
            if (activeBorder != null) activeBorder.enabled = false;
        }
        private void OnDestroy() { if (borderMaterial != null) Destroy(borderMaterial); }
    }
}
