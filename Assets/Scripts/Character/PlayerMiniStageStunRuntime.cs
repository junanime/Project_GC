using System.Collections;
using UnityEngine;

namespace Vampire
{
    [DisallowMultipleComponent]
    public class PlayerMiniStageStunRuntime : MonoBehaviour
    {
        [Header("Runtime State")]
        [Tooltip("현재 플레이어가 미니 스테이지 기믹에 의해 스턴 상태인지 여부입니다.")]
        [SerializeField] private bool stunned = false;

        [Tooltip("스턴 중 고정할 월드 위치입니다.")]
        [SerializeField] private Vector3 lockedWorldPosition;

        [Tooltip("스턴 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = false;

        private Character ownerCharacter;
        private Rigidbody2D cachedRigidbody;
        private Coroutine stunCoroutine;

        public bool IsStunned => stunned;

        public static PlayerMiniStageStunRuntime GetOrCreate(Character character)
        {
            if (character == null)
            {
                return null;
            }

            PlayerMiniStageStunRuntime runtime = character.GetComponent<PlayerMiniStageStunRuntime>();

            if (runtime == null)
            {
                runtime = character.gameObject.AddComponent<PlayerMiniStageStunRuntime>();
            }

            runtime.Init(character);
            return runtime;
        }

        public void Init(Character character)
        {
            ownerCharacter = character;

            if (cachedRigidbody == null)
            {
                cachedRigidbody = character.GetComponent<Rigidbody2D>();
            }
        }

        public void ApplyStun(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            if (ownerCharacter == null)
            {
                ownerCharacter = GetComponent<Character>();
            }

            if (cachedRigidbody == null)
            {
                cachedRigidbody = GetComponent<Rigidbody2D>();
            }

            lockedWorldPosition = transform.position;

            if (stunCoroutine != null)
            {
                StopCoroutine(stunCoroutine);
            }

            stunCoroutine = StartCoroutine(StunRoutine(duration));
        }

        private IEnumerator StunRoutine(float duration)
        {
            stunned = true;
            ForceLockNow();

            if (ownerCharacter != null)
            {
                ownerCharacter.Move(Vector2.zero);
                ownerCharacter.StopWalkAnimation();
            }

            if (debugLog)
            {
                Debug.Log($"[PlayerMiniStageStunRuntime] 스턴 시작. duration={duration:0.00}");
            }

            float timer = 0f;

            while (timer < duration)
            {
                ForceLockNow();
                timer += Time.deltaTime;
                yield return null;
            }

            stunned = false;
            stunCoroutine = null;

            if (cachedRigidbody != null)
            {
                cachedRigidbody.velocity = Vector2.zero;
                cachedRigidbody.angularVelocity = 0f;
            }

            if (debugLog)
            {
                Debug.Log("[PlayerMiniStageStunRuntime] 스턴 종료.");
            }
        }

        private void FixedUpdate()
        {
            if (!stunned)
            {
                return;
            }

            ForceLockNow();
        }

        private void LateUpdate()
        {
            if (!stunned)
            {
                return;
            }

            ForceLockNow();
        }

        private void ForceLockNow()
        {
            transform.position = lockedWorldPosition;

            if (cachedRigidbody != null)
            {
                cachedRigidbody.velocity = Vector2.zero;
                cachedRigidbody.angularVelocity = 0f;
                cachedRigidbody.position = lockedWorldPosition;
            }
        }

        private void OnDisable()
        {
            stunned = false;

            if (stunCoroutine != null)
            {
                StopCoroutine(stunCoroutine);
                stunCoroutine = null;
            }
        }
    }
}