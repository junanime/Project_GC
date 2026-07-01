using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class AugmentHistoryManager : MonoBehaviour
    {
        public static AugmentHistoryManager Instance { get; private set; }

        [Serializable]
        public class AugmentEntry
        {
            public string key;
            public string displayName;
            public Sprite icon;
            public Ability.AugmentTier tier;
            public int level;
        }

        public event Action Changed;

        private readonly List<AugmentEntry> entries = new();
        public IReadOnlyList<AugmentEntry> Entries => entries;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void RecordAbility(Ability ability)
        {
            if (ability == null)
            {
                return;
            }

            
            

            string key = $"Ability_{ability.GetInstanceID()}";

            AugmentEntry entry = entries.Find(x => x.key == key);

            if (entry == null)
            {
                entry = new AugmentEntry
                {
                    key = key,
                    displayName = ability.Name,
                    icon = ability.Image,
                    tier = ability.Tier,
                    level = ability.Level
                };

                entries.Add(entry);
            }
            else
            {
                entry.level = ability.Level;
            }

            Changed?.Invoke();
        }

        public void RecordGeneralAugment(
            string augmentId,
            string displayName,
            Sprite icon)
        {
            string key = $"General_{augmentId}";

            AugmentEntry entry = entries.Find(x => x.key == key);

            if (entry == null)
            {
                entry = new AugmentEntry
                {
                    key = key,
                    displayName = displayName,
                    icon = icon,
                    tier = Ability.AugmentTier.General,
                    level = 1
                };

                entries.Add(entry);
            }
            else
            {
                entry.level++;
            }

            Changed?.Invoke();
        }
    }
}