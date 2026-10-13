using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 필드 상호작용 오브젝트 공통 기반.
    ///
    /// 개선점:
    /// - 여러 상호작용 오브젝트 범위가 겹쳐도 가장 가까운 1개만 E키에 반응한다.
    /// - 포커스된 오브젝트만 안내 UI를 표시한다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public abstract class InteractableEventObject : MonoBehaviour
    {
        private static readonly List<InteractableEventObject> candidates = new List<InteractableEventObject>();
        private static InteractableEventObject focusedObject;
        private static Character focusedPlayer;
        private static int lastInteractionFrame = -1;

        [Header("Interaction")]
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        [Tooltip("true면 한 번 상호작용 후 다시 사용할 수 없습니다.")]
        [SerializeField] private bool interactOnce = true;

        [Tooltip("상호작용 성공 후 이 오브젝트를 비활성화합니다.")]
        [SerializeField] private bool disableObjectAfterInteract = true;

        [Tooltip("상호작용 성공 후 Collider를 끕니다.")]
        [SerializeField] private bool disableColliderAfterInteract = true;

        [Header("Prompt UI")]
        [Tooltip("플레이어가 가까이 왔을 때 켜질 안내 오브젝트입니다. 비워도 작동합니다.")]
        [SerializeField] private GameObject promptRoot;




        [Header("References")]
        [SerializeField] protected LevelManager levelManager;

        [Header("Debug")]
        [SerializeField] protected bool debugLog = true;

        private Collider2D interactionCollider;
        private Character currentPlayer;
        private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();
        private bool playerInside => playerColliders.Count > 0;
        private bool alreadyInteracted;

        protected Character CurrentPlayer => currentPlayer;
        protected virtual bool KeepVisibleAfterInteraction => false;
        protected virtual bool AllowMiniStageInteraction => false;
        protected virtual bool InteractionAvailable => true;
        protected virtual Component PromptOwner => this;
        protected void ConfigureReusableInteraction()
        {
            interactOnce=false;disableObjectAfterInteract=false;disableColliderAfterInteract=false;
        }

        protected void ResetInteractionAvailability()
        {
            alreadyInteracted = false;
            if (interactionCollider != null) interactionCollider.enabled = true;
        }

        protected virtual void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();

            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        protected virtual void Awake()
        {
            InteractionFocus.Register(this, () => CanBeFocusedBy(currentPlayer));
            interactionCollider = GetComponent<Collider2D>();

            if (interactionCollider != null)
            {
                interactionCollider.isTrigger = true;
            }

            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }

            SetPromptVisible(false);
        }

        protected virtual void OnDisable()
        {
            playerColliders.Clear();
            currentPlayer = null;
            RemoveCandidate(this);

            if (focusedObject == this)
            {
                ClearFocus();
                RefreshFocus(focusedPlayer);
            }

            SetPromptVisible(false);
        }

        protected virtual void Update()
        {
            if (Time.timeScale <= 0f) { SetPromptVisible(false); return; }
            playerColliders.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            if (playerInside && currentPlayer != null)
            {
                AddCandidate(this);
                RefreshFocus(currentPlayer);
            }

            if (focusedObject != this)
            {
                return;
            }

            if (!CanBeFocusedBy(currentPlayer))
            {
                RemoveCandidate(this);
                ClearFocus();
                RefreshFocus(currentPlayer);
                return;
            }

            if (Vampire.GameInput.TryConsumeInteraction(interactionKey, this))
            {
                TryInteract();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Character character = other.GetComponentInParent<Character>();

            if (character == null || other == character.CollectableCollider)
            {
                return;
            }

            currentPlayer = character;
            playerColliders.Add(other);

            AddCandidate(this);
            RefreshFocus(character);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Character character = other.GetComponentInParent<Character>();

            if (character == null || character != currentPlayer)
            {
                return;
            }

            playerColliders.Remove(other);
            if (playerInside) { RefreshFocus(character); return; }
            RemoveCandidate(this);
            currentPlayer = null;
            SetPromptVisible(false);

            if (focusedObject == this)
            {
                ClearFocus();
                RefreshFocus(character);
            }
        }

        public void TryInteract()
        {
            if (Time.timeScale <= 0f) return;
            if (!InteractionFocus.IsFocused(this)) return;
            if (MiniStageRuntimeState.IsInsideMiniStage && !AllowMiniStageInteraction)
            {
                return;
            }

            if (focusedObject != this)
            {
                return;
            }

            if (!CanBeFocusedBy(currentPlayer))
            {
                return;
            }

            if(lastInteractionFrame==Time.frameCount)return;

            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }

            bool success = ExecuteInteraction(currentPlayer);

            if (!success)
            {
                return;
            }
            lastInteractionFrame=Time.frameCount;

            CompleteInteraction();
        }

        protected bool HasInteracted => alreadyInteracted;

        // Shared by player interaction and automatic event activation.
        protected void CompleteInteraction()
        {
            alreadyInteracted = true;

            RemoveCandidate(this);

            if (focusedObject == this)
            {
                ClearFocus();
            }

            SetPromptVisible(false);

            if (disableColliderAfterInteract && interactionCollider != null)
            {
                interactionCollider.enabled = false;
            }

            if (disableObjectAfterInteract && !KeepVisibleAfterInteraction)
            {
                gameObject.SetActive(false);
            }

            RefreshFocus(currentPlayer);
        }

        protected abstract bool ExecuteInteraction(Character player);

        private bool CanBeFocusedBy(Character player)
        {
            if (!InteractionAvailable || (MiniStageRuntimeState.IsInsideMiniStage && !AllowMiniStageInteraction))
            {
                return false;
            }

            if (player == null)
            {
                return false;
            }

            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                return false;
            }

            if (!playerInside || currentPlayer != player)
            {
                return false;
            }

            if (interactOnce && alreadyInteracted)
            {
                return false;
            }

            return true;
        }

        private static void AddCandidate(InteractableEventObject interactable)
        {
            if (interactable == null)
            {
                return;
            }

            if (!candidates.Contains(interactable))
            {
                candidates.Add(interactable);
            }
        }

        private static void RemoveCandidate(InteractableEventObject interactable)
        {
            if (interactable == null)
            {
                return;
            }

            candidates.Remove(interactable);

            if (focusedObject == interactable)
            {
                ClearFocus();
            }
        }

        private static void RefreshFocus(Character player)
        {
            if (player == null)
            {
                ClearFocus();
                return;
            }

            focusedPlayer = player;

            InteractableEventObject nearest = null;

            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                InteractableEventObject candidate = candidates[i];

                if (candidate == null)
                {
                    candidates.RemoveAt(i);
                    continue;
                }

                if (!candidate.CanBeFocusedBy(player))
                {
                    candidate.SetPromptVisible(false);
                    candidates.RemoveAt(i);
                    continue;
                }

                if (InteractionFocus.IsFocused(candidate))
                {
                    nearest = candidate;
                }
            }

            if (focusedObject == nearest)
            {
                if (focusedObject != null)
                {
                    focusedObject.SetPromptVisible(true);
                }

                return;
            }

            if (focusedObject != null)
            {
                focusedObject.SetPromptVisible(false);
            }

            focusedObject = nearest;

            if (focusedObject != null)
            {
                focusedObject.SetPromptVisible(true);
            }
        }

        private static void ClearFocus()
        {
            if (focusedObject != null)
            {
                focusedObject.SetPromptVisible(false);
            }

            focusedObject = null;
        }

        protected void SetPromptVisible(bool visible)
        {
            PixelInteractionPrompt.Show(PromptOwner, visible && isActiveAndEnabled, promptRoot, this);
        }
    }
}
