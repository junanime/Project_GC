using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        void SelectCharacterBookmark()
        {
            if(level==null || confirmingDisplay || pendingSessionAction!=null || pendingKeyAction.HasValue)return;
            if(Page=="settings" && settingsOriginal!=null)GamePreferences.Apply(settingsOriginal,false);
            Show("run"); // Transfer the existing pause; never acquire or release it twice.
        }

        void CloseUnifiedBook()
        {
            if(Page=="settings")CancelSettings();else CloseRunBook();
        }

        void UnifiedBookFrame(bool settings)
        {
            ImageAt(content,RunBookArt.Backplate,0,0,1,1,false).name="Exploration record backplate";
            if(settings)
            {
                // Cover only the printed inner compartments, preserving the original frame/banner.
                var paper=RecordShape("Settings parchment",content,.035f,.135f,.930f,.813f,new Color(1,.94f,.81f),false,16);
                paper.glossy=false;
            }
            string heading=settings?"탐험 설정":"탐험 기록";
            foreach(var offset in new[]{new Vector2(-.002f,0),new Vector2(.002f,0),new Vector2(0,-.003f),new Vector2(0,.003f)})
                RecordText("Record title outline",heading,.403f+offset.x,.012f+offset.y,.196f,.090f,48,TextAlignmentOptions.Center).fontStyle=FontStyles.Bold;
            var title=RecordText("Record title",heading,.403f,.012f,.196f,.090f,48,TextAlignmentOptions.Center);
            title.fontStyle=FontStyles.Bold;title.color=new Color(1,.93f,.69f);
            var close=RecordButton("Record close","×",.944f,.092f,.039f,.065f,CloseUnifiedBook,false,true,true);
            close.GetComponent<ApothecaryButtonFeedback>().label.fontSizeMax=36;
            if(level!=null)
            {
                BookBookmark("Character bookmark","캐릭터정보",.052f,.148f,!settings,SelectCharacterBookmark);
                BookBookmark("Settings bookmark","설정",.216f,.104f,settings,()=>OpenSettings());
            }
        }

        void BookBookmark(string name,string title,float x,float width,bool selected,Action callback)
        {
            // Selected ribbons grow upward and outward inside non-overlapping fixed slots.
            float w=selected?width:width*.90f,h=selected?.103f:.084f;
            var b=ActionButton(content,title,x,.882f,x+w,.882f+h,callback,false,true,selected);b.name=name;
            var f=b.GetComponent<ApothecaryButtonFeedback>();f.useThemeStates=false;f.body.enabled=false;
            f.label.color=selected?new Color(1,.96f,.79f):RecordInk;f.label.fontSizeMin=15;f.label.fontSizeMax=selected?25:22;
            f.label.rectTransform.anchorMin=new Vector2(.04f,.20f);f.label.rectTransform.anchorMax=new Vector2(.96f,.94f);
            f.label.outlineColor=RecordInk;f.label.outlineWidth=selected?.16f:0;
            var shape=Rect("Bookmark silhouette",f.visual,0,0,1,1).gameObject.AddComponent<BookNavigationGraphic>();
            shape.color=selected?new Color(.97f,.31f,.34f):new Color(1,.90f,.71f);shape.raycastTarget=false;shape.transform.SetAsFirstSibling();
            // Keep the existing animated gold edge on the rectangular upper body of the ribbon.
            var orbit=(RectTransform)f.sparkle.transform;orbit.anchorMin=new Vector2(.02f,.24f);orbit.anchorMax=new Vector2(.98f,.98f);
            b.interactable=!confirmingDisplay&&pendingSessionAction==null;
        }

        Button SettingsButton(Transform parent,string text,float x,float y,float r,float t,Action action,bool primary=false,bool enabled=true,bool chosen=false,bool clickSound=true)
        {
            var b=ActionButton(parent,text,x,y,r,t,action,primary,enabled,chosen,clickSound);
            var f=b.GetComponent<ApothecaryButtonFeedback>();f.useThemeStates=false;f.body.enabled=false;
            var shape=RecordShape("Settings button frame",f.visual,0,0,1,1,RecordGold,true,10);
            shape.insetColor=primary||chosen?new Color(1,.45f,.44f):new Color(1,.94f,.81f);shape.transform.SetAsFirstSibling();
            f.label.color=primary||chosen?Color.white:RecordInk;f.label.fontSizeMax=21;f.label.fontSizeMin=14;
            return b;
        }
    }
}
