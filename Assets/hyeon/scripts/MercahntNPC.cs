using UnityEngine;

namespace Vampire
{
    [RequireComponent(typeof(Collider2D))]
    public class MerchantNPC : MonoBehaviour
    {
        private Character playerCharacter;
        private bool isShopOpen = false;

        void Start()
        {
            // 맵에 존재하는 플레이어 캐릭터를 자동으로 찾아서 기억합니다.
            playerCharacter = FindObjectOfType<Character>();

            // ZPositioner (앞뒤 가림 처리) 세팅
            if (playerCharacter != null)
            {
                ZPositioner zPositioner = gameObject.AddComponent<ZPositioner>();
                zPositioner.Init(playerCharacter.transform);
            }

            // 상인을 통과할 수 없는 벽으로 설정
            GetComponent<Collider2D>().isTrigger = false;
        }

        void OnCollisionEnter2D(Collision2D col)
        {
            // 부딪힌 대상이 플레이어 캐릭터인지 확인
            if (!isShopOpen && playerCharacter != null && col.collider.gameObject == playerCharacter.gameObject)
            {
                OpenShopUI();
            }
        }

        private void OpenShopUI()
        {
            isShopOpen = true;
            Debug.Log("수상한 상인과 부딪혔습니다! 상점 UI를 엽니다.");

            Time.timeScale = 0; // 게임 일시정지
            MerchantUIManager.Instance.OpenShop(this);
        }

        public void CloseShopUI()
        {
            isShopOpen = false;
            Time.timeScale = 1; // 게임 재개
        }
    }
}