using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>Add to flying enemies to cross blood clots without changing their damage layers.</summary>
    [DisallowMultipleComponent]
    public sealed class BloodClotPassThrough : MonoBehaviour
    {
        internal static readonly HashSet<BloodClotPassThrough> Active = new HashSet<BloodClotPassThrough>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() { Active.Clear(); }

        private void OnEnable()
        {
            Active.Add(this);
            foreach (var obstacle in BloodClotObstacle.Active)
                if (obstacle != null) ApplyTo(obstacle, true);
        }
        private void OnDisable()
        {
            Active.Remove(this);
            foreach (var obstacle in BloodClotObstacle.Active)
                if (obstacle != null) ApplyTo(obstacle, false);
        }
        internal void ApplyTo(BloodClotObstacle obstacle, bool ignore)
        {
            if (obstacle.Solid == null) return;
            foreach (var collider in GetComponentsInChildren<Collider2D>(true))
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                    Physics2D.IgnoreCollision(collider, obstacle.Solid, ignore);
        }
    }
}
