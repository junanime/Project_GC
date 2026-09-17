using UnityEngine;

namespace Vampire
{
    // Visual-only recoil: never moves the actor, collider, laser or projectile origin.
    [DefaultExecutionOrder(500)]
    public sealed class SniperRecoilVisual : MonoBehaviour
    {
        private SpriteRenderer target;
        private Sprite[] frames;
        private Sprite idle;
        private float frameTime, elapsed;
        private bool playing, facingLeft;
        public bool IsPlaying => playing;
        public int FrameIndex => frames == null || frames.Length == 0 ? 0 :
            Mathf.Min(2 + Mathf.FloorToInt(elapsed / frameTime), frames.Length - 1);

        public void Configure(SpriteRenderer renderer, SniperMonsterBlueprint blueprint)
        {
            target = renderer;
            frames = blueprint.fireSprites;
            frameTime = Mathf.Max(0.01f, blueprint.fireFrameTime);
            idle = blueprint.walkSpriteSequence != null && blueprint.walkSpriteSequence.Length > 0
                ? blueprint.walkSpriteSequence[0] : renderer.sprite;
            ResetVisual();
        }

        public void Fire(Vector2 direction)
        {
            if (target == null || frames == null || frames.Length < 3) return;
            elapsed = 0f;
            facingLeft = direction.x < 0f;
            playing = true;
            target.flipX = facingLeft;
            target.sprite = frames[2]; // Real projectile has already launched; skip anticipation.
        }

        private void LateUpdate()
        {
            if (!playing || target == null) return;
            target.flipX = facingLeft;
            target.sprite = frames[FrameIndex];
            elapsed += Time.deltaTime;
            if (elapsed >= (frames.Length - 2) * frameTime) ResetVisual();
        }

        public void ResetVisual()
        {
            playing = false;
            elapsed = 0f;
            if (target != null && idle != null) target.sprite = idle;
        }
        private void OnDisable() => ResetVisual();
    }
}
