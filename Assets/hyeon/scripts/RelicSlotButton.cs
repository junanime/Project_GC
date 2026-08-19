using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class RelicSlotButton : MonoBehaviour
    {
        // =========================================================
        // 유물 데이터
        // =========================================================
        [Header("Relic Data")]
        [SerializeField] private RelicBlueprint relic;


        // =========================================================
        // 아이템 UI
        // =========================================================
        [Header("Item UI")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;

        // 유물 효과 설명
        [SerializeField] private TextMeshProUGUI descriptionText;


        // =========================================================
        // 상태 UI
        // =========================================================
        [Header("Status UI")]
        [SerializeField] private Image statusImage;

        [SerializeField] private Sprite equippedStatusSprite; // 장착중
        [SerializeField] private Sprite ownedStatusSprite;    // 보유중
        [SerializeField] private Sprite notOwnedStatusSprite; // 미보유


        // =========================================================
        // 구매 버튼 UI
        // =========================================================
        [Header("Buy Button UI")]
        [SerializeField] private Button button;

        // 구매 버튼 안에 있는 TMP Text
        [SerializeField] private TextMeshProUGUI buyText;

        // 실버 코인이 들어있는 TMP Sprite Asset
        [SerializeField] private TMP_SpriteAsset silverCoinSpriteAsset;


        private RelicShopManager manager;


        public RelicBlueprint Relic => relic;

        public string RelicId =>
            relic != null ? relic.relicId : string.Empty;

        public int Price =>
            relic != null ? relic.price : 0;


        // =========================================================
        // 초기화
        // =========================================================
        public void Init(RelicShopManager owner)
        {
            manager = owner;


            // Inspector에서 버튼이 연결되지 않았다면
            // 자식 Button 자동 검색
            if (button == null)
            {
                button = GetComponentInChildren<Button>(true);
            }


            // 구매 버튼 Text 자동 검색
            if (buyText == null && button != null)
            {
                buyText = button.GetComponentInChildren<TextMeshProUGUI>(true);
            }


            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
                button.onClick.AddListener(OnClick);
            }
            else
            {
                Debug.LogWarning(
                    $"[RelicSlotButton] {gameObject.name}에 구매 버튼이 연결되지 않았습니다."
                );
            }


            Refresh();
        }


        // =========================================================
        // 구매 버튼 클릭
        // =========================================================
        private void OnClick()
        {
            if (manager != null)
            {
                manager.SelectRelic(this);
            }
        }


        // =========================================================
        // UI 갱신
        // =========================================================
        public void Refresh()
        {
            if (relic == null)
            {
                Debug.LogWarning(
                    $"[RelicSlotButton] {gameObject.name}에 RelicBlueprint가 연결되지 않았습니다."
                );

                return;
            }


            bool unlocked =
                RelicSaveData.IsUnlocked(relic.relicId);

            bool equipped =
                RelicSaveData.IsEquipped(relic.relicId);


            // =====================================================
            // 유물 이미지
            // =====================================================
            if (iconImage != null)
            {
                iconImage.sprite = relic.icon;
                iconImage.enabled = relic.icon != null;
                iconImage.preserveAspect = true;

                Color iconColor = iconImage.color;

                // 미보유면 살짝 흐리게
                iconColor.a = unlocked ? 1f : 0.65f;

                iconImage.color = iconColor;
            }


            // =====================================================
            // 유물 이름
            // =====================================================
            if (nameText != null)
            {
                nameText.text = relic.relicName;
            }


            // =====================================================
            // 유물 설명
            // =====================================================
            if (descriptionText != null)
            {
                descriptionText.gameObject.SetActive(true);
                descriptionText.text = relic.Description;
            }


            // =====================================================
            // 상태 이미지 변경
            // =====================================================
            if (statusImage != null)
            {
                // 미보유
                if (!unlocked)
                {
                    statusImage.sprite = notOwnedStatusSprite;
                }

                // 장착중
                else if (equipped)
                {
                    statusImage.sprite = equippedStatusSprite;
                }

                // 보유중
                else
                {
                    statusImage.sprite = ownedStatusSprite;
                }


                statusImage.preserveAspect = true;
                statusImage.color = Color.white;
            }


            // =====================================================
            // 구매 버튼
            // =====================================================
            if (button != null)
            {
                button.gameObject.SetActive(true);
                button.interactable = true;
            }


            // =====================================================
            // 실버 코인 + 가격
            //
            // 별도의 Image / PriceText 없이
            // 구매 버튼 안의 TMP Text 하나로 표시
            //
            // 결과:
            // [실버코인] 100
            // =====================================================
            if (buyText != null)
            {
                buyText.gameObject.SetActive(true);


                if (silverCoinSpriteAsset != null)
                {
                    buyText.spriteAsset = silverCoinSpriteAsset;

                    // 코인 위치 + 크기 조절
                    buyText.text =
                        $"<voffset=9px><size=85%><sprite=0></size></voffset> {relic.price}";
                }
                else
                {
                    // Sprite Asset이 없으면 가격만 표시
                    buyText.text = relic.price.ToString();

                    Debug.LogWarning(
                        $"[RelicSlotButton] {gameObject.name}에 Silver Coin Sprite Asset이 연결되지 않았습니다."
                    );
                }
            }
        }


        // =========================================================
        // 이벤트 제거
        // =========================================================
        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
            }
        }
    }
}