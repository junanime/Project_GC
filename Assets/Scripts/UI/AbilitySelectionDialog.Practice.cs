using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public partial class AbilitySelectionDialog
    {
        GStarControlPractice practiceOwner;
        Image practiceShade;
        TextMeshProUGUI practicePrompt;
        InteractionKeycapGraphic rerollKey;

        internal void OpenPractice(GStarControlPractice owner)
        {
            practiceOwner = owner;
            Open(false);
        }

        void ShowPracticeSpotlight(bool choose)
        {
            if (practiceShade == null)
            {
                practiceShade = AugmentPanelTheme.Fill("연습 주목 배경",panelContent,new Color(0,0,0,.52f));
                // Cover the full modal even on ultrawide screens.
                AugmentPanelTheme.Box(practiceShade.rectTransform,Vector2.zero,new Vector2(12000,12000));
                practicePrompt = AugmentPanelTheme.Label("증강 연습 대사",panelContent,TrainingUITheme.Font,"",25);
                practicePrompt.fontStyle = FontStyles.Bold;
                practicePrompt.color = new Color(1f,.97f,.81f);
                practicePrompt.raycastTarget = false;
            }
            practiceShade.gameObject.SetActive(true);
            practiceShade.raycastTarget = !choose;
            practiceShade.transform.SetAsLastSibling();
            skipButton.gameObject.SetActive(false);
            rerollButton.gameObject.SetActive(!choose);
            if (choose) abilityCardsParent.SetAsLastSibling();
            else rerollButton.transform.SetAsLastSibling();
            if (rerollKey != null) rerollKey.ShowKey("R",39,!choose);
            for (int i = 0; i < abilityCards.Count; i++)
            {
                var key = abilityCards[i].transform.Find("Choice keycap")?.GetComponent<InteractionKeycapGraphic>();
                if (key != null) key.ShowKey((i+1).ToString(),54,choose);
            }
            practicePrompt.gameObject.SetActive(true);
            practicePrompt.transform.SetAsLastSibling();
            practicePrompt.text = choose ? "이번 연습은 지존 확정!   마음에 드는 카드를 골라 보세요." :
                "조금 아쉬운데요~   새로고침해 볼까요?";
            AugmentPanelTheme.Box(practicePrompt.rectTransform,
                new Vector2(choose ? 0 : -241,-291),new Vector2(choose ? 980 : 470,62));
        }

        void HidePracticeSpotlight()
        {
            if (practiceShade != null) practiceShade.gameObject.SetActive(false);
            if (practicePrompt != null) practicePrompt.gameObject.SetActive(false);
            if (skipButton != null) skipButton.gameObject.SetActive(true);
            if (rerollKey != null) rerollKey.ShowKey("R",39);
            if (abilityCards == null) return;
            for (int i = 0; i < abilityCards.Count; i++)
            {
                var key = abilityCards[i].transform.Find("Choice keycap")?.GetComponent<InteractionKeycapGraphic>();
                if (key != null) key.ShowKey((i+1).ToString(),54);
            }
        }
    }
}
