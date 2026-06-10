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
        private bool playerInside;

        public bool IsUnlocked => unlocked;

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

            playerInside = true;
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

            playerInside = false;
            SetGuideVisible(false);
        }

        private void SetGuideVisible(bool visible)
        {
            if (unlockedGuide != null)
            {
                unlockedGuide.SetActive(visible && unlocked);
            }

            if (lockedGuide != null)
            {
                lockedGuide.SetActive(visible && !unlocked);
            }
        }
    }
}