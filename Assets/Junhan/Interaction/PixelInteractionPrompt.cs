using UnityEngine;

namespace Vampire
{
    /// <summary>A shared, non-interactive E key sprite. Owners supply their actual interaction eligibility.</summary>
    [DisallowMultipleComponent]
    public sealed class PixelInteractionPrompt : MonoBehaviour
    {
        private SpriteRenderer keyRenderer;
        private SpriteRenderer[] bodyRenderers;
        private Transform legacyRoot;
        private bool requestedVisible;
        private float shownAt;
        private const float WorldWidth = 0.48f;

        public static void Show(Component owner, bool visible, GameObject oldGuide = null)
        {
            if (oldGuide != null && oldGuide != owner.gameObject) oldGuide.SetActive(false);
            var prompt = owner.GetComponent<PixelInteractionPrompt>();
            if (prompt == null && !visible) return;
            if (prompt == null) prompt = owner.gameObject.AddComponent<PixelInteractionPrompt>();
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
            float top = transform.position.y;
            foreach (var body in bodyRenderers)
            {
                if (body == null || !body.enabled || !body.gameObject.activeInHierarchy ||
                    (legacyRoot != null && body.transform.IsChildOf(legacyRoot)) ||
                    body == keyRenderer ||
                    body.GetComponent<SyringeAugmentVfx>() != null ||
                    body.name.ToLowerInvariant().Contains("shadow")) continue;
                top = Mathf.Max(top, body.bounds.max.y);
            }
            float press = EvaluatePress(Time.unscaledTime - shownAt);
            keyRenderer.transform.position = new Vector3(transform.position.x,
                top + 0.12f + WorldWidth * 0.5f - press * 0.035f, transform.position.z);
            keyRenderer.transform.rotation = Quaternion.identity;
            Vector3 parentScale = transform.lossyScale;
            float scale = WorldWidth / Mathf.Max(0.001f, keyRenderer.sprite.bounds.size.x);
            float verticalScale = scale * (1f - 0.10f * press);
            keyRenderer.transform.localScale = new Vector3(
                Mathf.Abs(parentScale.x) > 0.001f ? scale / parentScale.x : scale,
                Mathf.Abs(parentScale.y) > 0.001f ? verticalScale / parentScale.y : verticalScale, 1f);
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
            if (keyRenderer != null) keyRenderer.enabled = false;
        }
    }
}
