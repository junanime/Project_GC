using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Shared sprite sequences for the six approved UFO attacks.
    public static class BossPatternArt
    {
        private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
        public static Sprite[] Frames(string name)
        {
            if (!cache.TryGetValue(name, out var result))
            {
                result = Resources.LoadAll<Sprite>("BossPatternArt/" + name);
                Array.Sort(result, (a, b) => string.CompareOrdinal(a.name, b.name));
                cache[name] = result;
            }
            return result;
        }

        public static BossSpriteSequence Replace(GameObject target, string sequence, float width, float fps = 12, bool loop = true)
        {
            if (Frames(sequence).Length == 0) return null;
            var existing = target.GetComponentInChildren<BossSpriteSequence>();
            if (existing != null) { existing.Play(sequence, width, fps, loop); return existing; }
            int order = 550, layer = 0;
            foreach (var sr in target.GetComponentsInChildren<SpriteRenderer>(true))
            { order = Mathf.Max(order, sr.sortingOrder); layer = sr.sortingLayerID; sr.enabled = false; }
            foreach (var ps in target.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var go = new GameObject("PatternArt"); go.transform.SetParent(target.transform, false);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sortingOrder = order; renderer.sortingLayerID = layer;
            var sequencePlayer = go.AddComponent<BossSpriteSequence>(); sequencePlayer.Play(sequence, width, fps, loop);
            return sequencePlayer;
        }

        public static void Bullet(GameObject target, int phase)
        {
            var visual = Replace(target, "Bullet" + Mathf.Clamp(phase, 1, 3), .28f);
            if (visual == null) return;
            // Match the small artwork to the hit area, independent of the old prefab scale.
            foreach (var col in target.GetComponentsInChildren<Collider2D>())
            {
                float sx = Mathf.Max(.001f, Mathf.Abs(col.transform.lossyScale.x));
                float sy = Mathf.Max(.001f, Mathf.Abs(col.transform.lossyScale.y));
                if (col is CircleCollider2D circle) { circle.offset = Vector2.zero; circle.radius = .12f / Mathf.Max(sx, sy); }
                else if (col is BoxCollider2D box) { box.offset = Vector2.zero; box.size = new Vector2(.24f / sx, .24f / sy); }
            }
        }

        public static BossSpriteSequence Effect(string sequence, Vector3 position, float width, float fps = 12)
        {
            if (Frames(sequence).Length == 0) return null;
            var go = new GameObject(sequence); go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = 560;
            var player = go.AddComponent<BossSpriteSequence>(); player.Play(sequence, width, fps, false);
            player.DestroyAfterPlayback = true;
            return player;
        }
    }
}
