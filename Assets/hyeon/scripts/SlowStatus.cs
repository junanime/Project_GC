using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class SlowStatus : MonoBehaviour, ICombatStatus
    {
        private float statusExpires;
        public CombatStatusTag ActiveStatusTags => isSlowed && Time.time < statusExpires ? CombatStatusTag.Slow : CombatStatusTag.None;
        private Monster monster;
        private float originalSpeed;
        private bool isSlowed = false;
        private Coroutine slowCoroutine;

        private void Awake()
        {
            monster = GetComponent<Monster>();
        }

        public void Apply(float duration, float slowPercentage)
        {
            if (monster == null) return;

            // 이미 둔화 상태라면 기존 코루틴을 종료하고 새로 갱신 (리프레시)
            if (isSlowed && slowCoroutine != null)
            {
                StopCoroutine(slowCoroutine);
                // 속도가 중첩되어 0이 되는 것을 막기 위해 원래 속도로 일시 복구
                RestoreSpeed();
            }

            slowCoroutine = StartCoroutine(SlowRoutine(duration, slowPercentage));
        }

        private IEnumerator SlowRoutine(float duration, float slowPercentage)
        {
            statusExpires = Time.time + Mathf.Max(0, duration);
            isSlowed = true;

            //  [주의] Monster.cs 내부의 이동속도 변수명에 맞게 수정해야 합니다!
            // 예: monster.moveSpeed 또는 monster.speed 등
            originalSpeed = monster.moveSpeed;
            monster.moveSpeed = originalSpeed * (1f - slowPercentage);

            yield return new WaitForSeconds(duration);

            RestoreSpeed();
        }

        private void RestoreSpeed()
        {
            if (monster != null && isSlowed)
            {
                monster.moveSpeed = originalSpeed; // 원래 속도로 복구
            }
            isSlowed = false;
            Destroy(this); // 역할이 끝난 컴포넌트는 스스로 삭제 (메모리 관리)
        }

        private void OnDestroy()
        {
            // 컴포넌트가 도중에 파괴되더라도 속도는 복구되도록 안전장치 마련
            if (isSlowed) RestoreSpeed();
        }
    }
}