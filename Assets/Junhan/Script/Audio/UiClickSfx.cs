using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    /// <summary>
    /// 일반 UI Button 클릭 시 공통 UI 클릭 효과음을 재생하는 재사용 컴포넌트입니다.
    ///
    /// 전용 효과음이 이미 있는 버튼에는 붙이지 마세요.
    /// 예: 증강 카드 선택(AugmentSelect) 등
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class UiClickSfx : MonoBehaviour
    {
        private Button targetButton;

        private void Awake()
        {
            targetButton = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (targetButton == null)
            {
                targetButton = GetComponent<Button>();
            }

            if (targetButton != null)
            {
                targetButton.onClick.AddListener(PlayUiClickSfx);
            }
        }

        private void OnDisable()
        {
            if (targetButton != null)
            {
                targetButton.onClick.RemoveListener(PlayUiClickSfx);
            }
        }

        /// <summary>
        /// Button.onClick이 실제 발생했을 때 공통 UI 클릭 효과음을 1회 재생합니다.
        /// </summary>
        private void PlayUiClickSfx()
        {
            GameAudioManager.PlaySfx(
                GameAudioManager.GameSfxId.UiClick
            );
        }
    }
}
