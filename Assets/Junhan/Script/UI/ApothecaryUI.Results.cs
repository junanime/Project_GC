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
        Color ResultAccent => passed?new Color(.91f,.35f,.46f):new Color(.49f,.25f,.77f);
        void ResultHeader(string title,float x,float y,float r,float t)
        {
            ImageAt(content,null,x,y,r,t,false).color=ResultAccent;
            Label(content,title,x+.008f,y,r-.008f,t,22).color=Color.white;
        }
        void OctoberResult()
        {
            previewCharacter=null;
            var resultPortrait=passed?OctoberArt.Character(character,2):CharacterExitSequence.ResultPortrait(character);
            portrait=ImageAt(content,resultPortrait??OctoberArt.Character(character,passed?2:1),.07f,.66f,.235f,.84f);
            var player=level?.PlayerCharacter;
            float time=level!=null?level.CurrentLevelTime:0;
            string[] names={"플레이 시간","처치한 적","획득한 코인","도달 레벨","가한 피해","받은 피해","회복량","획득 실버"};
            var damage=RunResultData.DamageRows(AugmentDamageTracker.Instance?.AugmentDamages);
            float total=damage.Sum(row=>row.damage);
            string[] values={$"{(int)time/60:00}:{(int)time%60:00}",$"{stats?.MonstersKilled??0:N0}",$"{stats?.CoinsGained??0:N0}",$"Lv. {player?.CurrentLevel??1}",
                total.ToString("N0"),$"{stats?.DamageTaken??0:N0}",$"{stats?.HealingReceived??0:N0}",$"{SilverRunRewarder.Instance?.RunEarnedSilver??0:N0}"};
            for(int i=0;i<8;i++)
            {
                float x=.26f+i%4*.17f,y=.72f-i/4*.08f;
                var n=Label(content,names[i],x,y+.042f,x+.161f,y+.08f,17);n.name="Result metric "+names[i];
                Label(content,values[i],x,y,x+.161f,y+.041f,22).fontStyle=FontStyles.Bold;
            }
            ResultHeader("공격 기여도",.065f,.586f,.578f,.636f);
            ResultHeader("이벤트 기록",.595f,.586f,.935f,.636f);
            var history=AugmentHistoryManager.Instance?.Entries;
            for(int i=0;i<damage.Count;i++)
            {
                var row=damage[i];float y=.532f-i*.047f;
                var icons=row.sources.Select(name=>new RunBookData.Entry{title=name,icon=ResultSourceIcon(name)}).ToList();
                ResultIcons("Damage icons "+i,icons,.071f,y,.115f,y+.041f,26);
                var name=Label(content,row.name,.12f,y,.265f,y+.041f,17);name.alignment=TextAlignmentOptions.MidlineLeft;
                name.enableWordWrapping=false;name.overflowMode=TextOverflowModes.Ellipsis;name.raycastTarget=true;
                if(row.name=="기타 증강")HudTooltip.Bind(name.gameObject,()=>"기타 증강",()=>row.sources.Length>0?string.Join("\n",row.sources):"추가 피해 기록 없음");
                ImageAt(content,null,.267f,y+.01f,.444f,y+.031f,false).color=new Color(.85f,.84f,.82f);
                if(total>0&&row.damage>0)ImageAt(content,null,.267f,y+.01f,.267f+.177f*(row.damage/total),y+.031f,false).color=RunResultData.DamageColors[row.name=="기타 증강"?4:i];
                var amount=Label(content,row.damage.ToString("N0"),.448f,y,.565f,y+.041f,18);amount.name="Result damage "+row.name;amount.alignment=TextAlignmentOptions.Right;
            }
            if(total==0)Label(content,"아직 기록된 공격이 없습니다",.13f,.43f,.54f,.48f,17);
            string[] events={"미니스테이지 성공","엘리트 몬스터 처치","일반 몬스터 처치","보급 캡슐 획득"};
            int[] counts={stats?.MiniStagesCleared??0,stats?.EliteMonstersKilled??0,stats?.NormalMonstersKilled??0,stats?.SupplyCapsulesCollected??0};
            Sprite[] eventIcons={OctoberArt.Get("OctoberUI/MapSymbols","Portal"),RunBookArt.Icon(13),RunBookArt.Icon(12),SupplyCapsuleArt.Closed};
            for(int i=0;i<4;i++)
            {
                float y=.516f-i*.06f;
                ResultIcons("Event icons "+i,new[]{new RunBookData.Entry{title=events[i],icon=eventIcons[i]}},.602f,y,.642f,y+.045f,30);
                var text=Label(content,events[i],.647f,y,.866f,y+.045f,17);text.alignment=TextAlignmentOptions.MidlineLeft;
                Label(content,counts[i].ToString("N0"),.869f,y,.926f,y+.045f,21).name="Result event "+i;
            }
            ResultHeader("이번 탐험의 기록",.065f,.277f,.935f,.324f);
            string[] headings={"획득 실버","보유 아이템","보유 유물","획득 증강"};
            for(int i=0;i<4;i++)
            {
                float x=.065f+i*.220f;Panel(content,x,.128f,x+.210f,.267f);
                Label(content,headings[i],x+.005f,.226f,x+.205f,.261f,16);
                if(i==0)
                {
                    ImageAt(content,Config.items.FirstOrDefault(item=>item.octoberId==39)?.itemIcon,x+.015f,.145f,x+.082f,.215f);
                    Label(content,$"{SilverRunRewarder.Instance?.RunEarnedSilver??0:N0}",x+.09f,.153f,x+.20f,.213f,26);
                }
                else
                {
                    IEnumerable<RunBookData.Entry> entries=i==1?OctoberOwnedItems().Select(item=>new RunBookData.Entry{title=item.itemName,body=item.description,icon=item.itemIcon}):
                        i==2?(player?.Relics?.Owned??Array.Empty<RelicBlueprint>()).Select(relic=>new RunBookData.Entry{title=relic.relicName,body=relic.Description,icon=relic.icon}):
                        (history??Array.Empty<AugmentHistoryManager.AugmentEntry>()).Select(entry=>new RunBookData.Entry{title=entry.displayName,body="강화 단계 "+entry.level,icon=entry.icon});
                    ResultIcons("Result inventory "+i,entries,x+.01f,.14f,x+.2f,.224f,48);
                }
            }
            ActionButton(content,"메인으로",.13f,.02f,.355f,.105f,()=>ReturnToLobby(false));
            ActionButton(content,"다시하기",.387f,.02f,.613f,.105f,()=>{CrossSceneData.CharacterBlueprint=character;CrossSceneData.ClearStartingLobbyItems();StageEntryLoading.Begin(character);});
            ActionButton(content,"준비화면",.645f,.02f,.87f,.105f,()=>ReturnToLobby(true),true);
        }
        Sprite ResultSourceIcon(string name)
        {
            var entry=AugmentHistoryManager.Instance?.Entries.FirstOrDefault(e=>e.displayName==name);
            if(entry?.icon!=null)return entry.icon;
            var weapon=Config.weapons?.FirstOrDefault(w=>w!=null&&(w.Name==name||Ver4AugmentCatalog.ParentNames[(int)w.Type]==name));
            if(weapon!=null)return weapon.Image;
            if(character?.skills!=null&&name==character.skills.activeName)return character.skills.activeIcon;
            if(character?.skills!=null&&name==character.skills.passiveName)return character.skills.passiveIcon;
            return Config.basicNeedle;
        }
        void ResultIcons(string name,IEnumerable<RunBookData.Entry> source,float x,float y,float right,float top,float preferred)
        {
            var items=source.ToArray();var root=Rect(name,content,x,y,right,top);root.gameObject.AddComponent<RectMask2D>();
            float width=(right-x)*1280,height=(top-y)*720;
            var grid=RunResultData.IconGrid(items.Length,width,height,preferred);
            float cell=Mathf.Min(preferred,width/grid.x,height/grid.y);
            for(int i=0;i<items.Length;i++)
            {
                var entry=items[i];float left=(i/grid.y)*cell/width,bottom=1-(i%grid.y+1)*cell/height;
                var icon=ImageAt(root,entry.icon,left,bottom,left+cell/width,bottom+cell/height);
                icon.rectTransform.offsetMin=new Vector2(1,1);icon.rectTransform.offsetMax=new Vector2(-1,-1);
                icon.name=name+" icon "+i;icon.raycastTarget=true;
                HudTooltip.Bind(icon.gameObject,()=>entry.title,()=>entry.body??entry.title);
            }
        }
    }
}
