using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        bool ColonExitBlocksMenus
        {
            get
            {
                var exit = level != null ? level.GetComponent<ColonExitSequence>() : null;
                return exit != null && (exit.State == ColonExitSequence.ExitState.Unlocking || exit.State == ColonExitSequence.ExitState.Demo);
            }
        }
        void UpdateColonExitModal()
        {
            var dialogue = level != null ? level.GetComponent<ColonExitSequence>()?.Dialogue : null;
            if (dialogue == null) return;
            dialogue.transform.SetAsLastSibling();
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != dialogue.ConfirmButton.gameObject)
                EventSystem.current.SetSelectedGameObject(dialogue.ConfirmButton.gameObject);
        }
        internal DemoCompletionDialogue BuildColonDemoCompletionDialogue()
        {
            var holder = Rect("지스타 데모 · 탐험 완료", root, 0, 0, 1, 1); holder.SetAsLastSibling();
            var dim = holder.gameObject.AddComponent<Image>(); dim.color = new Color(.12f, .035f, .07f, .8f); dim.raycastTarget = true;
            var view = holder.gameObject.AddComponent<DemoCompletionDialogue>();
            var panel = Rect("Demo completion card", holder, .16f, .22f, .84f, .78f);
            var patch = StitchPatchTheme.Attach(panel, StitchPatchTheme.Coral, true); patch.radius = 25; patch.inset = 12;
            Label(panel, "변비왕의 봉인이 풀렸습니다!", .08f, .75f, .92f, .88f, 25).color = new Color(.55f, .27f, .23f);
            view.Message = Label(panel, "데모 버전은 여기까지입니다!", .06f, .44f, .94f, .71f, 40);
            view.Message.enableAutoSizing = true; view.Message.fontSizeMin = 24; view.Message.fontSizeMax = 40; view.Message.enableWordWrapping = true;
            Label(panel, "플레이해 주셔서 감사합니다.", .1f, .30f, .9f, .43f, 23);
            var button = ActionButton(panel, "출전준비로 돌아가기", .25f, .08f, .75f, .25f, view.Confirm);
            StitchPatchTheme.Button(button.GetComponent<ApothecaryButtonFeedback>(), false); view.ConfirmButton = button.GetComponent<Button>();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
            return view;
        }
    }
}
