using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 신경차단에 걸린 몬스터에게 붙는 런타임 상태.
    /// Rigidbody2D 속도만 0으로 두면 몬스터 이동 스크립트가 다시 움직일 수 있으므로,
    /// 정지 시간 동안 Monster 컴포넌트 자체도 잠시 비활성화한다.
    /// </summary>
    public class NeuralBlockedMonsterStatus : MonoBehaviour
    {
        private Monster monster;
        private Rigidbody2D rb;
        private Animator[] animators;
        private float[] originalAnimatorSpeeds;

        private float endTime = -1f;
        private bool initialized = false;

        private bool capturedMonsterEnabled = false;
        private bool originalMonsterEnabled = true;

        public void Apply(float duration)
        {
            if (!initialized)
            {
                CacheReferences();
            }

            endTime = Mathf.Max(endTime, Time.time + Mathf.Max(0.05f, duration));

            DisableMonsterMovementComponent();
            SetAnimatorSpeed(0f);
            StopMovement();
        }

        private void CacheReferences()
        {
            monster = GetComponent<Monster>() ?? GetComponentInParent<Monster>();
            rb = GetComponent<Rigidbody2D>() ?? GetComponentInParent<Rigidbody2D>();
            animators = GetComponentsInChildren<Animator>(true);

            if (animators != null && animators.Length > 0)
            {
                originalAnimatorSpeeds = new float[animators.Length];

                for (int i = 0; i < animators.Length; i++)
                {
                    originalAnimatorSpeeds[i] = animators[i] != null ? animators[i].speed : 1f;
                }
            }

            initialized = true;
        }

        private void Update()
        {
            if (Time.time >= endTime)
            {
                RestoreMonsterMovementComponent();
                RestoreAnimatorSpeed();
                Destroy(this);
                return;
            }

            DisableMonsterMovementComponent();
            StopMovement();
        }

        private void FixedUpdate()
        {
            if (Time.time < endTime)
            {
                StopMovement();
            }
        }

        private void DisableMonsterMovementComponent()
        {
            if (monster == null)
            {
                return;
            }

            if (!capturedMonsterEnabled)
            {
                originalMonsterEnabled = monster.enabled;
                capturedMonsterEnabled = true;
            }

            if (monster.enabled)
            {
                monster.enabled = false;
            }
        }

        private void RestoreMonsterMovementComponent()
        {
            if (monster == null)
            {
                return;
            }

            if (!capturedMonsterEnabled)
            {
                return;
            }

            monster.enabled = originalMonsterEnabled;
        }

        private void StopMovement()
        {
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        private void SetAnimatorSpeed(float speed)
        {
            if (animators == null)
            {
                return;
            }

            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                {
                    animators[i].speed = speed;
                }
            }
        }

        private void RestoreAnimatorSpeed()
        {
            if (animators == null || originalAnimatorSpeeds == null)
            {
                return;
            }

            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                {
                    animators[i].speed = originalAnimatorSpeeds[i];
                }
            }
        }

        private void OnDestroy()
        {
            RestoreMonsterMovementComponent();
            RestoreAnimatorSpeed();
        }
    }
}