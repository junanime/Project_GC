using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        readonly int[] recordPages=new int[3];
        readonly string[] recordSelected=new string[3];
        static readonly Color RecordInk=new Color(.27f,.09f,.07f);
        static readonly Color RecordGold=new Color(.87f,.57f,.29f);
        static readonly string[] StatNames={"공격력","공격 속도","치명타 확률","치명타 피해","투사체 수","투사체 크기","투사체 속도","방어력","이동 속도","획득 범위","경험치 획득량","골드 획득량"};

        // The approved composition uses normalized coordinates measured from its top edge.
        RectTransform RecordRect(string name,Transform parent,float x,float y,float w,float h)=>Rect(name,parent,x,1-y-h,x+w,1-y);
        TextMeshProUGUI RecordText(string name,string value,float x,float y,float w,float h,float size,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
        {
            var label=Label(content,value,x,1-y-h,x+w,1-y,size);label.name=name;label.color=RecordInk;label.alignment=align;return label;
        }
        Image RecordImage(string name,Sprite sprite,float x,float y,float w,float h)
        {var image=ImageAt(content,sprite,x,1-y-h,x+w,1-y);image.name=name;return image;}
        IdentityShape RecordShape(string name,Transform parent,float x,float y,float w,float h,Color color,bool frame=false,float radius=9)
        {
            var shape=RecordRect(name,parent,x,y,w,h).gameObject.AddComponent<IdentityShape>();
            shape.color=color;shape.frame=frame;shape.radius=radius;shape.insetColor=new Color(1,.94f,.81f);shape.raycastTarget=false;return shape;
        }
        Button RecordButton(string name,string text,float x,float y,float w,float h,Action action,bool selected=false,bool enabled=true,bool coral=false)
        {
            var b=ActionButton(content,text,x,1-y-h,x+w,1-y,action,false,enabled,selected);b.name=name;
            var visual=b.transform.Find("Visual");var feedback=b.GetComponent<ApothecaryButtonFeedback>();feedback.useThemeStates=false;
            feedback.body.color=Color.clear;feedback.body.enabled=false;
            var shape=RecordShape("Record button frame",visual,0,0,1,1,selected?new Color(.70f,.35f,.86f):coral?new Color(1,.34f,.40f):RecordGold,true,h*720*(coral?.5f:.18f));
            shape.insetColor=coral?new Color(1,.43f,.46f):new Color(1,.94f,.81f);shape.transform.SetAsFirstSibling();
            feedback.label.color=coral?Color.white:RecordInk;feedback.label.fontSize=feedback.label.fontSizeMax=17;feedback.label.fontSizeMin=12;
            return b;
        }
        void ExplorationRecord()
        {
            Tab=0;previewCharacter=null;
            var player=level?.PlayerCharacter;
            ImageAt(content,RunBookArt.Backplate,0,0,1,1,false).name="Exploration record backplate";
            foreach(var offset in new[]{new Vector2(-.002f,0),new Vector2(.002f,0),new Vector2(0,-.003f),new Vector2(0,.003f)})
            {
                var stroke=RecordText("Record title outline","탐험 기록",.403f+offset.x,.012f+offset.y,.196f,.090f,48,TextAlignmentOptions.Center);stroke.fontStyle=FontStyles.Bold;
            }
            var title=RecordText("Record title","탐험 기록",.403f,.012f,.196f,.090f,48,TextAlignmentOptions.Center);
            title.fontStyle=FontStyles.Bold;title.color=new Color(1,.93f,.69f);title.outlineColor=RecordInk;title.outlineWidth=.2f;
            var close=RecordButton("Record close","×",.944f,.092f,.039f,.065f,CloseRunBook,false,true,true);
            var closeLabel=close.GetComponent<ApothecaryButtonFeedback>().label;closeLabel.fontSize=closeLabel.fontSizeMax=40;closeLabel.fontSizeMin=40;
            RecordButton("Record consumables","소모품",.048f,.030f,.08f,.046f,()=>SwitchTab(4));
            RecordButton("Record guide","안내",.135f,.030f,.067f,.046f,()=>SwitchTab(5));
            RecordButton("Record settings","설정",.211f,.030f,.067f,.046f,()=>Show("settings"));
            RecordText("Record key hint","TAB 닫기",.851f,.038f,.085f,.035f,13,TextAlignmentOptions.Right).color=new Color(1,.94f,.77f);
            if(player==null)return;
            character=player.Blueprint;
            RecordImage("Record character",OctoberArt.Character(character,0)??CharacterSprite(character),.083f,.155f,.161f,.219f);
            var name=RecordText("Record character name",$"{player.DisplayName} Lv.{player.CurrentLevel}",.052f,.388f,.201f,.048f,27,TextAlignmentOptions.Center);name.fontStyle=FontStyles.Bold;
            RecordShape("Record health track",content,.062f,.452f,.184f,.034f,new Color(.32f,.12f,.13f),false,13);
            RecordShape("Record health fill",content,.066f,.457f,.176f*Mathf.Clamp01(player.CurrentHealth/Mathf.Max(1,player.MaxHealth)),.024f,new Color(.12f,.76f,.30f),false,9).glossy=true;
            RecordText("Record health",$"HP {player.CurrentHealth:0} / {player.MaxHealth:0}",.073f,.452f,.164f,.032f,18,TextAlignmentOptions.Center).color=Color.white;
            var heart=level.PlayerInventory?.Slots.FirstOrDefault(slot=>slot.CollectableType!=null&&slot.CollectableType.name.ToLowerInvariant().Contains("health"));
            if(heart?.IconImage!=null)RecordImage("Record heart",heart.IconImage.sprite,.052f,.446f,.028f,.044f);
            for(int i=0;i<2;i++)
            {
                bool active=i==1;var data=character.skills;float x=.060f+i*.108f;
                var b=RecordButton(active?"Active skill slot":"Passive skill slot","",x,.505f,.081f,.102f,()=>ShowSkillDetails(character,active));
                var v=b.transform.Find("Visual");ImageAt(v,data!=null?(active?data.activeIcon:data.passiveIcon):null,.13f,.23f,.87f,.98f);
                var label=Label(v,data!=null?(active?data.activeName:data.passiveName):"준비 중",.04f,.01f,.96f,.24f,13);label.color=RecordInk;
                HudTooltip.Bind(b.gameObject,()=>SkillTitle(character,active),()=>SkillHelp(character,active));
            }
            RecordText("Stats heading","주요 스탯",.292f,.153f,.170f,.045f,25).fontStyle=FontStyles.Bold;
            var manager=FindObjectOfType<AbilityManager>();var needle=SyringeAbilityResolver.FindOwnedOrFirst(manager);var values=RunBookData.Stats(player,needle);
            for(int i=0;i<12;i++)
            {
                float y=.214f+i*.0317f;
                RecordImage("Stat icon "+i,RunBookArt.Icon(i),.282f,y,.022f,.028f);
                RecordText("Stat label "+i,StatNames[i],.311f,y,.106f,.029f,16);
                RecordText("Stat value "+i,values[i],.417f,y,.042f,.029f,16,TextAlignmentOptions.Right);
            }
            RecordText("Map heading","탐험 지도",.515f,.145f,.26f,.047f,25).fontStyle=FontStyles.Bold;
            RecordMap(player);
            var relic=player.Relics?.Equipped;
            var relics=new List<RunBookData.Entry>();
            if(relic!=null)relics.Add(new RunBookData.Entry{id=relic.relicId,title=relic.relicName,body=RelicDescription(relic),badge="장착 중",icon=relic.icon,color=new Color(.70f,.35f,.86f)});
            var items=OctoberOwnedItems().Select(item=>new RunBookData.Entry{id=item.octoberId.ToString(),title=item.itemName,body=item.description,badge="보유",icon=item.itemIcon}).ToList();
            RecordInventory(0,"유물",relics,.045f,.291f,6);
            RecordInventory(1,"아이템",items,.365f,.266f,10);
            RecordInventory(2,"무기",RunBookData.Weapons(manager,Config),.665f,.290f,10);
        }
        void RecordInventory(int section,string title,List<RunBookData.Entry> entries,float x,float width,int pageSize)
        {
            RecordText("Record section "+section,title,x+.027f,.638f,width-.20f,.041f,25).fontStyle=FontStyles.Bold;
            RecordText("Record count "+section,section==0?$"{entries.Count}/1":$"{entries.Count}종",x+width-.08f,.638f,.071f,.041f,17,TextAlignmentOptions.Right);
            int pages=Mathf.Max(1,Mathf.CeilToInt(entries.Count/(float)pageSize));recordPages[section]=Mathf.Clamp(recordPages[section],0,pages-1);
            if(!entries.Any(e=>e.id==recordSelected[section]))recordSelected[section]=entries.FirstOrDefault()?.id;
            int columns=section==0?6:5;float cellW=(width-.014f)/columns;
            for(int slot=0;slot<pageSize;slot++)
            {
                int index=recordPages[section]*pageSize+slot;var entry=index<entries.Count?entries[index]:null;
                float bx=x+.002f+(slot%columns)*cellW,by=section==0?.718f:.690f+(slot/columns)*.061f;
                var b=RecordButton($"Record {section} slot {slot}","",bx,by,cellW-.004f,.057f,()=>{recordSelected[section]=entry.id;Render();},entry!=null&&recordSelected[section]==entry.id,entry!=null);
                if(entry!=null)
                {
                    var v=b.transform.Find("Visual");ImageAt(v,entry.icon,.09f,.09f,.91f,.91f);
                    var shape=v.GetComponentInChildren<IdentityShape>();shape.color=recordSelected[section]==entry.id?entry.color:RecordGold;
                }
                else
                {
                    var shape=b.GetComponentInChildren<IdentityShape>();shape.color=new Color(.79f,.62f,.43f);shape.insetColor=new Color(.94f,.83f,.65f);
                }
            }
            if(pages>1)
            {
                RecordButton($"Record {section} previous","‹",x+width-.171f,.647f,.022f,.028f,()=>{recordPages[section]--;Render();},false,recordPages[section]>0);
                RecordText($"Record {section} page",$"{recordPages[section]+1}/{pages}",x+width-.147f,.647f,.041f,.028f,10,TextAlignmentOptions.Center);
                RecordButton($"Record {section} next","›",x+width-.104f,.647f,.022f,.028f,()=>{recordPages[section]++;Render();},false,recordPages[section]<pages-1);
            }
            var selected=entries.FirstOrDefault(e=>e.id==recordSelected[section]);
            if(selected==null)
            {RecordText("Record detail "+section,"아직 보유한 "+title+"이 없습니다.",x+.012f,.835f,width-.03f,.081f,16,TextAlignmentOptions.Center);return;}
            RecordImage("Record detail icon "+section,selected.icon,x+.007f,.833f,.050f,.080f);
            RecordText("Record detail title "+section,selected.title,x+.067f,.825f,width-.151f,.033f,18).fontStyle=FontStyles.Bold;
            var badge=RecordShape("Record badge "+section,content,x+width-.080f,.826f,.072f,.026f,selected.color,false,9);
            var bt=Label(badge.transform,selected.badge,0,0,1,1,11);bt.color=Color.white;
            RecordDescription(section,selected.body,x+.067f,.864f,width-.080f,.072f);
        }
        void RecordDescription(int section,string body,float x,float y,float w,float h)
        {
            var holder=RecordRect("Record description scroll "+section,content,x,y,w,h);
            holder.gameObject.AddComponent<Image>().color=Color.clear;
            var scroll=holder.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=22;
            var viewport=Rect("Viewport",holder,0,0,.965f,1);viewport.gameObject.AddComponent<RectMask2D>();
            var text=Label(viewport,body,0,1,1,1,14);text.name="Record detail "+section;
            text.alignment=TextAlignmentOptions.TopLeft;text.color=RecordInk;text.enableAutoSizing=false;text.enableWordWrapping=true;
            text.rectTransform.pivot=new Vector2(.5f,1);text.rectTransform.sizeDelta=new Vector2(0,0);
            var fitter=text.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=text.rectTransform;
            var track=Rect("Scrollbar",holder,.975f,0,1,1);var trackImage=track.gameObject.AddComponent<Image>();trackImage.color=new Color(.80f,.62f,.42f,.35f);
            var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;
            var handle=Rect("Handle",track,0,0,1,1);var handleImage=handle.gameObject.AddComponent<Image>();handleImage.color=RecordGold;bar.handleRect=handle;bar.targetGraphic=handleImage;
            scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;scroll.verticalNormalizedPosition=1;
        }
        void RecordMap(Character player)
        {
            var map=ExplorationMapSystem.Instance;
            var viewport=RecordRect("Record map viewport",content,.499f,.214f,.347f,.360f);viewport.gameObject.AddComponent<RectMask2D>();
            if(map!=null&&map.FullMapTexture!=null)
            {
                var raw=Rect("Live exploration map",viewport,0,0,1,1).gameObject.AddComponent<RawImage>();raw.texture=map.FullMapTexture;raw.uvRect=map.BookMapUV;raw.raycastTarget=false;
                var points=Rect("Record live entities",viewport,0,0,1,1);
                int count=0;
                foreach(var monster in FindObjectsOfType<Monster>())
                {
                    if(count>=160)break;if(!monster.gameObject.activeInHierarchy||monster.HP<=0||monster.GetComponent<MapMarker>()!=null)continue;
                    if(!map.TryBookPoint(monster.transform.position,out var point))continue;
                    var icon=monster is BossMonster||monster is MiniBossMonster?OctoberArt.Get("OctoberUI/MapSymbols","Boss"):monster.Blueprint is EliteMonsterBlueprint?RunBookArt.Icon(13):RunBookArt.Icon(12);
                    RecordMapPoint(points,point,icon,monster is BossMonster||monster is MiniBossMonster?16:8);count++;
                }
                count=0;
                foreach(var coin in FindObjectsOfType<Coin>())
                    if(count<60&&map.TryBookPoint(coin.transform.position,out var point)){RecordMapPoint(points,point,RunBookArt.Icon(11),9);count++;}
                count=0;
                foreach(var gem in FindObjectsOfType<ExpGem>())
                    if(count<60&&map.TryBookPoint(gem.transform.position,out var point)){RecordMapPoint(points,point,RunBookArt.Icon(14),8);count++;}
                map.CopyBookMarkers(viewport);
            }
            else RecordText("Map unavailable","탐험 지도를 준비하고 있습니다",.515f,.355f,.305f,.065f,18,TextAlignmentOptions.Center);
            string[] labels={"플레이어","일반 몬스터","엘리트 몬스터","보스","보물상자","골드","경험치 보석","특수 오브젝트","혈전","상인","자판기","보스 제단","미니보스"};
            Sprite[] icons={CharacterProfile(player.Blueprint),RunBookArt.Icon(12),RunBookArt.Icon(13),OctoberArt.Get("OctoberUI/MapSymbols","Boss"),OctoberArt.Get("OctoberUI/ItemChestIcon"),RunBookArt.Icon(11),RunBookArt.Icon(14),RunBookArt.Icon(15),OctoberArt.Get("OctoberUI/MapSymbols","Portal"),OctoberArt.Get("OctoberUI/MapSymbols","Merchant"),OctoberArt.Get("OctoberUI/MapSymbols","Vending"),OctoberArt.Get("OctoberUI/BossAltarIcon"),ChameleonArt.Portrait(ChameleonKind.Fanta,0,true)};
            for(int i=0;i<labels.Length;i++)
            {
                float y=.218f+i*.0273f;RecordImage("Map legend icon "+i,icons[i],.866f,y,.023f,.025f);
                RecordText("Map legend "+i,labels[i],.895f,y,.069f,.025f,12);
            }
        }
        void RecordMapPoint(RectTransform parent,Vector2 point,Sprite sprite,float size)
        {
            var image=ImageAt(parent,sprite,point.x,point.y,point.x,point.y);image.name="Record live map marker";image.rectTransform.sizeDelta=Vector2.one*size;
        }
    }
}
