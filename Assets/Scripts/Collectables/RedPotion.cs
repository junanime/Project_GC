using UnityEngine;

namespace Vampire
{
    public class RedPotion : Collectable
    {

        protected override void OnCollected()
        {
            var level=FindObjectOfType<LevelManager>();
            if(level!=null)level.ActivateSpawnPotion();

            // RedPotion을 실제 획득하여
            // 스폰 증가 효과가 시작된 순간 1회만 재생합니다.
            GameAudioManager.PlaySfx(
                GameAudioManager.GameSfxId.RedPotionPickup
            );
            Destroy(gameObject);
        }

    }
}
