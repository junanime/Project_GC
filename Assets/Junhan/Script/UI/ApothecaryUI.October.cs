using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        void OctoberMain()
        {
            previewCharacter=null;
            foreach(var c in Config.characters)
            {
                string role=OctoberArt.CharacterKey(c);float x,y,r,t;
                switch(role)
                {
                    case "Hyuki":x=.11f;y=.12f;r=.255f;t=.425f;break;
                    case "Shini":x=.475f;y=.45f;r=.615f;t=.74f;break;
                    case "Ari":x=.655f;y=.10f;r=.86f;t=.42f;break;
                    default:x=.36f;y=.075f;r=.53f;t=.39f;break;
                }
                var art=ImageAt(content,OctoberArt.Character(c,0),x,y,r,t);art.name=role+" title actor";
                art.gameObject.AddComponent<OctoberTitleMotion>().role=role;
            }
            var germ=ImageAt(content,OctoberArt.Get("OctoberUI/MapSymbols","Germ"),.818f,.085f,.908f,.252f);
            germ.gameObject.AddComponent<OctoberTitleMotion>().role="Germ";
            string[] labels={"게임 시작","업적","설정","종료"};string[] pages={"prepare","unlock","settings","exit"};
            for(int i=0;i<4;i++){int index=i;ActionButton(content,labels[i],.768f,.77f-i*.104f,.968f,.872f-i*.104f,()=>Show(pages[index]),i==0);}
        }
        void OctoberFrame(string title)
        {
            var dim=ImageAt(content,null,0,0,1,1,false);dim.color=new Color(.13f,.025f,.08f,.30f);
            Panel(content,.10f,.11f,.905f,.865f);
            ImageAt(content,OctoberArt.Get(Page=="result"&&!passed?"OctoberUI/FailureBanner":"OctoberUI/Banner"),.30f,.79f,.70f,1);
            var heading=Label(content,title,.34f,.86f,.66f,.967f,42);heading.fontStyle=TMPro.FontStyles.Bold;heading.color=new Color(1,.94f,.68f);heading.outlineColor=OctoberArt.Ink;heading.outlineWidth=.16f;
        }
        void OctoberPrepare()
        {
            character=Config.characters[characterIndex];previewCharacter=null;
            var preview=Rect("Character skill preview",content,.15f,.555f,.423f,.824f);
            var effect=ImageAt(preview,null,0,0,1,1);
            portrait=ImageAt(preview,CharacterSprite(character),.05f,-.07f,.95f,1.08f);
            var motion=preview.gameObject.AddComponent<PreparationSkillPreview>();motion.character=character;motion.body=portrait;motion.effect=effect;
            Label(content,character.name,.19f,.505f,.365f,.551f,27);
            Panel(content,.128f,.333f,.437f,.505f);
            IdentityHeader(content,"기본 능력치",.14f,.457f,.286f,.498f,18);
            Label(content,$"체력 {character.hp:0}   방어 {character.armor}\n이동 {character.movespeed:0.##}   행운 {character.luck:0.##}",.14f,.357f,.288f,.448f,17);
            ProfileSkills(character,.298f,.347f,.43f,.491f);
            var relic=Config.relics.FirstOrDefault(r=>RelicSaveData.IsEquipped(r.relicId));
            var needle=StartingNeedleSelection.Selected(Config,character);
            SmallLoadoutSlot(.128f,"무기",needle!=null?needle.Image:Config.basicNeedle,needle!=null?Ver4AugmentCatalog.ParentNames[(int)needle.Type]:"기본 침",()=>SwitchTab(1));
            SmallLoadoutSlot(.207f,"유물",relic!=null?relic.icon:null,relic!=null?relic.relicName:"빈 슬롯",()=>SwitchTab(2));
            for(int i=0;i<2;i++)
            {
                var item=LobbyLoadoutData.SelectedCarryItems.ElementAtOrDefault(i);
                SmallLoadoutSlot(.286f+i*.079f,"아이템 "+(i+1),item!=null?item.itemIcon:null,item!=null?item.itemName:"빈 슬롯",()=>SwitchTab(3));
            }
            string[] tabs={"캐릭터","무기","유물","아이템"};
            for(int i=0;i<4;i++){int k=i;ActionButton(content,tabs[i],.472f+i*.104f,.769f,.57f+i*.104f,.819f,()=>SwitchTab(k),false,true,Tab==i);}
            OctoberSelection();
            Label(content,message,.472f,.124f,.878f,.16f,14);
            ActionButton(content,"‹  뒤로",.10f,.018f,.28f,.095f,()=>Show("main"));
            ActionButton(content,Owned(character)?"출전하기":"캐릭터 해금 필요",.667f,.018f,.905f,.095f,StartRun,true,Owned(character)&&!starting);
        }
        void IdentityHeader(Transform parent,string title,float x,float y,float right,float top,float size)
        {
            var pill=Rect("Category "+title,parent,x,y,right,top).gameObject.AddComponent<IdentityShape>();
            pill.color=PreparationIdentity.ColorFor(character);pill.radius=30;pill.raycastTarget=false;
            var text=Label(pill.transform,title,0,0,1,1,size);text.fontStyle=TMPro.FontStyles.Bold;
            text.color=OctoberArt.CharacterKey(character)=="Ari"?Ink:Color.white;
        }
        void SmallLoadoutSlot(float x,string heading,Sprite icon,string caption,Action action)
        {
            var b=ActionButton(content,"",x,.145f,x+.073f,.322f,action);SlotArt(b);b.name="Loadout "+heading;
            var v=b.transform.Find("Visual");
            IdentityHeader(v,heading,0,.78f,1,1,14);
            var frame=Rect("Icon frame",v,.09f,.21f,.91f,.76f).gameObject.AddComponent<IdentityShape>();
            frame.color=Color.Lerp(PreparationIdentity.ColorFor(character),Color.white,.50f);frame.frame=true;frame.radius=8;frame.raycastTarget=false;frame.insetColor=new Color(1,.94f,.84f);
            ImageAt(frame.transform,icon,.1f,.1f,.9f,.9f);
            if(icon==null)Label(frame.transform,"+",.1f,.1f,.9f,.9f,28).color=new Color(.72f,.51f,.46f);
            var label=Label(v,caption,.04f,.015f,.96f,.205f,12);label.fontStyle=TMPro.FontStyles.Bold;
        }
        void OctoberSelection()
        {
            var items=Config.items.Where(i=>i!=null&&i.canBuyInLobby).ToArray();
            var weapons=(Config.weapons??Array.Empty<SyringeSpecialAugmentAbility>()).OrderBy(w=>StartingNeedleSelection.Owned(w)?0:1).ThenBy(w=>(int)w.Type).ToArray();
            int count=Tab==0?9:Tab==1?weapons.Length+1:Tab==2?Config.relics.Length:items.Length;
            selection=Mathf.Clamp(selection,0,Mathf.Max(0,count-1));
            int pages=Mathf.Max(1,Mathf.CeilToInt(count/6f));pageIndex=Mathf.Clamp(pageIndex,0,pages-1);
            Func<int,bool> owned=i=>Tab==0?i<Config.characters.Length&&Owned(Config.characters[i]):Tab==1?i==0||StartingNeedleSelection.Owned(weapons[i-1]):Tab==2?RelicSaveData.IsUnlocked(Config.relics[i].relicId):true;
            Func<int,Sprite> icon=i=>Tab==0?i<Config.characters.Length?CharacterProfile(Config.characters[i]):OctoberArt.Character(Config.characters[i%Config.characters.Length],0):Tab==1?i==0?Config.basicNeedle:weapons[i-1].Image:Tab==2?Config.relics[i].icon:items[i].itemIcon;
            Func<int,string> name=i=>Tab==0?i<Config.characters.Length?Config.characters[i].name:"새로운 동료":Tab==1?i==0?"기본 침":Ver4AugmentCatalog.ParentNames[(int)weapons[i-1].Type]:Tab==2?Config.relics[i].relicName:items[i].itemName;
            for(int j=0;j<6;j++)
            {
                int index=pageIndex*6+j;if(index>=count)break;
                float x=.478f+(j%3)*.135f,y=.752f-(j/3+1)*.174f;
                var b=ActionButton(content,"",x,y,x+.126f,y+.162f,()=>{selection=index;if(Tab==0&&index<Config.characters.Length&&Owned(Config.characters[index])){characterIndex=index;character=Config.characters[index];}Render();},false,true,selection==index);SlotArt(b);b.name="Choice "+Tab+" "+index;
                var v=b.transform.Find("Visual");var a=ImageAt(v,icon(index),.16f,.21f,.84f,.94f);
                bool unlocked=owned(index);if(!unlocked){a.color=new Color(0,0,0,.65f);DrawLock(v,.38f,.35f,.62f,.65f);}
                Label(v,unlocked?name(index):"잠김",.03f,.01f,.97f,.22f,15);
                if(selection==index){var edge=ImageAt(v,null,.04f,.0f,.96f,.025f,false);edge.color=new Color(.12f,.85f,.56f);}
                if(Tab==1 && index>0 && unlocked)
                {
                    var weapon=weapons[index-1];bool enabled=StartingNeedleSelection.Enabled(weapon);
                    if(!enabled)a.color=new Color(.45f,.45f,.45f,.6f);
                    var check=ActionButton(b.transform,"",.025f,.70f,.27f,.96f,()=>{
                        if(!StartingNeedleSelection.SetEnabled(weapon,!StartingNeedleSelection.Enabled(weapon),Config))
                            message="증강 후보는 최소 1종을 활성화해야 합니다.";
                        Render();
                    },enabled);
                    check.name="Weapon pool toggle "+weapon.Type;
                    if(enabled)
                    {
                        // Geometry avoids a missing checkmark glyph in the Korean display font.
                        var shortStroke=ImageAt(check.transform,null,.21f,.34f,.48f,.47f,false);
                        shortStroke.rectTransform.localRotation=Quaternion.Euler(0,0,-42);
                        var longStroke=ImageAt(check.transform,null,.37f,.43f,.81f,.56f,false);
                        longStroke.rectTransform.localRotation=Quaternion.Euler(0,0,42);
                    }
                }
            }
            if(pages>1)
            {
                ActionButton(content,"‹",.48f,.36f,.525f,.398f,()=>{pageIndex--;Render();},false,pageIndex>0);
                Label(content,$"{pageIndex+1} / {pages}",.545f,.36f,.806f,.398f,14);
                ActionButton(content,"›",.828f,.36f,.874f,.398f,()=>{pageIndex++;Render();},false,pageIndex<pages-1);
            }
            string detail="",action="선택",title=count>0?name(selection):"",need="";Action accept=null;bool can=true;
            if(count==0){Label(content,"준비 중",.5f,.35f,.85f,.6f,24);return;}
            bool has=owned(selection);
            if(Tab==0)
            {
                if(selection>=Config.characters.Length){detail="새로운 동료가 준비 중입니다.";can=false;action="준비 중";}
                else
                {
                    var c=Config.characters[selection];detail=c.description;
                    if(c.skills!=null)detail+=$"\n패시브 · {c.skills.passiveName}\n액티브 · {c.skills.activeName}";
                    need=$"{c.cost} 실버로 해금";
                    action=has?(character==c?"선택됨":"캐릭터 선택"):$"해금 · {c.cost}";can=has||SilverWallet.CanSpend(c.cost);
                    accept=()=>{if(!Owned(c)&&SilverWallet.TrySpend(c.cost))LobbyUnlockSave.Unlock("Character",c.name);if(Owned(c)){characterIndex=Array.IndexOf(Config.characters,c);character=c;}Render();};
                }
            }
            else if(Tab==1)
            {
                var w=selection==0?null:weapons[selection-1];detail=w!=null?w.Description:"기본 침으로 시작합니다. 전투에서 원하는 침을 획득하세요.";
                int cost=StartingNeedleSelection.Price(w);need=$"{cost} 실버로 해금";action=has?"무기 장착":$"해금 · {cost}";can=has||SilverWallet.CanSpend(cost);
                if(has && w!=null && !StartingNeedleSelection.Enabled(w)){action="후보 비활성화";can=false;detail+="\n체크를 켜면 장착·전투 중 증강 후보에 포함됩니다.";}
                accept=()=>{if(!has&&SilverWallet.TrySpend(cost))LobbyUnlockSave.Unlock("Needle",w.Type.ToString());if(w==null||StartingNeedleSelection.Owned(w))StartingNeedleSelection.Set(w,character);Render();};
            }
            else if(Tab==2)
            {
                var r=Config.relics[selection];detail=RelicDescription(r);need=$"{r.price} 실버로 해금";action=has?"유물 장착":$"해금 · {r.price}";can=has||SilverWallet.CanSpend(r.price);
                accept=()=>{if(!has&&SilverWallet.TrySpend(r.price))RelicSaveData.Unlock(r.relicId);if(RelicSaveData.IsUnlocked(r.relicId))RelicSaveData.Equip(r.relicId);Render();};
            }
            else
            {
                var i=items[selection];detail=i.description;bool equipped=LobbyLoadoutData.IsEquipped(i);
                action=equipped?"장착 해제":$"구매 · {i.silverCost}";
                can=equipped||(LobbyLoadoutData.SelectedCarryItems.Count<2&&SilverWallet.CanSpend(i.silverCost));
                accept=()=>{if(LobbyLoadoutData.IsEquipped(i)){LobbyLoadoutData.Unequip(i);SilverWallet.Add(i.silverCost);}else LobbyLoadoutData.TryBuyAndEquip(i,i.silverCost,out message);Render();};
            }
            Panel(content,.474f,.165f,.878f,.352f);
            var titleLabel=Label(content,title,.486f,.305f,.706f,.347f,19);titleLabel.alignment=TMPro.TextAlignmentOptions.MidlineLeft;titleLabel.fontStyle=TMPro.FontStyles.Bold;
            var explanation=Label(content,detail,.486f,.179f,.708f,.304f,15);explanation.name="Selection description";
            explanation.alignment=TMPro.TextAlignmentOptions.TopLeft;explanation.enableWordWrapping=true;
            // Keep the main-menu button's 3.49:1 footprint at the smaller size.
            var choose=ActionButton(content,action,.718f,.248f,.865f,.323f,()=>accept?.Invoke(),true,can);
            choose.name="Selection action";
            Label(content,!has?need:Tab==0?"오른쪽 프로필을 눌러 변경":Tab==1?"체크한 침만 전투 중 등장":Tab==3?"아이템은 최대 2개":"선택한 유물로 출전",.718f,.178f,.865f,.239f,13);

        }
        void OctoberDetails(string title,string description)
        {
            var p=Rect("Selection details",content,.25f,.25f,.75f,.7f);Panel(p,0,0,1,1);
            Label(p,title,.08f,.75f,.92f,.94f,27);Label(p,description,.08f,.25f,.92f,.75f,22);
            ActionButton(p,"닫기",.35f,.055f,.65f,.23f,()=>Destroy(p.gameObject));
        }
        void DrawLock(Transform parent,float x,float y,float r,float t)
        {
            var h=Rect("Lock",parent,x,y,r,t);var outside=ImageAt(h,null,.22f,.4f,.78f,1,false);outside.color=OctoberArt.Ink;
            var hole=ImageAt(h,null,.34f,.48f,.66f,.85f,false);hole.color=new Color(1,.97f,.88f);
            var body=ImageAt(h,null,.05f,0,.95f,.62f,false);body.color=OctoberArt.Ink;
            var center=ImageAt(h,null,.13f,.08f,.87f,.54f,false);center.color=new Color(1,.97f,.88f);
            ImageAt(h,null,.45f,.19f,.55f,.42f,false).color=OctoberArt.Ink;
        }
        MerchantItemBlueprint[] OctoberOwnedItems()
        {
            var runtime=level!=null&&level.PlayerCharacter!=null?level.PlayerCharacter.GetComponent<OctoberItemRuntime>():null;
            return runtime!=null?Config.items.Where(i=>i!=null&&runtime.Has(i.octoberId)).ToArray():runItems.Where(i=>i!=null).ToArray();
        }
        void OctoberResult()
        {
            previewCharacter=null;
            portrait=ImageAt(content,OctoberArt.Character(character,passed?2:1),.19f,.43f,.45f,.80f);
            float time=level!=null?level.CurrentLevelTime:0;
            string[] titles={"플레이 시간","처치한 적","획득한 코인","도달 레벨"};
            string[] values={$"{(int)time/3600:00}:{(int)time/60%60:00}:{(int)time%60:00}",$"{(stats!=null?stats.MonstersKilled:0):N0} 마리",$"{(stats!=null?stats.CoinsGained:0):N0} 개",$"Lv. {(level!=null&&level.PlayerCharacter!=null?level.PlayerCharacter.CurrentLevel:1)}"};
            for(int i=0;i<4;i++){float y=.70f-i*.073f;Label(content,titles[i],.50f,y,.68f,y+.063f,24);Label(content,values[i],.70f,y,.82f,y+.063f,26);}
            ImageAt(content,null,.18f,.35f,.82f,.414f,false).color=new Color(.91f,.35f,.46f);
            Label(content,"이번 탐험의 기록",.18f,.35f,.82f,.414f,23).color=Color.white;
            var ownedItems=OctoberOwnedItems();
            var relic=Config.relics.FirstOrDefault(r=>RelicSaveData.IsEquipped(r.relicId));
            var entries=AugmentHistoryManager.Instance!=null?AugmentHistoryManager.Instance.Entries:null;
            string[] summary={"획득 실버\n"+(SilverRunRewarder.Instance!=null?SilverRunRewarder.Instance.RunEarnedSilver:0).ToString("N0"),"수집 아이템\n"+ownedItems.Length+"종","장착 유물\n"+(relic!=null?relic.relicName:"없음"),"획득 증강\n"+(entries!=null?entries.Count:0)+"종"};
            Sprite[] icons={Config.items.FirstOrDefault(i=>i.octoberId==39)?.itemIcon,ownedItems.FirstOrDefault()?.itemIcon,relic!=null?relic.icon:null,entries!=null&&entries.Count>0?entries[0].icon:Config.basicNeedle};
            for(int i=0;i<4;i++)
            {
                float x=.19f+i*.16f;Panel(content,x,.19f,x+.14f,.343f);
                ImageAt(content,icons[i],x+.045f,.265f,x+.095f,.335f);
                Label(content,summary[i],x+.005f,.194f,x+.135f,.267f,16);
            }
            ActionButton(content,"메인으로",.13f,.052f,.355f,.149f,()=>ReturnToLobby(false));
            ActionButton(content,"다시하기",.387f,.052f,.613f,.149f,()=>{Time.timeScale=1;CrossSceneData.CharacterBlueprint=character;CrossSceneData.ClearStartingLobbyItems();SceneManager.LoadScene(1);});
            ActionButton(content,"준비화면",.645f,.052f,.87f,.149f,()=>ReturnToLobby(true),true);
        }
    }
}
