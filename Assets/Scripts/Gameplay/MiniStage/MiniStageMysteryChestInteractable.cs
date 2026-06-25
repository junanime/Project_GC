using TMPro;
using UnityEngine;

namespace Vampire
{
    public enum MiniStageMysteryChestOutcome
    {
        Fake,
        TrueReward,
        Monster
    }

    /// <summary>
    /// 개편 개꿀방의 선택용 가짜 상자.
    ///
    /// 실제 Chest가 아니라 E키 상호작용용 오브젝트입니다.
    /// 선택하는 순간 방 스크립트에 결과 처리를 요청합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class MiniStageMysteryChestInteractable : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("이 상자를 관리하는 MiniStageBonusChestRoom입니다. 비워두면 부모에서 자동으로 찾습니다.")]
        [SerializeField] private MiniStageBonusChestRoom ownerRoom;

        [Tooltip("상자 외형 SpriteRenderer입니다.")]
        [SerializeField] private SpriteRenderer chestRenderer;

        [Tooltip("상호작용 가능할 때 보여줄 안내 UI입니다. 예: 'E 확인'. 없어도 작동합니다.")]
        [SerializeField] private GameObject interactionGuide;

        [Tooltip("상자의 결과를 짧게 보여줄 텍스트입니다. 없어도 작동합니다.")]
        [SerializeField] private TMP_Text resultLabel;

        [Header("Sprites")]
        [Tooltip("선택 전 공통 상자 스프라이트입니다.")]
        [SerializeField] private Sprite closedChestSprite;

        [Tooltip("꽝 상자로 밝혀졌을 때 사용할 스프라이트입니다.")]
        [SerializeField] private Sprite fakeChestSprite;

        [Tooltip("진짜 보상 상자로 밝혀졌을 때 사용할 스프라이트입니다.")]
        [SerializeField] private Sprite trueRewardChestSprite;

        [Tooltip("몬스터 상자로 밝혀졌을 때 사용할 스프라이트입니다.")]
        [SerializeField] private Sprite monsterChestSprite;

        [Header("Colors")]
        [Tooltip("선택 전 상자 색상입니다.")]
        [SerializeField] private Color closedColor = Color.white;

        [Tooltip("꽝 상자로 밝혀졌을 때 색상입니다.")]
        [SerializeField] private Color fakeColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        [Tooltip("진짜 보상 상자로 밝혀졌을 때 색상입니다.")]
        [SerializeField] private Color trueRewardColor = new Color(1f, 0.9f, 0.25f, 1f);

        [Tooltip("몬스터 상자로 밝혀졌을 때 색상입니다.")]
        [SerializeField] private Color monsterColor = new Color(1f, 0.35f, 0.25f, 1f);

        [Header("Interaction")]
        [Tooltip("상자를 확인할 때 사용할 키입니다.")]
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        [Tooltip("상호작용 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private Collider2D triggerCollider;
        private bool playerInside;
        private bool selectable;
        private bool revealed;

        public MiniStageMysteryChestOutcome Outcome { get; private set; }

        private void Awake()
        {
            ResolveReferences();
            SetGuideVisible(false);
        }

        private void OnValidate()
        {
            ResolveReferences();

            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void Update()
        {
            if (!selectable || revealed)
            {
                return;
            }

            if (!playerInside)
            {
                return;
            }

            if (Input.GetKeyDown(interactionKey))
            {
                if (debugLog)
                {
                    Debug.Log($"[MiniStageMysteryChestInteractable] 상자 선택: {name}, outcome={Outcome}");
                }

                ownerRoom?.NotifyMysteryChestSelected(this);
            }
        }

        public void SetOwnerRoom(MiniStageBonusChestRoom room)
        {
            ownerRoom = room;
        }

        public void Setup(MiniStageMysteryChestOutcome outcome)
        {
            ResolveReferences();

            Outcome = outcome;
            selectable = true;
            revealed = false;
            playerInside = false;

            gameObject.SetActive(true);

            if (triggerCollider != null)
            {
                triggerCollider.enabled = true;
                triggerCollider.isTrigger = true;
            }

            if (chestRenderer != null)
            {
                chestRenderer.enabled = true;

                if (closedChestSprite != null)
                {
                    chestRenderer.sprite = closedChestSprite;
                }

                chestRenderer.color = closedColor;
            }

            if (resultLabel != null)
            {
                resultLabel.text = "?";
            }

            SetGuideVisible(false);
        }

        public void Reveal()
        {
            selectable = false;
            revealed = true;

            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }

            ApplyRevealedVisual();
            SetGuideVisible(false);
        }

        public void DespawnChest()
        {
            selectable = false;
            revealed = true;
            playerInside = false;

            SetGuideVisible(false);
            gameObject.SetActive(false);
        }

        private void ApplyRevealedVisual()
        {
            if (chestRenderer == null)
            {
                return;
            }

            switch (Outcome)
            {
                case MiniStageMysteryChestOutcome.Fake:
                    if (fakeChestSprite != null)
                    {
                        chestRenderer.sprite = fakeChestSprite;
                    }

                    chestRenderer.color = fakeColor;

                    if (resultLabel != null)
                    {
                        resultLabel.text = "꽝";
                    }
                    break;

                case MiniStageMysteryChestOutcome.TrueReward:
                    if (trueRewardChestSprite != null)
                    {
                        chestRenderer.sprite = trueRewardChestSprite;
                    }

                    chestRenderer.color = trueRewardColor;

                    if (resultLabel != null)
                    {
                        resultLabel.text = "보상";
                    }
                    break;

                case MiniStageMysteryChestOutcome.Monster:
                    if (monsterChestSprite != null)
                    {
                        chestRenderer.sprite = monsterChestSprite;
                    }

                    chestRenderer.color = monsterColor;

                    if (resultLabel != null)
                    {
                        resultLabel.text = "몬스터";
                    }
                    break;
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
            SetGuideVisible(selectable && !revealed);
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
            if (interactionGuide != null)
            {
                interactionGuide.SetActive(visible);
            }
        }

        private void ResolveReferences()
        {
            if (ownerRoom == null)
            {
                ownerRoom = GetComponentInParent<MiniStageBonusChestRoom>();
            }

            if (chestRenderer == null)
            {
                chestRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }

            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<Collider2D>();
            }

            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }
    }
}