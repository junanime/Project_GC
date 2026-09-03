using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 몬스터 디버퍼가 발사하는 전용 투사체입니다.
    /// 플레이어에게 맞으면 공격속도 감소 디버프를 적용합니다.
    /// </summary>
    public class AttackSpeedDownProjectile : Projectile
    {
        [Header("Attack Speed Debuff")]
        [Tooltip("플레이어 공격속도 감소량입니다. 0.2 = 공격속도 20% 감소입니다.")]
        [SerializeField] private float attackSpeedReduction = 0.2f;

        [Tooltip("공격속도 감소 디버프 지속 시간입니다.")]
        [SerializeField] private float debuffDuration = 4f;

        [Header("Arrow Rain Visual")]
        [Tooltip("공격속도 감소 중 표시할 아래 화살표 색상입니다.")]
        [SerializeField] private Color arrowColor = Color.yellow;

        [Tooltip("화살표 비 연출이 플레이어 중심에서 위로 얼마나 올라갈지 설정합니다.")]
        [SerializeField] private float arrowYOffset = 1.4f;

        [Tooltip("화살표 비 연출의 가로 폭입니다.")]
        [SerializeField] private float arrowWidth = 0.8f;

        [Tooltip("화살표가 떨어지는 세로 범위입니다.")]
        [SerializeField] private float arrowHeight = 1.1f;

        [Tooltip("화살표가 아래로 떨어지는 속도입니다.")]
        [SerializeField] private float arrowFallSpeed = 1.5f;

        [Tooltip("동시에 표시할 화살표 개수입니다.")]
        [SerializeField] private int arrowCount = 5;

        [Tooltip("화살표 TextMeshPro 글자 크기입니다.")]
        [SerializeField] private float arrowFontSize = 2.2f;

        [Tooltip("화살표 Sorting Order입니다.")]
        [SerializeField] private int arrowSortingOrder = 150;

        [Header("Debug")]
        [Tooltip("체크하면 공격속도 디버프 적용 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = false;

        protected override void HitDamageable(IDamageable damageable)
        {
            Character targetCharacter = damageable as Character;

            if (targetCharacter != null)
            {
                PlayerAttackSpeedDebuffRuntime runtime =
                    targetCharacter.GetComponent<PlayerAttackSpeedDebuffRuntime>();

                if (runtime == null)
                {
                    runtime = targetCharacter.gameObject.AddComponent<PlayerAttackSpeedDebuffRuntime>();
                }

                runtime.Apply(
                    attackSpeedReduction,
                    debuffDuration,
                    arrowColor,
                    arrowYOffset,
                    arrowWidth,
                    arrowHeight,
                    arrowFallSpeed,
                    arrowCount,
                    arrowFontSize,
                    arrowSortingOrder,
                    debugLog);
            }

            base.HitDamageable(damageable);
        }
    }
}