using UnityEngine;

namespace Vampire
{
    public class BossSimpleBullet : MonoBehaviour
    {
        [Header("Bullet Settings")]
        [Tooltip("탄환이 자동으로 사라지기까지 걸리는 시간입니다.")]
        [SerializeField] private float lifeTime = 5f;

        [Tooltip("탄환이 날아가면서 회전하는 속도입니다. 0이면 회전하지 않습니다.")]
        [SerializeField] private float rotationSpeed = 0f;

        [Tooltip("체크하면 플레이어에게 맞았을 때 탄환이 파괴됩니다.")]
        [SerializeField] private bool destroyOnHit = true;

        [Header("Debug")]
        [Tooltip("체크하면 플레이어에게 데미지를 줬을 때 Console에 로그를 출력합니다.")]
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
            if (MiniStageRuntimeState.IsInsideMiniStage) { Destroy(gameObject); return; }
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
            if (MiniStageRuntimeState.IsInsideMiniStage) return;
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
                Debug.Log($"[BossSimpleBullet] Player hit / damage={damage}");
            }

            if (destroyOnHit)
            {
                Destroy(gameObject);
            }
        }
    }
}