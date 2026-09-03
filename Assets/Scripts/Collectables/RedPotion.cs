using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class RedPotion : Collectable
    {
        [Tooltip("지속 회복 동안 총 회복할 체력량입니다.")]
        [SerializeField]
        protected float healAmount = 50f;

        [Tooltip("지속 회복 효과가 유지되는 시간(초)입니다.")]
        [SerializeField]
        protected float healTime = 30f;

        protected override void OnCollected()
        {
            gameObject.SetActive(true);

            GetComponentInChildren<SpriteRenderer>().enabled = false;

            // 지속 회복 효과 시작
            StartCoroutine(HealOverTime());

            // RedPotion을 실제 획득하여
            // 지속 회복 효과가 시작된 순간 1회만 재생합니다.
            GameAudioManager.PlaySfx(
                GameAudioManager.GameSfxId.RedPotionPickup
            );
        }

        private IEnumerator HealOverTime()
        {
            float t = 0f;

            while (t < healTime)
            {
                t += Time.deltaTime;

                playerCharacter.GainHealth(
                    Time.deltaTime * healAmount / healTime
                );

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}