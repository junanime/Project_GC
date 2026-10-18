using System;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        RelicBlueprint[] supplyChoices;
        Func<RelicBlueprint,bool> supplyAccept;
        Action supplyCancel;
        int supplySelection;
        float supplyPreviousTime;
        public void ShowSupplyRelics(RelicBlueprint[] choices,Func<RelicBlueprint,bool> accept,Action cancel)
        {
            supplyChoices=choices;supplyAccept=accept;supplyCancel=cancel;supplySelection=0;
            supplyPreviousTime=Time.timeScale;Time.timeScale=0;Show("supplyRelic");
        }
        void CloseSupplyRelics(bool accepted)
        {
            var cancel=supplyCancel;supplyCancel=null;supplyAccept=null;
            Show("hud");Time.timeScale=supplyPreviousTime;
            if(!accepted)cancel?.Invoke();
        }
        void OctoberSupplyRelics()
        {
            // Reuse the merchant's dimmer, banner, card frames, typography, buttons and tooltip contract.
            MerchantBackdrop("보급 유물 선택");
            Label(content,"이번 탐험에 추가할 유물을 하나 선택하세요",.15f,.77f,.85f,.82f,21).color=Color.white;
            for(int i=0;i<(supplyChoices?.Length??0);i++)
            {
                int index=i;var relic=supplyChoices[i];float y=.57f-i*.17f;
                var b=ActionButton(content,"",.13f,y,.46f,y+.15f,()=>{supplySelection=index;Render();},false,true,index==supplySelection);
                b.name="Supply relic choice "+i;SlotArt(b);
                var visual=b.transform.Find("Visual");ImageAt(visual,relic.icon,.03f,.1f,.27f,.9f);
                Label(visual,relic.relicName,.29f,.15f,.95f,.85f,22);
            }
            Panel(content,.49f,.22f,.875f,.73f);
            var chosen=supplyChoices!=null&&supplyChoices.Length>0?supplyChoices[supplySelection]:null;
            ImageAt(content,chosen?.icon,.60f,.51f,.765f,.70f);
            Label(content,chosen!=null?chosen.relicName:"모든 유물을 보유하고 있습니다",.51f,.445f,.855f,.51f,25);
            var detail=Label(content,chosen?.Description??"",.525f,.245f,.84f,.435f,22);detail.name="Supply relic description";detail.alignment=TMPro.TextAlignmentOptions.TopLeft;
            ActionButton(content,"유물 획득",.57f,.10f,.84f,.19f,()=>{
                if(chosen!=null&&supplyAccept!=null&&supplyAccept(chosen))CloseSupplyRelics(true);
            },true,chosen!=null);
            ActionButton(content,"나중에 받기",.15f,.10f,.42f,.19f,()=>CloseSupplyRelics(false));
        }
    }
}
