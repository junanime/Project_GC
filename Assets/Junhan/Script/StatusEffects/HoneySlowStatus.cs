using System.Collections;
using UnityEngine;
using Parent = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;

namespace Vampire
{
    // 꿀침 상태이상
    // 몬스터의 Rigidbody2D.drag를 일정 시간 증가시켜 이동 체감 속도를 낮춘다.
    public class HoneySlowStatus : MonoBehaviour, ICombatStatus
    {
        private float statusExpires;
        public CombatStatusTag ActiveStatusTags => IsActive && Time.time < statusExpires ? CombatStatusTag.Slow : CombatStatusTag.None;
        private void EnsureAugmentVisual()
        {
            if (!SyringeAugmentVfx.IsLiving(this)) return;
            var renderer = SyringeAugmentVfx.FindTarget(this);
            HoneyTrailEmitter.Touch(renderer);
        }
        private Rigidbody2D targetRigidbody;
        private Coroutine slowCoroutine;
        public bool IsActive => slowCoroutine != null;
        // Script-driven bosses cannot use Rigidbody drag as their locomotion speed.
        public float SpeedMultiplier { get; private set; } = 1f;

        private bool hasOriginalDrag = false;
        private float originalDrag;

        // Field pacing and movement buffs can change the base while honey and ice coexist.
        // Honey restores the most recent base; the ice layer continues to own its final multiplier.
        internal static void SetUnslowedDrag(Rigidbody2D body, float drag)
        {
            var honey = body.GetComponent<HoneySlowStatus>();
            if (honey != null && honey.hasOriginalDrag)
            {
                honey.originalDrag = drag;
                drag /= honey.SpeedMultiplier * honey.SpeedMultiplier;
            }
            IceChillStatus.SetBaseDrag(body, drag);
        }

        private void Awake()
        {
            targetRigidbody = GetComponent<Rigidbody2D>();

            if (targetRigidbody == null)
            {
                targetRigidbody = GetComponentInParent<Rigidbody2D>();
            }
        }

        public void Apply(float duration, float slowMultiplier, bool showHoneyTrail = true, Ver4CombatSnapshot snapshot = null)
        {
            if (MonsterNeedleBarrier.Blocks(this)) return;
            if (targetRigidbody == null)
            {
                targetRigidbody = GetComponent<Rigidbody2D>();

                if (targetRigidbody == null)
                {
                    targetRigidbody = GetComponentInParent<Rigidbody2D>();
                }
            }

            if (targetRigidbody == null)
            {
                return;
            }

            // slowMultiplier는 0에 가까울수록 강한 둔화, 1이면 둔화 없음
            slowMultiplier = Mathf.Clamp(slowMultiplier, 0.05f, 1f);
            SpeedMultiplier = slowMultiplier;

            if (!hasOriginalDrag)
            {
                originalDrag = IceChillStatus.UnmodifiedDrag(targetRigidbody);
                hasOriginalDrag = true;
            }

            // 이 프로젝트의 몬스터는 rb.drag로 최고 속도에 가까운 움직임을 제어한다.
            // drag를 높이면 같은 acceleration을 받아도 이동 속도가 낮아진다.
            // 속도 체감 비율을 맞추기 위해 제곱 반비례로 drag를 증가시킨다.
            IceChillStatus.SetBaseDrag(targetRigidbody, originalDrag / (slowMultiplier * slowMultiplier));

            if (slowCoroutine != null)
            {
                StopCoroutine(slowCoroutine);
            }

            statusExpires = Time.time + Mathf.Max(0, duration);
            slowCoroutine = StartCoroutine(SlowRoutine(duration));
            if(showHoneyTrail)EnsureAugmentVisual();
            if (duration > 0 && slowMultiplier < 1f && snapshot != null)
            {
                snapshot.Impact(transform.position,this,Parent.Honey,0);
                snapshot.Impact(transform.position,this,Parent.Honey,1);
            }
        }

        private IEnumerator SlowRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);

            RestoreOriginalDrag();
        }

        private void RestoreOriginalDrag()
        {
            if (hasOriginalDrag)
            {
                hasOriginalDrag = false;
                if (targetRigidbody != null) IceChillStatus.SetBaseDrag(targetRigidbody, originalDrag);
            }

            slowCoroutine = null;
            SpeedMultiplier = 1f;
        }

        private void OnDisable()
        {
            if (slowCoroutine != null)
            {
                StopCoroutine(slowCoroutine);
                slowCoroutine = null;
            }

            RestoreOriginalDrag();
        }

        private void OnDestroy()
        {
            RestoreOriginalDrag();
        }
    }
}
