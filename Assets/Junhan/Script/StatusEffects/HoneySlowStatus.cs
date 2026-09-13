using System.Collections;
using UnityEngine;

namespace Vampire
{
    // 꿀침 상태이상
    // 몬스터의 Rigidbody2D.drag를 일정 시간 증가시켜 이동 체감 속도를 낮춘다.
    public class HoneySlowStatus : MonoBehaviour
    {
        private SyringeAugmentVfx augmentVisual;
        private void EnsureAugmentVisual()
        {
            if (augmentVisual != null || !SyringeAugmentVfx.IsLiving(this)) return;
            var renderer = SyringeAugmentVfx.FindTarget(this);
            if (renderer != null) augmentVisual = SyringeAugmentVfx.Play("Honey", renderer.bounds.center, renderer);
        }
        private void ReleaseAugmentVisual()
        {
            if (augmentVisual != null) augmentVisual.Release();
            augmentVisual = null;
        }
        private void LateUpdate()
        {
            if (!SyringeAugmentVfx.IsLiving(this)) ReleaseAugmentVisual();
        }
        private Rigidbody2D targetRigidbody;
        private Coroutine slowCoroutine;

        private bool hasOriginalDrag = false;
        private float originalDrag;

        private void Awake()
        {
            targetRigidbody = GetComponent<Rigidbody2D>();

            if (targetRigidbody == null)
            {
                targetRigidbody = GetComponentInParent<Rigidbody2D>();
            }
        }

        public void Apply(float duration, float slowMultiplier)
        {
            if (targetRigidbody == null)
            {
                targetRigidbody = GetComponent<Rigidbody2D>();

                if (targetRigidbody == null)
                {
                    targetRigidbody = GetComponentInParent<Rigidbody2D>();
                }
            }

            if (targetRigidbody == null)
            {
                return;
            }

            // slowMultiplier는 0에 가까울수록 강한 둔화, 1이면 둔화 없음
            slowMultiplier = Mathf.Clamp(slowMultiplier, 0.05f, 1f);

            if (!hasOriginalDrag)
            {
                originalDrag = targetRigidbody.drag;
                hasOriginalDrag = true;
            }

            // 이 프로젝트의 몬스터는 rb.drag로 최고 속도에 가까운 움직임을 제어한다.
            // drag를 높이면 같은 acceleration을 받아도 이동 속도가 낮아진다.
            // 속도 체감 비율을 맞추기 위해 제곱 반비례로 drag를 증가시킨다.
            targetRigidbody.drag = originalDrag / (slowMultiplier * slowMultiplier);

            if (slowCoroutine != null)
            {
                StopCoroutine(slowCoroutine);
            }

            slowCoroutine = StartCoroutine(SlowRoutine(duration));
            EnsureAugmentVisual();
        }

        private IEnumerator SlowRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);

            RestoreOriginalDrag();
        }

        private void RestoreOriginalDrag()
        {
            ReleaseAugmentVisual();
            if (targetRigidbody != null && hasOriginalDrag)
            {
                targetRigidbody.drag = originalDrag;
            }

            slowCoroutine = null;
        }

        private void OnDisable()
        {
            if (slowCoroutine != null)
            {
                StopCoroutine(slowCoroutine);
                slowCoroutine = null;
            }

            RestoreOriginalDrag();
        }

        private void OnDestroy()
        {
            RestoreOriginalDrag();
        }
    }
}