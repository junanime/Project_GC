using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Vampire
{
    public sealed class InventoryHudCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        InventorySlot slot;
        HudPaperGraphic face;
        TextMeshProUGUI count, caption;
        CharacterBlueprint character;
        LevelManager level;
        int index;
        TitleMenuSurface sparkle;
        bool hover, pressed;
        public void Configure(InventorySlot value,HudPaperGraphic paper,TextMeshProUGUI stock,TextMeshProUGUI text,int number)
        {slot=value;face=paper;count=stock;caption=text;index=number;character=null;
            sparkle=transform.Find("Card gold feedback")?.GetComponent<TitleMenuSurface>();
            if(sparkle==null)
            {
                sparkle=AugmentPanelTheme.Rect("Card gold feedback",transform).gameObject.AddComponent<TitleMenuSurface>();
                AugmentPanelTheme.Anchors(sparkle.rectTransform,Vector2.zero,Vector2.one);
                sparkle.button=GetComponent<UnityEngine.UI.Button>();sparkle.decorationOnly=true;sparkle.hideIcon=true;sparkle.raycastTarget=false;
            }
            Refresh();}
        void LateUpdate(){Refresh();}
        void Refresh()
        {
            if(slot==null||face==null)return;
            if(level==null)level=FindObjectOfType<LevelManager>();
            var current=level?.PlayerCharacter?.Blueprint??CrossSceneData.CharacterBlueprint;
            if(current!=character)
            {
                character=current;
                foreach(var ornament in transform.parent.GetComponentsInChildren<HudOrnament>(true))ornament.SetCharacter(character);
            }
            var edge=hover?new Color(1,.76f,.26f):CharacterHudTheme.Edge(character);
            if(face.edge!=edge){face.edge=edge;face.SetVerticesDirty();}
            face.color=pressed?new Color(1,.91f,.76f):CharacterHudTheme.Paper;
            bool highlighted=(hover||pressed)&&!GamePreferences.Current.reducedMotion;
            if(sparkle!=null&&sparkle.Selected!=highlighted)sparkle.SetActiveStyle(highlighted);
            count.text=slot.CountLabel;
            bool mobile=GamePlatform.UsesTouchControls||MobileGameplayInput.Active;
            caption.text=mobile?ConsumableHelp.ShortTitle(slot):ConsumableHelp.Keys(index);
        }
        public void OnPointerEnter(PointerEventData e){hover=true;}
        public void OnPointerExit(PointerEventData e){hover=pressed=false;}
        public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)pressed=true;}
        public void OnPointerUp(PointerEventData e){pressed=false;if(GamePlatform.UsesTouchControls||MobileGameplayInput.Active)hover=false;}
        void OnDisable(){hover=pressed=false;}
    }
}
