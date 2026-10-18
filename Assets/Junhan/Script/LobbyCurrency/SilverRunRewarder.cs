using UnityEngine;

namespace Vampire
{
    public class SilverRunRewarder : MonoBehaviour
    {
        public static SilverRunRewarder Instance { get; private set; }

        [Header("Normal Monster Reward")]
        [SerializeField] private int normalMonsterSilverMin = 1;
        [SerializeField] private int normalMonsterSilverMax = 2;

        [Header("Boss Reward")]
        [SerializeField] private int bossMonsterBonusSilver = 100;

        [Header("Debug")]
        [SerializeField] private bool debugLog = true;

        public int RunEarnedSilver { get; private set; }
        private bool pickupOnly;
        public static void RewardPickup(int amount)
        {
            if (amount <= 0) return;
            if (Instance == null)
            {
                var level = FindObjectOfType<LevelManager>();
                if (level != null)
                {
                    var tracker = level.gameObject.AddComponent<SilverRunRewarder>();
                    // A scene without kill rewards stays that way; only track the new jar pickups.
                    tracker.pickupOnly = true;
                }
            }
            if (Instance != null) Instance.RunEarnedSilver += amount;
            SilverWallet.Add(amount);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            RunEarnedSilver = 0;
        }

        public static void RewardMonsterKill(MonsterBlueprint monsterBlueprint)
        {
            if (Instance == null)
            {
                return;
            }

            Instance.AddReward(monsterBlueprint);
        }

        private void AddReward(MonsterBlueprint monsterBlueprint)
        {
            if (pickupOnly) return;
            int amount = Random.Range(normalMonsterSilverMin, normalMonsterSilverMax + 1);

            if (monsterBlueprint is BossMonsterBlueprint)
            {
                amount += bossMonsterBonusSilver;
            }

            RunEarnedSilver += amount;
            SilverWallet.Add(amount);

            if (debugLog)
            {
                Debug.Log($"[Silver] +{amount} | Run Earned = {RunEarnedSilver} | Total = {SilverWallet.Silver}");
            }
        }
    }
}
