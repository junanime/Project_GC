using TMPro;
using UnityEngine;

namespace Vampire
{
    public class StatsDetailPanelUI : MonoBehaviour
    {
        [Header("기본 스탯")]
        [SerializeField] private TextMeshProUGUI hpValueText;
        [SerializeField] private TextMeshProUGUI moveSpeedValueText;
        [SerializeField] private TextMeshProUGUI armorValueText;

        [Header("공격 스탯")]
        [SerializeField] private TextMeshProUGUI damageMultiplierValueText;
        [SerializeField] private TextMeshProUGUI attackSpeedValueText;
        [SerializeField] private TextMeshProUGUI critChanceValueText;
        [SerializeField] private TextMeshProUGUI rangeMultiplierValueText;
        [SerializeField] private TextMeshProUGUI additionalProjectilesValueText;
        [SerializeField] private TextMeshProUGUI projectileSpeedValueText;
        [SerializeField] private TextMeshProUGUI projectileSizeValueText;
        [SerializeField] private TextMeshProUGUI additionalPierceValueText;
        [SerializeField] private TextMeshProUGUI burnChanceValueText;
        [SerializeField] private TextMeshProUGUI slowChanceValueText;

        [Header("회복 · 성장")]
        [SerializeField] private TextMeshProUGUI lifeStealValueText;
        [SerializeField] private TextMeshProUGUI healOnKillValueText;
        [SerializeField] private TextMeshProUGUI idleHealPerSecondValueText;
        [SerializeField] private TextMeshProUGUI magnetRangeBonusValueText;
        [SerializeField] private TextMeshProUGUI experienceMultiplierValueText;
        [SerializeField] private TextMeshProUGUI luckValueText;

        [Header("대시 · 생존")]
        [SerializeField] private TextMeshProUGUI dashChargesValueText;
        [SerializeField] private TextMeshProUGUI dashDistanceValueText;
        [SerializeField] private TextMeshProUGUI dashRechargeTimeValueText;
        [SerializeField] private TextMeshProUGUI hasShieldValueText;
        [SerializeField] private TextMeshProUGUI reviveCountValueText;
        [SerializeField] private TextMeshProUGUI invincibilityTimeBonusValueText;

        [Header("특수 효과")]
        [SerializeField] private TextMeshProUGUI thermometerStacksValueText;
        [SerializeField] private TextMeshProUGUI mouthwashCountValueText;
        [SerializeField] private TextMeshProUGUI reflexHammerCountValueText;
        [SerializeField] private TextMeshProUGUI antibioticBombChanceValueText;
        [SerializeField] private TextMeshProUGUI hasGinsengStickValueText;
        [SerializeField] private TextMeshProUGUI autoCollectItemsValueText;

        private Character playerCharacter;

        private void OnEnable()
        {
            ResolvePlayer();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void ResolvePlayer()
        {
            if (playerCharacter == null)
            {
                playerCharacter = FindObjectOfType<Character>();
            }
        }

        private void Refresh()
        {
            if (playerCharacter == null)
            {
                ResolvePlayer();
            }

            if (playerCharacter == null)
            {
                SetAllTexts("-");
                return;
            }

            Character c = playerCharacter;

            SetText(hpValueText, $"{c.CurrentHealth:0}/{c.MaxHealth:0}");
            SetText(moveSpeedValueText, $"{c.CurrentMoveSpeed:0.0}");
            SetText(armorValueText, $"{c.CurrentArmor:0}");

            SetText(damageMultiplierValueText, $"x{c.DamageMultiplier:0.00}");
            SetText(attackSpeedValueText, $"x{c.AttackSpeedMultiplier:0.00}");
            SetText(critChanceValueText, $"{c.CritChance * 100f:0}%");
            SetText(rangeMultiplierValueText, $"x{c.RangeMultiplier:0.00}");
            SetText(additionalProjectilesValueText, $"+{c.AdditionalProjectiles}");
            SetText(projectileSpeedValueText, $"x{c.ProjectileSpeedMultiplier:0.00}");
            SetText(projectileSizeValueText, $"x{c.ProjectileSizeMultiplier:0.00}");
            SetText(additionalPierceValueText, $"+{c.AdditionalPierce}");
            SetText(burnChanceValueText, $"{c.BurnChance * 100f:0}%");
            SetText(slowChanceValueText, $"{c.SlowChance * 100f:0}%");

            SetText(lifeStealValueText, $"{c.LifeSteal * 100f:0}%");
            SetText(healOnKillValueText, $"{c.HealOnKill:0.##}");
            SetText(idleHealPerSecondValueText, $"{c.IdleHealPerSecond * 100f:0.##}%");
            SetText(magnetRangeBonusValueText, $"+{c.MagnetRangeBonus:0.##}");
            SetText(experienceMultiplierValueText, $"x{c.ExperienceMultiplier:0.00}");
            SetText(luckValueText, $"{c.Luck:0.##}");

            SetText(dashChargesValueText, $"{c.CurrentDashCharges}/{c.MaxDashCharges}");
            SetText(dashDistanceValueText, $"{c.DashDistance:0.0}");
            SetText(dashRechargeTimeValueText, $"{c.DashRechargeTime:0.00}s");
            SetText(hasShieldValueText, c.HasShield ? "있음" : "없음");
            SetText(reviveCountValueText, $"{c.ReviveCount}");
            SetText(invincibilityTimeBonusValueText, $"{c.InvincibilityTimeBonus:0.00}s");

            SetText(thermometerStacksValueText, $"{c.ThermometerStacks}");
            SetText(mouthwashCountValueText, $"{c.MouthwashCount}");
            SetText(reflexHammerCountValueText, $"{c.ReflexHammerCount}");
            SetText(antibioticBombChanceValueText, $"{c.AntibioticBombChance * 100f:0}%");
            SetText(hasGinsengStickValueText, c.HasGinsengStick ? "보유" : "없음");
            SetText(autoCollectItemsValueText, c.AutoCollectItems ? "활성" : "비활성");
        }

        private void SetText(TextMeshProUGUI textUI, string value)
        {
            if (textUI != null)
            {
                textUI.text = value;
            }
        }

        private void SetAllTexts(string value)
        {
            SetText(hpValueText, value);
            SetText(moveSpeedValueText, value);
            SetText(armorValueText, value);

            SetText(damageMultiplierValueText, value);
            SetText(attackSpeedValueText, value);
            SetText(critChanceValueText, value);
            SetText(rangeMultiplierValueText, value);
            SetText(additionalProjectilesValueText, value);
            SetText(projectileSpeedValueText, value);
            SetText(projectileSizeValueText, value);
            SetText(additionalPierceValueText, value);
            SetText(burnChanceValueText, value);
            SetText(slowChanceValueText, value);

            SetText(lifeStealValueText, value);
            SetText(healOnKillValueText, value);
            SetText(idleHealPerSecondValueText, value);
            SetText(magnetRangeBonusValueText, value);
            SetText(experienceMultiplierValueText, value);
            SetText(luckValueText, value);

            SetText(dashChargesValueText, value);
            SetText(dashDistanceValueText, value);
            SetText(dashRechargeTimeValueText, value);
            SetText(hasShieldValueText, value);
            SetText(reviveCountValueText, value);
            SetText(invincibilityTimeBonusValueText, value);

            SetText(thermometerStacksValueText, value);
            SetText(mouthwashCountValueText, value);
            SetText(reflexHammerCountValueText, value);
            SetText(antibioticBombChanceValueText, value);
            SetText(hasGinsengStickValueText, value);
            SetText(autoCollectItemsValueText, value);
        }
    }
}