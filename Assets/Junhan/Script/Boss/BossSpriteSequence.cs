using UnityEngine;

namespace Vampire
{
    public sealed class BossSpriteSequence : MonoBehaviour
    {
        public bool DestroyAfterPlayback;
        public SpriteRenderer Renderer { get; private set; }
        private Sprite[] frames;
        private float started, fps, width, referenceWidth;
        private bool loop;
        public void Play(string name, float worldWidth, float frameRate, bool repeat)
        {
            frames = BossPatternArt.Frames(name); width = worldWidth; fps = Mathf.Max(1, frameRate); loop = repeat;
            started = Time.time; Renderer = GetComponent<SpriteRenderer>();
            referenceWidth = 1;
            // Equal padded canvases preserve expanding explosions and attachment positions.
            if (frames.Length > 0) { referenceWidth = frames[0].rect.width / frames[0].pixelsPerUnit; Renderer.sprite = frames[0]; }
            UpdateScale();
        }
        private void Update()
        {
            if (frames == null || frames.Length == 0) return;
            int index = Mathf.FloorToInt((Time.time - started) * fps);
            if (!loop && index >= frames.Length)
            {
                if (DestroyAfterPlayback) Destroy(gameObject);
                return;
            }
            Renderer.sprite = frames[loop ? index % frames.Length : Mathf.Min(index, frames.Length - 1)];
        }
        private void LateUpdate() { UpdateScale(); }
        private void UpdateScale()
        {
            Vector3 parentScale = transform.parent == null ? Vector3.one : transform.parent.lossyScale;
            transform.localScale = new Vector3(width / referenceWidth / Mathf.Max(.001f, Mathf.Abs(parentScale.x)), width / referenceWidth / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), 1);
        }
    }
}
