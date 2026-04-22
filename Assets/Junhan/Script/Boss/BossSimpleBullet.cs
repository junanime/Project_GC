using UnityEngine;

namespace Vampire
{
    public class BossSimpleBullet : MonoBehaviour
    {
        [SerializeField] private float lifeTime = 5f;

        private Vector2 direction;
        private float speed;
        private float damage;

        public void Init(Vector2 direction, float speed, float damage)
        {
            this.direction = direction.normalized;
            this.speed = speed;
            this.damage = damage;

            Destroy(gameObject, lifeTime);
        }

        private void Update()
        {
            transform.position += (Vector3)(direction * speed * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<IDamageable>(out IDamageable damageable))
            {
                if (damageable is Character character)
                {
                    character.TakeDamage(damage);
                    Destroy(gameObject);
                }
            }
        }
    }
}