using UnityEngine;

namespace Vampire
{
    public class Bomb : Collectable
    {
        [SerializeField] protected float bombDamage;

        protected override void OnCollected()
        {
            ThrowAt(BombTargeting.AutomaticTarget(entityManager,playerCharacter,Camera.main));
        }
        public bool ThrowAt(Vector2 target)
        {
            if(playerCharacter==null||!playerCharacter.IsAlive||Time.timeScale<=0)return false;
            var sprite=GetComponentInChildren<SpriteRenderer>(true)?.sprite;
            ThrownConsumableBomb.Launch(playerCharacter,entityManager,target,bombDamage,sprite);
            Destroy(gameObject);return true;
        }
    }
}
