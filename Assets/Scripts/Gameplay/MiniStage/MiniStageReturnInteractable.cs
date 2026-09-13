using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class MiniStageReturnInteractable : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("미니 스테이지 진행을 관리하는 MiniStageDirector입니다.")]
        [SerializeField] private MiniStageDirector miniStageDirector;

        [Tooltip("상호작용 가능 상태일 때 보여줄 안내 UI입니다. 예: 'E 복귀'. 없어도 작동합니다.")]
        [SerializeField] private GameObject unlockedGuide;

        [Tooltip("아직 상호작용 불가능할 때 보여줄 잠금 안내 UI입니다. 예: '스나이퍼를 모두 처치하고 보상을 획득하세요'. 없어도 작동합니다.")]
        [SerializeField] private GameObject lockedGuide;

        [Header("Interaction")]
        [Tooltip("원래 필드로 돌아갈 때 사용할 키입니다.")]
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        [Tooltip("상호작용 가능한 상태로 시작할지 여부입니다. 미니 스테이지 중앙 혈전은 false로 두는 것이 좋습니다.")]
        [SerializeField] private bool startUnlocked = false;

        [Tooltip("귀환 오브젝트 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private bool unlocked;
        private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();
        private bool playerInside => playerColliders.Count > 0;

        [Header("Portal Visual")]
        [SerializeField] private SpriteRenderer portalRenderer;
        [SerializeField] private Sprite lockedSprite;
        [SerializeField] private Sprite unlockedSprite;

        public bool IsUnlocked => unlocked;
        public bool CanInteract => isActiveAndEnabled && unlocked && playerInside &&
            miniStageDirector != null && miniStageDirector.CanReturnFromInteractable(this);

        private void OnDisable()
        {
            playerColliders.Clear();
            SetGuideVisible(false);
        }

        private void Awake()
        {
            if (miniStageDirector == null)
            {
                miniStageDirector = FindObjectOfType<MiniStageDirector>();
            }

            SetUnlocked(startUnlocked);
            SetGuideVisible(false);
        }

        private void Update()
        {
            playerColliders.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            SetGuideVisible(CanInteract);
            if (!CanInteract || Time.timeScale <= 0f) return;
            if (!playerInside)
            {
                return;
            }

            if (!unlocked)
            {
                return;
            }

            if (Input.GetKeyDown(interactionKey))
            {
                if (debugLog)
                {
                    Debug.Log("[MiniStageReturnInteractable] E 입력 확인. 원래 필드 복귀 요청.");
                }

                if (miniStageDirector != null)
                {
                    miniStageDirector.ReturnToFieldFromInteractable(this);
                }
            }
        }

        public void SetUnlocked(bool value)
        {
            unlocked = value;
            if (portalRenderer == null) portalRenderer = GetComponentInChildren<SpriteRenderer>(true);
            Sprite sprite = unlocked ? unlockedSprite : lockedSprite;
            if (portalRenderer != null && sprite != null) portalRenderer.sprite = sprite;

            if (debugLog)
            {
                Debug.Log($"[MiniStageReturnInteractable] 상호작용 가능 상태 변경: {unlocked}");
            }

            if (playerInside)
            {
                SetGuideVisible(true);
            }
            else
            {
                SetGuideVisible(false);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Character character = other.GetComponentInParent<Character>();

            if (character == null)
            {
                return;
            }

            playerColliders.Add(other);
            SetGuideVisible(true);

            if (debugLog)
            {
                if (unlocked)
                {
                    Debug.Log("[MiniStageReturnInteractable] 플레이어 진입. E키로 복귀 가능.");
                }
                else
                {
                    Debug.Log("[MiniStageReturnInteractable] 플레이어 진입. 아직 복귀 조건이 충족되지 않았습니다.");
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Character character = other.GetComponentInParent<Character>();

            if (character == null)
            {
                return;
            }

            playerColliders.Remove(other);
            SetGuideVisible(CanInteract);
        }

        private void SetGuideVisible(bool visible)
        {
            if (lockedGuide != null && lockedGuide != gameObject) lockedGuide.SetActive(false);
            PixelInteractionPrompt.Show(this, visible && CanInteract, unlockedGuide);
        }
    }
}
