using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Vampire
{
    public sealed class DemoCompletionDialogue : MonoBehaviour
    {
        public const string CompletionMessage = "데모버전은 스테이지 1까지만 공개됩니다!";
        public bool Confirmed { get; private set; }
        public Button ConfirmButton;
        public TMP_Text Message;
        public void Confirm()
        {
            if (Confirmed) return;
            Confirmed = true;
            ConfirmButton.interactable = false;
        }
    }
}
