using UnityEngine;

namespace Vampire
{
    public class BossSimpleBullet : MonoBehaviour
    {
        [Header("Bullet Settings")]
        [SerializeField] private float lifeTime = 5f;
        [SerializeField] private float rotationSpeed = 0f;
        [SerializeField] private bool destroyOnHit = true;

        [Header("Debug")]
        [SerializeField] private bool debugLogDamage = false;

        private Vector2 direction = Vector2.zero;
        private float speed = 0f;
        private float damage = 0f;
        private bool initialized = false;

        public void Init(Vector2 direction, float speed, float damage)
        {
            this.direction = direction.normalized;
            this.speed = speed;
            this.damage = damage;
            initialized = true;

            Destroy(gameObject, lifeTime);
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            transform.position += (Vector3)(direction * speed * Time.deltaTime);

            if (rotationSpeed != 0f)
            {
                transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!initialized)
            {
                return;
            }

            Character character = other.GetComponentInParent<Character>();

            if (character == null)
            {
                return;
            }

            character.TakeDamage(damage);

            if (debugLogDamage)
            {
                Debug.Log($"[BossSimpleBullet] Player hit | damage={damage}");
            }

            if (destroyOnHit)
            {
                Destroy(gameObject);
            }
        }
    }
}