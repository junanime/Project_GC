using UnityEngine;

namespace Vampire
{
    public class BossProjectileVisualOrder : MonoBehaviour
    {
        [Header("Projectile Visual Sorting")]
        [SerializeField] private int projectileSortingOrder = 500;

        private void Awake()
        {
            ApplySortingOrder();
        }

        private void OnEnable()
        {
            ApplySortingOrder();
        }

        private void ApplySortingOrder()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.sortingOrder = projectileSortingOrder;
            }
        }
    }
}