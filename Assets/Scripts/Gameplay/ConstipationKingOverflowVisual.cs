using UnityEngine;

namespace Vampire
{
    /// <summary>The approved shout -> seat-gap spill -> expanding ground pool presentation.</summary>
    public sealed class ConstipationKingOverflowVisual : MonoBehaviour
    {
        ConstipationKingRuntime owner;
        SpriteRenderer pool;
        readonly LineRenderer[] streams = new LineRenderer[3];
        float radius, progress, opacity = 1, clock;
        public float Progress => progress;
        public float VisibleRadius => radius * Mathf.Sqrt(Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.22f, .86f, progress)));

        public static ConstipationKingOverflowVisual Create(ConstipationKingRuntime boss, float radius)
        {
            var go = new GameObject("Toilet overflow · spill then expanding summoning circle");
            go.transform.SetParent(boss.Effects, true); go.transform.position = boss.transform.position;
            var visual = go.AddComponent<ConstipationKingOverflowVisual>(); visual.owner = boss; visual.radius = radius;
            visual.pool = new GameObject("Expanding brown pool · radius 1.5 body lengths").AddComponent<SpriteRenderer>();
            visual.pool.transform.SetParent(go.transform, false); visual.pool.sprite = ConstipationKingPropArt.Get("OverflowPool");
            visual.pool.sharedMaterial = ConstipationKingPropArt.FloorMaterial; visual.pool.sortingOrder = 476;
            for (int i = 0; i < visual.streams.Length; i++)
            {
                var line = FoodBossEffects.Line(go.transform, new Vector2[16], .25f, new Color(.63f, .37f, .17f, .92f), false, 524);
                line.name = "Liquid from the porcelain seat gap " + i;
                line.sharedMaterial = ConstipationKingPropArt.FloorMaterial;
                line.widthCurve = new AnimationCurve(new Keyframe(0, .18f), new Keyframe(.65f, .24f), new Keyframe(1, .48f));
                line.widthMultiplier = 1;
                visual.streams[i] = line;
            }
            ColonAttackWall.Mask(go, boss.transform.position); visual.Sample(); return visual;
        }
        public void SetProgress(float value) { progress = Mathf.Clamp01(value); Sample(); }
        public void SetOpacity(float value) { opacity = Mathf.Clamp01(value); Sample(); }
        void LateUpdate()
        {
            if (owner == null || owner.Dead) { Destroy(gameObject); return; }
            if (owner.EncounterPaused || owner.Immobilized) return;
            clock += Time.deltaTime; Sample();
        }
        void Sample()
        {
            if (pool == null || owner == null || owner.Visual == null) return;
            float size = VisibleRadius;
            pool.enabled = size > .01f && opacity > 0;
            pool.color = new Color(1, 1, 1, .8f * opacity);
            if (pool.sprite != null) pool.transform.localScale = Vector3.one * size * 2 / (pool.sprite.bounds.size.x * (358f / 384f));
            float strength = Mathf.SmoothStep(0, 1, progress / .22f);
            for (int i = 0; i < streams.Length; i++)
            {
                var line = streams[i]; if (line == null) continue;
                line.enabled = strength > .001f && opacity > 0;
                line.startColor = new Color(.55f, .29f, .12f, .94f * opacity);
                line.endColor = new Color(.76f, .53f, .27f, .94f * opacity);
                line.widthMultiplier = strength;
                float side = i == 0 ? -.55f : i == 1 ? .52f : .15f;
                Vector2 from = owner.Visual.SeatGap + Vector2.right * side * owner.Visual.Facing;
                Vector2 to = (Vector2)transform.position + new Vector2((side + (i == 0 ? -.28f : .24f)) * owner.Visual.Facing, .03f + i * .08f);
                for (int j = 0; j < line.positionCount; j++)
                {
                    float q = j / (float)(line.positionCount - 1);
                    Vector2 p = Vector2.Lerp(from, to, q);
                    p.x += Mathf.Sin(q * 9 - clock * 5 + i * 1.7f) * .055f * q;
                    line.SetPosition(j, p);
                }
            }
        }
    }
}
