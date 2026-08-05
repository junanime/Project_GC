using TMPro;
using UnityEngine;

namespace Vampire
{
    public class StatsDetailPanelUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statsText;

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
            if (statsText == null)
            {
                return;
            }

            if (playerCharacter == null)
            {
                ResolvePlayer();
            }

            if (playerCharacter == null)
            {
                statsText.text = "<align=center><b>캐릭터 정보를 불러오는 중...</b></align>";
                return;
            }

            Character c = playerCharacter;

            statsText.text =
                "<line-height=115%>" +

                SectionTitle("기본 스탯") +
                StatRow("체력", $"{c.CurrentHealth:0} / {c.MaxHealth:0}") +
                StatRow("이동 속도", $"{c.CurrentMoveSpeed:0.0}") +
                StatRow("방어력", $"{c.CurrentArmor:0}") +
                Space() +

                SectionTitle("공격 스탯") +
                StatRow("피해 배율", $"x{c.DamageMultiplier:0.00}") +
                StatRow("공격 속도", $"x{c.AttackSpeedMultiplier:0.00}") +
                StatRow("치명타 확률", $"{c.CritChance * 100f:0}%") +
                StatRow("공격 범위", $"x{c.RangeMultiplier:0.00}") +
                StatRow("투사체 추가", $"+{c.AdditionalProjectiles}") +
                StatRow("투사체 속도", $"x{c.ProjectileSpeedMultiplier:0.00}") +
                StatRow("투사체 크기", $"x{c.ProjectileSizeMultiplier:0.00}") +
                StatRow("관통 추가", $"+{c.AdditionalPierce}") +
                StatRow("화상 확률", $"{c.BurnChance * 100f:0}%") +
                StatRow("둔화 확률", $"{c.SlowChance * 100f:0}%") +
                Space() +

                SectionTitle("회복 · 성장") +
                StatRow("흡혈", $"{c.LifeSteal * 100f:0}%") +
                StatRow("처치 회복", $"{c.HealOnKill:0.##}") +
                StatRow("정지 회복", $"최대 체력의 {c.IdleHealPerSecond * 100f:0.##}% / 초") +
                StatRow("자석 범위 추가", $"+{c.MagnetRangeBonus:0.##}") +
                StatRow("경험치 획득", $"x{c.ExperienceMultiplier:0.00}") +
                StatRow("행운", $"{c.Luck:0.##}") +
                Space() +

                SectionTitle("대시 · 생존") +
                StatRow("대시 충전", $"{c.CurrentDashCharges} / {c.MaxDashCharges}") +
                StatRow("대시 거리", $"{c.DashDistance:0.0}") +
                StatRow("대시 충전 시간", $"{c.DashRechargeTime:0.00}초") +
                StatRow("보호막", c.HasShield ? "활성" : "없음") +
                StatRow("부활 횟수", $"{c.ReviveCount}") +
                StatRow("피격 무적 추가", $"{c.InvincibilityTimeBonus:0.00}초") +
                Space() +

                SectionTitle("특수 효과") +
                StatRow("체온계 스택", $"{c.ThermometerStacks}") +
                StatRow("구강청결제", $"{c.MouthwashCount}") +
                StatRow("반사신경 망치", $"{c.ReflexHammerCount}") +
                StatRow("항생제 폭탄 확률", $"{c.AntibioticBombChance * 100f:0}%") +
                StatRow("인삼 스틱", c.HasGinsengStick ? "보유" : "없음") +
                StatRow("자동 수집", c.AutoCollectItems ? "활성" : "비활성") +

                "</line-height>";
        }

        private string SectionTitle(string title)
        {
            return
                $"<color=#FFD86B><b>{title}</b></color>\n" +
                "<color=#8A5A3C>────────────────</color>\n";
        }

        private string StatRow(string label, string value)
        {
            return
                $"<color=#6F5B4A>{label}</color>  " +
                $"<color=#2B1A12><b>{value}</b></color>\n";
        }

        private string Space()
        {
            return "\n";
        }
    }
}