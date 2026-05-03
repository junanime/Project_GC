using UnityEngine;

namespace Vampire
{
    public class BossVisualOrderKeeper : MonoBehaviour
    {
        [Header("Boss Visual Sorting")]
        [SerializeField] private int bossSortingOrder = 800;
        [SerializeField] private bool applyEveryFrame = true;

        private SpriteRenderer[] renderers;

        private void Awake()
        {
            CacheRenderers();
            ApplySortingOrder();
        }

        private void LateUpdate()
        {
            if (applyEveryFrame)
            {
                ApplySortingOrder();
            }
        }

        private void CacheRenderers()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void ApplySortingOrder()
        {
            if (renderers == null || renderers.Length == 0)
            {
                CacheRenderers();
            }

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.sortingOrder = bossSortingOrder;
            }
        }
    }
}