using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Scene-owned, bounded sprite pool: no per-puff Instantiate/Destroy, and no
    // parenting to moving/pool-recycled projectiles or characters.
    public sealed class MotionVfx : MonoBehaviour
    {
        public const float NeedleSize = .48f * (2f / 3f);
        public const float NeedleLifetime = .28f;
        const int Capacity = 128;
        static MotionVfx instance;
        static Sprite needle;
        static readonly Dictionary<string, Sprite> trails = new Dictionary<string, Sprite>();
        readonly List<Puff> pool = new List<Puff>();
        class Puff
        {
            public SpriteRenderer renderer;
            public Vector3 origin, drift;
            public float started, lifetime, size, rotation;
            public bool active, end;
        }

        public static Sprite CharacterSprite(CharacterBlueprint blueprint)
        {
            if (trails.Count == 0)
                foreach (var sprite in Resources.LoadAll<Sprite>("MotionVfx/CharacterTrails")) trails[sprite.name] = sprite;
            trails.TryGetValue(OctoberArt.CharacterKey(blueprint), out var result);
            return result;
        }

        public static void PlayTrail(Sprite sprite, Vector3 position, Vector2 heading, SpriteRenderer reference, int sequence)
        {
            var puff = Emit(sprite, position, reference, false);
            if (puff == null) return;
            puff.drift = new Vector3(-heading.x * .08f, .1f - heading.y * .08f, 0);
            puff.rotation = Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg + (sequence * 17 % 61 - 30);
            Apply(puff, 0);
        }

        public static void PlayNeedleEnd(Vector3 position, SpriteRenderer reference)
        {
            if (needle == null) needle = Resources.Load<Sprite>("MotionVfx/NeedleEnd");
            Emit(needle, position, reference, true);
        }

        static Puff Emit(Sprite sprite, Vector3 position, SpriteRenderer reference, bool end)
        {
            if (sprite == null) return null;
            if (instance == null) instance = new GameObject("Character trails and needle glints").AddComponent<MotionVfx>();
            Puff puff = null;
            foreach (var candidate in instance.pool)
                if (!candidate.active) { puff = candidate; break; }
            if (puff == null && instance.pool.Count < Capacity)
            {
                var go = new GameObject("Motion VFX puff");
                go.transform.SetParent(instance.transform, false);
                puff = new Puff { renderer = go.AddComponent<SpriteRenderer>() };
                instance.pool.Add(puff);
            }
            if (puff == null)
            {
                // At the cap, retire the oldest cosmetic instead of allocating.
                puff = instance.pool[0];
                foreach (var candidate in instance.pool) if (candidate.started < puff.started) puff = candidate;
            }
            puff.active = true;
            puff.end = end;
            puff.origin = position;
            puff.drift = Vector3.zero;
            puff.rotation = 0;
            puff.started = Time.time;
            puff.lifetime = end ? NeedleLifetime : .6f;
            puff.size = end ? NeedleSize : .4f;
            puff.renderer.sprite = sprite;
            puff.renderer.sortingLayerID = reference != null ? reference.sortingLayerID : 0;
            puff.renderer.sortingOrder = (reference != null ? reference.sortingOrder : 0) + (end ? 3 : -1);
            puff.renderer.enabled = true;
            Apply(puff, 0);
            return puff;
        }

        void Update()
        {
            foreach (var puff in pool)
            {
                if (!puff.active) continue;
                float age = (Time.time - puff.started) / puff.lifetime;
                if (age >= 1) { puff.active = false; puff.renderer.enabled = false; }
                else Apply(puff, age);
            }
        }

        static void Apply(Puff puff, float age)
        {
            float scale = puff.end ? Mathf.Lerp(.55f, 1.2f, Mathf.Min(1, age * 3)) : Mathf.Lerp(1, .35f, age);
            var size = puff.renderer.sprite.bounds.size;
            puff.renderer.transform.localScale = Vector3.one * (puff.size / Mathf.Max(size.x, size.y) * scale);
            puff.renderer.transform.position = puff.origin + puff.drift * age;
            puff.renderer.transform.rotation = Quaternion.Euler(0, 0, puff.rotation);
            puff.renderer.color = new Color(1, 1, 1, puff.end ? Mathf.Clamp01((1 - age) * 2) : Mathf.Pow(1 - age, .65f));
        }

        void OnDestroy() { if (instance == this) instance = null; }
    }
}
