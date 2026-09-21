using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Sprite pivots/PPU, rather than per-frame fill-to-fit, preserve size and the ground line.
    public sealed class CharacterIdlePreview : MonoBehaviour
    {
        public CharacterBlueprint Blueprint { get; private set; }
        public Image Image { get; private set; }
        float elapsed;
        public void Bind(CharacterBlueprint data)
        {
            Blueprint = data; elapsed = 0;
            if (Image == null)
            {
                var child = new GameObject("Idle sprite", typeof(RectTransform), typeof(Image));
                child.transform.SetParent(transform, false); Image = child.GetComponent<Image>();
                Image.raycastTarget = false; Image.preserveAspect = true;
            }
            Refresh();
        }
        void Update() { elapsed += Time.unscaledDeltaTime; Refresh(); }
        void Refresh()
        {
            if (Blueprint == null || Image == null) return;
            var frames = Blueprint.idleSpriteSequence;
            Sprite sprite = frames != null && frames.Length > 0
                ? frames[(int)(elapsed / Mathf.Max(.01f, Blueprint.idleFrameTime)) % frames.Length]
                : ApothecaryUI.CharacterSprite(Blueprint);
            Image.sprite = sprite; Image.enabled = sprite != null;
            if (sprite == null) return;
            var box = ((RectTransform)transform).rect;
            float scale = Mathf.Min(box.height / .72f, box.width / .95f);
            var rect = Image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
            rect.pivot = sprite.pivot / sprite.rect.size;
            rect.sizeDelta = sprite.rect.size / sprite.pixelsPerUnit * scale;
            rect.anchoredPosition = new Vector2(0, .27f * scale);
        }
    }
}
