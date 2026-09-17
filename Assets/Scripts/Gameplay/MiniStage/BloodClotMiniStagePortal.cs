using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class BloodClotMiniStagePortal : MonoBehaviour
    {
        [Header("Portal References")]
        [Tooltip("이 포탈과 연결된 미니 스테이지 관리자입니다. 포탈 생성 시 MiniStageDirector가 자동으로 넣어줍니다.")]
        [SerializeField] private MiniStageDirector miniStageDirector;

        [Tooltip("플레이어가 포탈 범위 안에 들어왔을 때 보여줄 안내 UI입니다. 예: 'E 입장'. 없어도 작동합니다.")]
        [SerializeField] private GameObject interactionGuide;

        [Header("Interaction")]
        [Tooltip("미니 스테이지에 입장할 때 사용할 키입니다.")]
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        [Tooltip("입장 후 포탈 오브젝트를 삭제할지 여부입니다. 보통 true가 좋습니다.")]
        [SerializeField] private bool destroyOnEnter = true;

        [Tooltip("포탈 상호작용 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();
        private bool playerInside => playerColliders.Count > 0;
        private bool consumed = false;
        public bool CanInteract => isActiveAndEnabled && !consumed && playerInside &&
            miniStageDirector != null && miniStageDirector.CanEnterFromPortal;

        private void RefreshGuide()
        {
            PixelInteractionPrompt.Show(this, CanInteract, interactionGuide);
        }

        private void OnDisable()
        {
            playerColliders.Clear();
            PixelInteractionPrompt.Show(this, false, interactionGuide);
        }

        public void Setup(MiniStageDirector director)
        {
            miniStageDirector = director;
            consumed = false;
            RefreshGuide();

            if (debugLog)
            {
                Debug.Log("[BloodClotMiniStagePortal] 포탈 Setup 완료.");
            }
        }

        private void Awake()
        {
            BloodClotObstacle.Ensure(gameObject);
            RefreshGuide();
        }

        private void Update()
        {
            playerColliders.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            RefreshGuide();
            if (!CanInteract || Time.timeScale <= 0f) return;
            if (consumed)
            {
                return;
            }

            if (!playerInside)
            {
                return;
            }

            if (Input.GetKeyDown(interactionKey))
            {
                TryEnterMiniStage();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (consumed)
            {
                return;
            }

            Character character = other.GetComponentInParent<Character>();

            if (character == null)
            {
                return;
            }

            playerColliders.Add(other);

            RefreshGuide();

            if (debugLog)
            {
                Debug.Log("[BloodClotMiniStagePortal] 플레이어가 포탈 범위에 들어왔습니다. E키로 입장 가능.");
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

            RefreshGuide();

            if (debugLog)
            {
                Debug.Log("[BloodClotMiniStagePortal] 플레이어가 포탈 범위에서 벗어났습니다.");
            }
        }

        private void TryEnterMiniStage()
        {
            if (!CanInteract || Time.timeScale <= 0f) return;
            if (miniStageDirector == null)
            {
                Debug.LogWarning("[BloodClotMiniStagePortal] MiniStageDirector가 없어 미니 스테이지에 입장할 수 없습니다.");
                return;
            }

            if (debugLog)
            {
                Debug.Log("[BloodClotMiniStagePortal] E 입력 확인. 미니 스테이지 입장 요청.");
            }

            miniStageDirector.EnterMiniStageFromPortal(this);
        }

        public void Consume()
        {
            consumed = true;
            playerColliders.Clear();

            RefreshGuide();

            if (destroyOnEnter)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}