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
        private int displayedDemoScore = -1;

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
            if (GStarDemo.Enabled && coinsGainedText != null)
            {
                var icon = coinsGainedText.transform.parent.Find("Counter icon")?.GetComponent<UnityEngine.UI.Image>();
                if (icon != null) icon.sprite = GStarScoreTokenArt.Sprite;
                coinsGainedText.fontSize = 20;
                coinsGainedText.enableAutoSizing = true;
                coinsGainedText.fontSizeMin = 13;
                coinsGainedText.fontSizeMax = 20;
            }
        }

        private void LateUpdate()
        {
            if (!GStarDemo.Enabled || coinsGainedText == null) return;
            int score = RunScore.Current?.Data.Total ?? 0;
            if (displayedDemoScore == score) return;
            displayedDemoScore = score;
            coinsGainedText.text = score.ToString("N0") + "점";
        }

        public void IncrementMonstersKilled()
        {
            monstersKilled++;
            if(monstersKilledText!=null)monstersKilledText.text = monstersKilled.ToString();
        }

        public void IncreaseCoinsGained(int amount)
        {
            if (GStarDemo.Enabled)
            {
                if (amount > 0) RunScore.Current?.Gold(amount);
                return;
            }
            coinsGained += amount;
            if (coinsGainedText != null) coinsGainedText.text = coinsGained.ToString();
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
