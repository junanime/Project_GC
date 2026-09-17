using UnityEngine;

namespace Vampire
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public class PlayerTrapBindRuntime : MonoBehaviour
    {
        private Character ownerCharacter;
        private Rigidbody2D cachedRigidbody;

        private TrapMonster currentTrap;
        private bool isBound = false;
        private Vector3 lockedWorldPosition;
        private SpriteRenderer visualRenderer;
        private Sprite savedSprite;
        private bool lockedFlip;
        private Vector2 lockedLook;
        private float visualElapsed;

        public SpriteRenderer VisualRenderer => visualRenderer;

        public bool IsBound => isBound;
        public TrapMonster CurrentTrap => currentTrap;

        public static PlayerTrapBindRuntime GetOrCreate(Character character)
        {
            if (character == null)
            {
                return null;
            }

            PlayerTrapBindRuntime runtime = character.GetComponent<PlayerTrapBindRuntime>();

            if (runtime == null)
            {
                runtime = character.gameObject.AddComponent<PlayerTrapBindRuntime>();
            }

            runtime.Init(character);
            return runtime;
        }

        public void Init(Character character)
        {
            ownerCharacter = character;
            var animator = character.GetComponentInChildren<SpriteAnimator>();
            if (animator != null) visualRenderer = animator.GetComponent<SpriteRenderer>();

            if (cachedRigidbody == null)
            {
                cachedRigidbody = character.GetComponent<Rigidbody2D>();
            }
        }

        public bool TryBind(TrapMonster trap, Vector3 worldPosition)
        {
            if (trap == null)
            {
                return false;
            }

            if (isBound && currentTrap != null && currentTrap != trap)
            {
                return false;
            }

            if (isBound && currentTrap == trap) return true;
            visualElapsed = 0f;
            lockedLook = ownerCharacter != null ? ownerCharacter.LookDirection : Vector2.right;
            if (visualRenderer != null)
            {
                savedSprite = visualRenderer.sprite;
                lockedFlip = visualRenderer.flipX;
            }

            currentTrap = trap;
            isBound = true;
            lockedWorldPosition = worldPosition;

            ForceLockNow();

            ApplyCapturedVisual();
            Debug.Log("[PlayerTrapBindRuntime] 플레이어 구속 시작");
            return true;
        }

        public void Release(TrapMonster trap)
        {
            if (!isBound)
            {
                return;
            }

            if (trap != null && currentTrap != trap)
            {
                return;
            }

            isBound = false;
            currentTrap = null;
            RestoreVisual();

            if (cachedRigidbody != null)
            {
                cachedRigidbody.velocity = Vector2.zero;
                cachedRigidbody.angularVelocity = 0f;
            }

            Debug.Log("[PlayerTrapBindRuntime] 플레이어 구속 해제");
        }

        private void LateUpdate()
        {
            if (!isBound)
            {
                return;
            }

            if (currentTrap == null || !currentTrap.gameObject.activeInHierarchy ||
                (ownerCharacter != null && ownerCharacter.CurrentHealth <= 0f))
            {
                Release(null);
                return;
            }
            ForceLockNow();
            ApplyCapturedVisual();
            visualElapsed += Time.deltaTime;
        }

        private void ApplyCapturedVisual()
        {
            if (ownerCharacter != null) ownerCharacter.LookDirection = lockedLook;
            if (visualRenderer == null) return;
            visualRenderer.flipX = lockedFlip;
            var data = ownerCharacter != null ? ownerCharacter.Blueprint : null;
            if (data == null || data.capturedSpriteSequence == null || data.capturedSpriteSequence.Length == 0) return;
            int frame = Mathf.Min(Mathf.FloorToInt(visualElapsed / Mathf.Max(0.01f, data.capturedFrameTime)),
                data.capturedSpriteSequence.Length - 1);
            visualRenderer.sprite = data.capturedSpriteSequence[frame];
        }

        private void RestoreVisual()
        {
            if (visualRenderer != null && savedSprite != null) visualRenderer.sprite = savedSprite;
            savedSprite = null;
            if (ownerCharacter != null && ownerCharacter.gameObject.activeInHierarchy)
            {
                ownerCharacter.StopWalkAnimation();
                ownerCharacter.StartIdleAnimation();
            }
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
            if (isBound) RestoreVisual();
            isBound = false;
            currentTrap = null;
        }
    }
}
