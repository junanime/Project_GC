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
                statsText.text = "캐릭터 정보를 불러오는 중...";
                return;
            }

            Character c = playerCharacter;

            statsText.text =
                "<b>기본 스탯</b>\n" +
                $"체력: {c.CurrentHealth:0} / {c.MaxHealth:0}\n" +
                $"이동 속도: {c.CurrentMoveSpeed:0.0}\n" +
                $"방어력: {c.CurrentArmor:0}\n\n" +

                "<b>공격 스탯</b>\n" +
                $"피해 배율: x{c.DamageMultiplier:0.00}\n" +
                $"공격 속도: x{c.AttackSpeedMultiplier:0.00}\n" +
                $"치명타 확률: {c.CritChance * 100f:0}%\n" +
                $"공격 범위: x{c.RangeMultiplier:0.00}\n" +
                $"투사체 추가: +{c.AdditionalProjectiles}\n" +
                $"투사체 속도: x{c.ProjectileSpeedMultiplier:0.00}\n" +
                $"투사체 크기: x{c.ProjectileSizeMultiplier:0.00}\n" +
                $"관통 추가: +{c.AdditionalPierce}\n" +
                $"화상 확률: {c.BurnChance * 100f:0}%\n" +
                $"둔화 확률: {c.SlowChance * 100f:0}%\n\n" +

                "<b>회복 · 성장</b>\n" +
                $"흡혈: {c.LifeSteal * 100f:0}%\n" +
                $"처치 회복: {c.HealOnKill:0.##}\n" +
                $"정지 회복: 최대 체력의 {c.IdleHealPerSecond * 100f:0.##}% / 초\n" +
                $"자석 범위 추가: +{c.MagnetRangeBonus:0.##}\n" +
                $"경험치 획득: x{c.ExperienceMultiplier:0.00}\n" +
                $"행운: {c.Luck:0.##}\n\n" +

                "<b>대시 · 생존</b>\n" +
                $"대시 충전: {c.CurrentDashCharges} / {c.MaxDashCharges}\n" +
                $"대시 거리: {c.DashDistance:0.0}\n" +
                $"대시 충전 시간: {c.DashRechargeTime:0.00}초\n" +
                $"보호막: {(c.HasShield ? "활성" : "없음")}\n" +
                $"부활 횟수: {c.ReviveCount}\n" +
                $"피격 무적 추가: {c.InvincibilityTimeBonus:0.00}초\n\n" +

                "<b>특수 효과</b>\n" +
                $"체온계 스택: {c.ThermometerStacks}\n" +
                $"구강청결제: {c.MouthwashCount}\n" +
                $"반사신경 망치: {c.ReflexHammerCount}\n" +
                $"항생제 폭탄 확률: {c.AntibioticBombChance * 100f:0}%\n" +
                $"인삼 스틱: {(c.HasGinsengStick ? "보유" : "없음")}\n" +
                $"자동 수집: {(c.AutoCollectItems ? "활성" : "비활성")}";
        }
    }
}