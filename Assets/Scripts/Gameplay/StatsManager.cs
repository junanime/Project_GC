using UnityEngine;
using TMPro;

namespace Vampire
{
    public class StatsManager : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI monstersKilledText;
        [SerializeField] private TextMeshProUGUI coinsGainedText;

        private int monstersKilled = 0;
        private float damageDealt = 0;
        private float damageTaken = 0;
        private int coinsGained = 0;

        public int MonstersKilled { get => monstersKilled; }
        public float DamageDealt { get => damageDealt; }
        public float DamageTaken { get => damageTaken; }
        public int CoinsGained { get => coinsGained; }
        public int NormalMonstersKilled { get; private set; }
        public int EliteMonstersKilled { get; private set; }
        public int MiniStagesCleared { get; private set; }
        public int SupplyCapsulesCollected { get; private set; }
        public float HealingReceived { get; private set; }
        public void RecordHealing(float actual) => HealingReceived += Mathf.Max(0, actual);
        public void RecordMiniStageClear() { MiniStagesCleared++; RunScore.Current?.Clear(); }
        public void RecordSupplyCollected() => SupplyCapsulesCollected++;
        public void RecordMonsterKill(Monster monster)
        {
            monster.RecordRankingKill();
            IncrementMonstersKilled();
            if(monster.Blueprint is EliteMonsterBlueprint) EliteMonstersKilled++;
            else if(!(monster is BossMonster) && !(monster is MiniBossMonster)) NormalMonstersKilled++;
        }

        private void Start()
        {
            TrainingUITheme.Counter(monstersKilledText,"HudLeaf",156);
            TrainingUITheme.Counter(coinsGainedText,"HudCoin",22);
        }

        public void IncrementMonstersKilled()
        {
            monstersKilled++;
            if(monstersKilledText!=null)monstersKilledText.text = monstersKilled.ToString();
        }

        public void IncreaseCoinsGained(int amount)
        {
            coinsGained += amount;
            coinsGainedText.text = coinsGained.ToString();
        }

        public void IncreaseDamageDealt(float damage)
        {
            damageDealt += damage; 
        }

        public void IncreaseDamageTaken(float damage)
        {
            damageTaken += damage;
        }
    }
}
