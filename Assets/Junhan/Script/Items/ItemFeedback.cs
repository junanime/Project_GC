using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Vampire
{
    public enum ItemFeedbackKind { PressureMark, PressureBurst, FireZone, Spirit, Steam, Revival, Echo, Themed }

    // Presentation only: exhausting the budget must never cancel a gameplay effect.
    public sealed partial class ItemFeedback : MonoBehaviour
    {
        public const int Capacity = 24;
        readonly List<ItemFeedbackVisual> pool = new List<ItemFeedbackVisual>();
        Material material;
        Character character;
        float nextCaption;
        public int CreatedCount => pool.Count;
        public static ItemFeedback For(Component owner) => owner.GetComponent<ItemFeedback>() ?? owner.gameObject.AddComponent<ItemFeedback>();
        void Awake() { character = GetComponent<Character>(); }
        void LateUpdate() { if (character != null && !character.IsAlive) OnDisable(); }

        public ItemFeedbackVisual Show(ItemFeedbackKind kind, Vector3 position, float duration, float radius = 1,
            Transform follow = null, int stacks = 0)
        {
            if (material == null) material = new Material(Shader.Find("Sprites/Default"));
            ItemFeedbackVisual visual = null;
            int active = 0;
            foreach (var candidate in pool)
            {
                if (candidate == null) continue;
                if (candidate.gameObject.activeSelf) active++;
                else if (visual == null) visual = candidate;
            }
            // Reserve one slot so a rare, important revival cannot be hidden by hit spam.
            int limit = kind == ItemFeedbackKind.Revival ? Capacity : Capacity - 1;
            if (active >= limit) return null;
            if (visual == null && pool.Count < limit)
            {
                visual = new GameObject("Item feedback").AddComponent<ItemFeedbackVisual>();
                visual.Initialize(material);
                pool.Add(visual);
            }
            if (visual == null) return null;
            string caption = kind == ItemFeedbackKind.PressureBurst ? "점혈!" : kind == ItemFeedbackKind.Steam ? "증기 폭발!" : kind == ItemFeedbackKind.Revival ? "다시 피어나다" : "";
            if (caption.Length > 0 && (Time.time >= nextCaption || kind == ItemFeedbackKind.Revival)) nextCaption = Time.time + .45f;
            else caption = "";
            visual.Play(kind, position, duration, radius, follow, stacks, caption);
            if(Application.isPlaying && character!=null && (kind==ItemFeedbackKind.PressureBurst || kind==ItemFeedbackKind.Steam || kind==ItemFeedbackKind.Revival))
                ItemProcAudio.For(this).Play(kind==ItemFeedbackKind.Revival?"bloom":kind==ItemFeedbackKind.Steam?"bubble":"pop");
            return visual;
        }

        void OnDisable()
        {
            foreach (var visual in pool) if (visual != null) visual.Release();
        }
        void OnDestroy()
        {
            foreach (var visual in pool) if (visual != null) Dispose(visual.gameObject);
            if (material != null) Dispose(material);
        }
        internal static void Dispose(Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }

}
