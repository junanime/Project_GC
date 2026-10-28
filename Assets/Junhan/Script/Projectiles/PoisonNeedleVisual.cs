using UnityEngine;

namespace Vampire
{
    // Separate augment renderers preserve the monster's own hit tint, animation and pooling state.
    [DisallowMultipleComponent]
    public sealed class PoisonNeedleVisual : MonoBehaviour
    {
        SpriteRenderer target, silhouette;
        readonly LineRenderer[] bubbles = new LineRenderer[3];
        Material tintMaterial, bubbleMaterial;
        Monster monster;
        float phase;
        bool corrosion;

        public void Bind(SpriteRenderer body, bool isCorrosion = false)
        {
            corrosion = isCorrosion;
            target = body;
            monster = body != null ? body.GetComponentInParent<Monster>() : null;
            phase = 0;
            if (silhouette == null)
            {
                tintMaterial = new Material(Resources.Load<Shader>("AugmentVfx/PoisonTint"));
                bubbleMaterial = new Material(Resources.Load<Shader>("AugmentVfx/OpacitySprite"));
                silhouette = new GameObject("Poison body tint").AddComponent<SpriteRenderer>();
                silhouette.transform.SetParent(transform, false);
                silhouette.sharedMaterial = tintMaterial;
                for (int i = 0; i < bubbles.Length; i++)
                {
                    var bubble = new GameObject("Small poison bubble " + i).AddComponent<LineRenderer>();
                    bubble.transform.SetParent(transform, false);
                    bubble.sharedMaterial = bubbleMaterial;
                    bubble.useWorldSpace = true;
                    bubble.loop = true;
                    bubble.positionCount = 12;
                    bubbles[i] = bubble;
                }
            }
            AugmentVfxTag.Mark(gameObject);
            RefreshVisual(0);
        }

        public void RefreshVisual(float deltaTime)
        {
            bool show = target != null && target.enabled && target.sprite != null && SyringeAugmentVfx.IsLiving(target);
            if (silhouette == null) return;
            silhouette.enabled = show;
            foreach (var bubble in bubbles) bubble.enabled = show;
            if (!show) return;
            if (monster == null || !monster.IsFieldRuntimeSuspended) phase += deltaTime;

            silhouette.sprite = target.sprite;
            silhouette.flipX = target.flipX;
            silhouette.flipY = target.flipY;
            silhouette.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);
            var parentScale = transform.lossyScale;
            var bodyScale = target.transform.lossyScale;
            silhouette.transform.localScale = new Vector3(bodyScale.x / parentScale.x, bodyScale.y / parentScale.y, bodyScale.z / parentScale.z);
            silhouette.sortingLayerID = target.sortingLayerID;
            silhouette.sortingOrder = target.sortingOrder + 1;
            float pulse = .5f - .5f * Mathf.Cos(phase * Mathf.PI * 2 / .96f);
            var color = corrosion ? new Color(.72f, .53f, .91f) : new Color(.40f, .86f, .12f);
            color.a = (.10f + .37f * pulse) * target.color.a;
            silhouette.color = color;
            StatusTintCycle.Submit(target, silhouette, corrosion ? StatusTintKind.Corrosion : StatusTintKind.Poison);

            var b = target.bounds;
            float scale = Mathf.Max(.01f, b.size.y / 1.05f);
            for (int i = 0; i < bubbles.Length; i++)
            {
                var bubble = bubbles[i];
                float age = Mathf.Repeat(phase / .9f + i * .33f, 1);
                var center = new Vector3(b.center.x + (i - 1) * .20f * scale, b.max.y + (-.05f + age * .26f) * scale, b.center.z);
                float radius = (.025f + .012f * i) * scale;
                bubble.startWidth = bubble.endWidth = .012f * scale;
                bubble.sortingLayerID = target.sortingLayerID;
                bubble.sortingOrder = target.sortingOrder + 2;
                var bubbleColor = corrosion ? new Color(.80f, .65f, .96f) : new Color(.47f, .90f, .16f);
                bubbleColor.a = Mathf.Sin(age * Mathf.PI) * .8f * target.color.a;
                bubble.startColor = bubble.endColor = bubbleColor;
                for (int n = 0; n < bubble.positionCount; n++)
                {
                    float angle = n * Mathf.PI * 2 / bubble.positionCount;
                    bubble.SetPosition(n, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            }
        }

        void OnDisable()
        {
            StatusTintCycle.Withdraw(target,silhouette,corrosion?StatusTintKind.Corrosion:StatusTintKind.Poison);
            target = null; monster = null; phase = 0;
            if (silhouette != null) silhouette.enabled = false;
            foreach (var bubble in bubbles) if (bubble != null) bubble.enabled = false;
        }
        void OnDestroy()
        {
            if (tintMaterial != null) Destroy(tintMaterial);
            if (bubbleMaterial != null) Destroy(bubbleMaterial);
        }
    }
}
