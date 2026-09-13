using UnityEngine;
using UnityEngine.Rendering;

namespace Vampire
{
    /// <summary>Backgrounds &lt; floor effects &lt; actors, projectiles and props.</summary>
    public static class GroundVisualSorting
    {
        public const string LayerName = "GroundEffects";
        public const string BackgroundLayerName = "Background";

        public static void Apply(Renderer renderer, int order = 0)
        {
            if (renderer == null) return;
            renderer.sortingLayerName = LayerName;
            renderer.sortingOrder = order;
        }

        // Use only on a dedicated floor visual root, never on an actor or room root.
        public static void ApplyHierarchy(GameObject root, int order = 0)
        {
            if (root == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                Apply(renderer, order);
            foreach (var group in root.GetComponentsInChildren<SortingGroup>(true))
            {
                group.sortingLayerName = LayerName;
                group.sortingOrder = order;
            }
        }

        public static void ApplyBackground(Renderer renderer, int order)
        {
            if (renderer == null) return;
            renderer.sortingLayerName = BackgroundLayerName;
            renderer.sortingOrder = order;
            var group = renderer.GetComponent<SortingGroup>();
            if (group != null)
            {
                group.sortingLayerName = BackgroundLayerName;
                group.sortingOrder = order;
            }
        }
    }
}
