using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Scene queries for combat and field mechanics also include active objects
    // outside EntityManager's pools. Keep the former FindObjectsOfType InstanceID order.
    public static class StageActorRegistry
    {
        static class Entries<T> where T : Component
        {
            internal static readonly SortedList<int, T> Objects = new SortedList<int, T>();
            internal static bool Ready;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Clear<Monster>();
            Clear<DigestiveEnzymeMonster>();
            Clear<ExpGem>();
            Clear<Coin>();
        }

        static void Clear<T>() where T : Component
        {
            Entries<T>.Objects.Clear();
            Entries<T>.Ready = false;
        }

        internal static void Register<T>(T target) where T : Component
        {
            if (Entries<T>.Ready) Entries<T>.Objects[target.GetInstanceID()] = target;
        }

        internal static void Remove<T>(T target) where T : Component
        {
            if (Entries<T>.Ready) Entries<T>.Objects.Remove(target.GetInstanceID());
        }

        public static void CopyActiveMonsters(List<Monster> destination) => CopyActive(destination);
        public static void CopyActiveEnzymes(List<DigestiveEnzymeMonster> destination) => CopyActive(destination);
        public static void CopyActiveGems(List<ExpGem> destination) => CopyActive(destination);
        public static void CopyActiveCoins(List<Coin> destination) => CopyActive(destination);

        static void CopyActive<T>(List<T> destination) where T : Component
        {
            var objects = Entries<T>.Objects;
            if (!Entries<T>.Ready)
            {
                // Pooled objects may already have had Awake before the first query.
                foreach (T t in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    objects[t.GetInstanceID()] = t;
                Entries<T>.Ready = true;
            }
            destination.Clear();
            var values = objects.Values;
            for (int i = 0; i < values.Count; i++)
            {
                T target = values[i];
                if (target == null)
                {
                    // An initially inactive seeded object may be destroyed without OnDestroy.
                    objects.RemoveAt(i--);
                    continue;
                }
                var go = target.gameObject;
                if (go.activeInHierarchy && ((go.hideFlags | target.hideFlags) & HideFlags.DontSave) == 0)
                    destination.Add(target);
            }
        }
    }
}
