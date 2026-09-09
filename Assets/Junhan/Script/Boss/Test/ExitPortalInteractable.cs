using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// Exit Portal의 플레이어 접근 감지와 E 상호작용,
    /// 실제 Scene -> Scene 런 상태 전달 테스트를 담당합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExitPortalInteractable :
        MonoBehaviour
    {
        [Header("Interaction Guide")]
        [Tooltip(
            "플레이어가 상호작용 거리 안에 들어왔을 때 보여줄 안내 UI입니다. " +
            "현재 제작한 키보드 E 키캡 안내 오브젝트를 연결하세요.")]
        [SerializeField]
        private GameObject interactionGuide;

        [Header("Interaction")]
        [Tooltip(
            "Exit Portal 상호작용에 사용할 키입니다. " +
            "현재 프로젝트의 다른 포탈과 동일하게 기본값은 E입니다.")]
        [SerializeField]
        private KeyCode interactionKey =
            KeyCode.E;

        [Tooltip(
            "체크되어 있을 때만 플레이어 접근 및 E 입력을 받습니다.")]
        [SerializeField]
        private bool interactionEnabled =
            true;

        [Header("Scene Transfer Test")]
        [Tooltip(
            "E 입력 시 실제로 로드할 Scene 이름입니다. " +
            "현재 Build Settings의 Level 1 Scene은 정확히 'Level 1'입니다.")]
        [SerializeField]
        private string targetSceneName =
            "Level 1";

        [Tooltip(
            "체크하면 E 입력 시 현재 런 상태를 저장하고 " +
            "Target Scene을 실제로 LoadScene합니다.")]
        [SerializeField]
        private bool loadSceneOnInteract =
            true;

        [Header("Debug")]
        [Tooltip(
            "플레이어 진입/이탈, E 입력 및 Scene 전환 로그를 출력합니다.")]
        [SerializeField]
        private bool debugLog =
            true;

        private readonly HashSet<int>
            playerColliderIds =
                new HashSet<int>();

        private bool transitionStarted;

        /// <summary>
        /// 기존 코드 호환을 위해 유지합니다.
        /// </summary>
        public event Action<ExitPortalInteractable>
            ExitRequested;

        public bool InteractionEnabled =>
            interactionEnabled;

        public bool IsPlayerInside =>
            playerColliderIds.Count > 0;

        private void Awake()
        {
            SetGuideVisible(false);
            ValidateTriggerSetup();
        }

        private void OnDisable()
        {
            playerColliderIds.Clear();
            SetGuideVisible(false);
        }

        private void Update()
        {
            if (transitionStarted ||
                !interactionEnabled ||
                !IsPlayerInside)
            {
                return;
            }

            if (!Input.GetKeyDown(
                    interactionKey))
            {
                return;
            }

            TryRequestExit();
        }

        private void TryRequestExit()
        {
            if (transitionStarted)
            {
                return;
            }

            if (debugLog)
            {
                Debug.Log(
                    "[ExitPortalInteractable] " +
                    "E 입력 확인. 탈출 요청.",
                    this);
            }

            // 기존 외부 이벤트 구조는 그대로 유지합니다.
            ExitRequested?.Invoke(this);

            if (!loadSceneOnInteract)
            {
                if (debugLog)
                {
                    Debug.Log(
                        "[ExitPortalInteractable] " +
                        "Load Scene On Interact가 꺼져 있어 " +
                        "이벤트만 발생시키고 종료합니다.",
                        this);
                }

                return;
            }

            transitionStarted =
                true;

            SetInteractionEnabled(false);

            bool started =
                RunSceneTransferTestService
                    .CaptureCurrentRunAndLoad(
                        targetSceneName);

            if (started)
            {
                return;
            }

            // 저장 또는 Scene Load 준비에 실패한 경우
            // Portal을 다시 사용할 수 있게 되돌립니다.
            transitionStarted =
                false;

            SetInteractionEnabled(true);

            Debug.LogError(
                "[ExitPortalInteractable] " +
                "Scene 전환 준비에 실패했습니다.",
                this);
        }

        public void SetInteractionEnabled(
            bool value)
        {
            interactionEnabled =
                value;

            SetGuideVisible(
                interactionEnabled &&
                IsPlayerInside);

            if (debugLog)
            {
                Debug.Log(
                    $"[ExitPortalInteractable] " +
                    $"상호작용 가능 상태 변경: " +
                    $"{interactionEnabled}",
                    this);
            }
        }

        private void OnTriggerEnter2D(
            Collider2D other)
        {
            if (!interactionEnabled ||
                transitionStarted ||
                other == null)
            {
                return;
            }

            Character character =
                other.GetComponentInParent<Character>();

            if (character == null)
            {
                return;
            }

            bool wasOutside =
                playerColliderIds.Count == 0;

            playerColliderIds.Add(
                other.GetInstanceID());

            if (!wasOutside ||
                playerColliderIds.Count == 0)
            {
                return;
            }

            SetGuideVisible(true);

            if (debugLog)
            {
                Debug.Log(
                    "[ExitPortalInteractable] " +
                    "플레이어가 상호작용 범위에 진입했습니다. " +
                    "E키로 탈출 가능.",
                    this);
            }
        }

        private void OnTriggerExit2D(
            Collider2D other)
        {
            if (other == null)
            {
                return;
            }

            Character character =
                other.GetComponentInParent<Character>();

            if (character == null)
            {
                return;
            }

            playerColliderIds.Remove(
                other.GetInstanceID());

            if (playerColliderIds.Count > 0)
            {
                return;
            }

            SetGuideVisible(false);

            if (debugLog)
            {
                Debug.Log(
                    "[ExitPortalInteractable] " +
                    "플레이어가 상호작용 범위에서 벗어났습니다.",
                    this);
            }
        }

        private void SetGuideVisible(
            bool visible)
        {
            if (interactionGuide == null)
            {
                return;
            }

            interactionGuide.SetActive(
                visible &&
                interactionEnabled &&
                !transitionStarted);
        }

        private void ValidateTriggerSetup()
        {
            Collider2D[] colliders =
                GetComponentsInChildren<Collider2D>(
                    true);

            if (colliders == null ||
                colliders.Length == 0)
            {
                Debug.LogError(
                    "[ExitPortalInteractable] " +
                    "Collider2D가 없습니다. " +
                    "Exit Portal에 CircleCollider2D 등의 Trigger를 추가하세요.",
                    this);

                return;
            }

            for (int i = 0;
                 i < colliders.Length;
                 i++)
            {
                Collider2D collider =
                    colliders[i];

                if (collider != null &&
                    collider.isTrigger)
                {
                    return;
                }
            }

            Debug.LogError(
                "[ExitPortalInteractable] " +
                "Trigger로 설정된 Collider2D가 없습니다. " +
                "상호작용용 Collider2D의 Is Trigger를 체크하세요.",
                this);
        }
    }
}