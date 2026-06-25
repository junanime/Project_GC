using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 신경차단에 걸린 몬스터에게 붙는 런타임 상태.
    /// 몬스터의 Rigidbody2D 속도를 0으로 고정하고, Animator가 있으면 잠시 멈춘다.
    /// </summary>
    public class NeuralBlockedMonsterStatus : MonoBehaviour
    {
        private Rigidbody2D rb;
        private Animator[] animators;
        private float[] originalAnimatorSpeeds;

        private float endTime = -1f;
        private bool initialized = false;

        public void Apply(float duration)
        {
            if (!initialized)
            {
                CacheReferences();
            }

            endTime = Mathf.Max(endTime, Time.time + Mathf.Max(0.05f, duration));

            SetAnimatorSpeed(0f);
            StopMovement();
        }

        private void CacheReferences()
        {
            rb = GetComponent<Rigidbody2D>();
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
                RestoreAnimatorSpeed();
                Destroy(this);
                return;
            }

            StopMovement();
        }

        private void FixedUpdate()
        {
            if (Time.time < endTime)
            {
                StopMovement();
            }
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
            RestoreAnimatorSpeed();
        }
    }
}