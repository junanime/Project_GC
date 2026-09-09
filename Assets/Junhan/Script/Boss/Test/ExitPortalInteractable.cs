using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// Exit Portal의 플레이어 접근 감지와 E 상호작용 입력을 담당합니다.
    ///
    /// 현재 1단계에서는 실제 씬 전환을 수행하지 않고
    /// E 입력 확인 로그와 ExitRequested 이벤트까지만 발생시킵니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExitPortalInteractable : MonoBehaviour
    {
        [Header("Interaction Guide")]
        [Tooltip(
            "플레이어가 상호작용 거리 안에 들어왔을 때 보여줄 안내 UI입니다. " +
            "키보드 키캡 모양의 E 안내 Prefab/자식 오브젝트를 연결하세요.")]
        [SerializeField]
        private GameObject interactionGuide;

        [Header("Interaction")]
        [Tooltip(
            "Exit Portal 상호작용에 사용할 키입니다. " +
            "현재 프로젝트의 미니 스테이지 포탈과 동일하게 기본값은 E입니다.")]
        [SerializeField]
        private KeyCode interactionKey = KeyCode.E;

        [Tooltip(
            "체크되어 있을 때만 플레이어 접근 및 E 입력을 받습니다.")]
        [SerializeField]
        private bool interactionEnabled = true;

        [Header("Debug")]
        [Tooltip(
            "플레이어 진입/이탈 및 E 입력 로그를 Console에 출력합니다.")]
        [SerializeField]
        private bool debugLog = true;

        private readonly HashSet<int>
            playerColliderIds =
                new HashSet<int>();

        /// <summary>
        /// 다음 단계의 Level 1 복귀 처리에서 구독할 수 있는 이벤트입니다.
        /// 현재 단계에서는 이벤트 발생과 로그 확인까지만 사용합니다.
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
            if (!interactionEnabled ||
                !IsPlayerInside)
            {
                return;
            }

            if (!Input.GetKeyDown(interactionKey))
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

            ExitRequested?.Invoke(this);
        }

        /// <summary>
        /// 다음 단계에서 씬 전환 중 중복 입력을 막을 때 사용할 수 있습니다.
        /// </summary>
        public void SetInteractionEnabled(
            bool value)
        {
            interactionEnabled = value;

            SetGuideVisible(
                interactionEnabled &&
                IsPlayerInside);

            if (debugLog)
            {
                Debug.Log(
                    $"[ExitPortalInteractable] " +
                    $"상호작용 가능 상태 변경: {interactionEnabled}",
                    this);
            }
        }

        private void OnTriggerEnter2D(
            Collider2D other)
        {
            if (!interactionEnabled ||
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
                    "E키로 탈출 상호작용 가능.",
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
                interactionEnabled);
        }

        /// <summary>
        /// Exit Portal 루트 또는 자식에 Trigger Collider2D가 있는지
        /// 런타임 시작 시 한 번 검증합니다.
        /// </summary>
        private void ValidateTriggerSetup()
        {
            Collider2D[] colliders =
                GetComponentsInChildren<Collider2D>(true);

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
