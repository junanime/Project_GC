using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class BurnStatus : MonoBehaviour
    {
        private Monster monster;
        private Coroutine burnCoroutine;
        private bool isBurning = false;

        private void Awake()
        {
            monster = GetComponent<Monster>();
        }

        public void Apply(float duration, float interval, float damagePerTick)
        {
            if (monster == null) return;

            // 이미 화상 상태라면 기존 화상 루프를 종료하고 새로 리프레시(시간 초기화)
            if (isBurning && burnCoroutine != null)
            {
                StopCoroutine(burnCoroutine);
            }

            burnCoroutine = StartCoroutine(BurnRoutine(duration, interval, damagePerTick));
        }

        private IEnumerator BurnRoutine(float duration, float interval, float damagePerTick)
        {
            isBurning = true;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                yield return new WaitForSeconds(interval);
                elapsed += interval;

                // 몬스터가 아직 살아있을 때만 데미지를 줍니다.
                if (monster != null && monster.HP > 0)
                {
                    //  데미지 텍스트와 물리 넉백 계산을 위해 Monster.cs에 구현된 TakeDamage를 호출합니다.
                    monster.TakePeriodicDamage(damagePerTick, Vector2.zero);
                }
                else
                {
                    break; // 몬스터가 도중에 죽었다면 루프 탈출
                }
            }

            isBurning = false;
            Destroy(this); // 화상이 끝나면 컴포넌트를 스스로 파괴하여 메모리 관리
        }
    }
}