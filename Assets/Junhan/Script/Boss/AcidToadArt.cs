using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vampire
{
    // Imported slices retain a common pixels-per-unit and grounded pivot across motions.
    public static class AcidToadArt
    {
        static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
        public static Sprite[] Frames(string sheet)
        {
            if (!cache.TryGetValue(sheet, out var frames))
                cache[sheet] = frames = Resources.LoadAll<Sprite>("ToadUpdate/" + sheet).OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
            return frames;
        }
        public static Sprite Frame(string sheet, int frame)
        {
            var frames = Frames(sheet);
            return frames.Length == 0 ? null : frames[Mathf.Clamp(frame, 0, frames.Length - 1)];
        }
    }

    public sealed class FoodAtlasMotion : MonoBehaviour
    {
        SpriteRenderer art, previous;
        string sheet;
        int first, count;
        float age, seconds, blend;
        bool loop, manual;
        public float Progress { get; private set; }
        public SpriteRenderer Renderer => art;
        public void Configure(SpriteRenderer renderer)
        {
            art = renderer;
            if (previous != null) return;
            var go = new GameObject("Motion transition"); go.transform.SetParent(art.transform, false);
            previous = go.AddComponent<SpriteRenderer>(); previous.sharedMaterial = art.sharedMaterial;
            previous.sortingLayerID = art.sortingLayerID; previous.sortingOrder = art.sortingOrder - 1;
            previous.enabled = false;
        }
        public void Play(string name, int start, int length, float duration, bool repeat = false)
        {
            if (sheet == name && first == start && count == length && loop == repeat && !manual) return;
            Transition(); sheet = name; first = start; count = length; seconds = Mathf.Max(.01f, duration);
            loop = repeat; age = 0; manual = false; Show(0);
        }
        public void Sample(string name, int start, int length, float progress)
        {
            if (sheet != name || first != start || !manual) Transition();
            sheet = name; first = start; count = length; manual = true; Show(progress);
        }
        void Transition()
        {
            if (previous == null || art == null) return;
            previous.sprite = art.sprite; previous.flipX = art.flipX;
            previous.enabled = previous.sprite != null; blend = .08f;
        }
        void Show(float progress)
        {
            Progress = Mathf.Clamp01(progress);
            if (art != null) art.sprite = AcidToadArt.Frame(sheet, first + Mathf.Min(count - 1, Mathf.FloorToInt(Progress * count)));
        }
        void LateUpdate()
        {
            if (art == null || string.IsNullOrEmpty(sheet) || Time.timeScale <= 0 || MiniStageRuntimeState.IsInsideMiniStage) return;
            if (!manual) { age += Time.deltaTime; Show(loop ? Mathf.Repeat(age / seconds, 1) : Mathf.Clamp01(age / seconds)); }
            blend = Mathf.Max(0, blend - Time.deltaTime);
            if (previous != null)
            {
                previous.enabled = blend > 0;
                previous.color = new Color(1, 1, 1, blend / .08f * .35f);
                previous.sortingOrder = art.sortingOrder - 1;
            }
        }
    }
}
