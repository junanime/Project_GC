using System.Linq;
using UnityEngine;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        public void OpenSkillHelp()
        {
            OpenRunBook();
            if (Page == "run") SwitchTab(0);
        }
        void ConsumableBook()
        {
            Label(content,"소모품 · 설명을 눌러도 사용되지 않습니다",.15f,.66f,.85f,.72f,23);
            var slots = level?.PlayerInventory?.Slots;
            if (slots == null) return;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i]; if (slot == null) continue;
                int index = i;
                float x = .15f + i % 2 * .36f, y = .43f - i / 2 * .21f;
                var b = ActionButton(content,"",x,y,x+.34f,y+.19f,()=>OctoberDetails(ConsumableHelp.Title(slot),ConsumableHelp.Body(slot,index)));
                SlotArt(b); var visual = b.transform.Find("Visual");
                ImageAt(visual,slot.IconImage != null ? slot.IconImage.sprite : null,.03f,.15f,.26f,.85f);
                Label(visual,ConsumableHelp.Title(slot)+"\n"+slot.CapacityDescription+"\n"+ConsumableHelp.Keys(index),.28f,.08f,.97f,.92f,21);
                HudTooltip.Bind(b.gameObject,()=>ConsumableHelp.Title(slot),()=>ConsumableHelp.Body(slot,index));
            }
            Label(content,"PC: 1~4 또는 Z·X·C·V / 모바일: 하단 약함 터치",.16f,.155f,.84f,.205f,18);
        }
        void GuideBook()
        {
            var guide = TutorialGuide.Instance;
            var entries = guide != null ? guide.Entries.OrderBy(e=>e.kind).ThenBy(e=>e.title).ToArray() : new TutorialGuide.Entry[0];
            int pages = Mathf.Max(1,Mathf.CeilToInt(entries.Length/6f)); pageIndex = Mathf.Clamp(pageIndex,0,pages-1);
            Label(content,"안내 다시 보기 · 읽는 동안 게임은 잠시 멈춥니다",.14f,.66f,.86f,.72f,22);
            for(int j=0;j<6;j++)
            {
                int index=pageIndex*6+j;if(index>=entries.Length)break;var entry=entries[index];
                float x=.15f+j%3*.235f,y=.43f-j/3*.20f;
                string kind=entry.kind=="weapon"?"무기":entry.kind=="event"?"이벤트":entry.kind=="mechanic"?"필드 기믹":entry.kind=="item"?"소모품":"몬스터";
                var b=ActionButton(content,kind+"\n"+entry.title,x,y,x+.22f,y+.18f,()=>guide.Review(entry.id));
                HudTooltip.Bind(b.gameObject,()=>entry.title,()=>entry.what+"\n\n"+entry.tip);
            }
            ActionButton(content,"‹",.38f,.16f,.43f,.218f,()=>{pageIndex--;Render();},false,pageIndex>0);
            Label(content,$"{pageIndex+1} / {pages}",.45f,.16f,.55f,.218f,16);
            ActionButton(content,"›",.57f,.16f,.62f,.218f,()=>{pageIndex++;Render();},false,pageIndex<pages-1);
        }
        static string SkillTitle(CharacterBlueprint data,bool active)
        {
            var d=data!=null?data.skills:null;
            return d!=null?(active?d.activeName:d.passiveName):"스킬 준비 중";
        }
        string SkillHelp(CharacterBlueprint data,bool active)
        {
            var d=data!=null?data.skills:null;
            if(d==null)return "이 캐릭터의 스킬은 추후 추가됩니다.";
            string text=active?d.activeDescription:d.passiveDescription;
            text=text.Replace(" 수치는 ShiniSkills에서 조절할 수 있습니다.","");
            if(CurrentSkills!=null&&data==character)
                text+=$"\n\n현재 Lv.{(active?CurrentSkills.ActiveLevel:CurrentSkills.PassiveLevel)}"+(active?(CurrentSkills.IsShini?" · 불씨 소모형":$" · 재사용 {CurrentSkills.EffectiveCooldown:0.#}초"):"");
            if(CurrentSkills!=null&&data==character)text+="\n"+CurrentSkills.UpgradeDescription(active).Split('\n')[0]+": "+CurrentSkills.UpgradeDescription(active).Split('\n')[1].Split('→')[0].Trim();
            return text;
        }
    }
    public static class ConsumableHelp
    {
        static string Id(InventorySlot slot)
        {
            string n=slot.CollectableType!=null?slot.CollectableType.name.ToLowerInvariant():"";
            return n.Contains("bomb")?"bomb":n.Contains("magnet")?"magnet":n.Contains("health")||n.Contains("heart")?"health":"potion";
        }
        static TutorialGuide.Entry Entry(InventorySlot slot) => TutorialGuide.Instance?.Entries.FirstOrDefault(e=>e.id=="item/"+Id(slot));
        public static string Title(InventorySlot slot) => Entry(slot)?.title ?? (Id(slot)=="bomb"?"폭탄":Id(slot)=="health"?"체력 회복":Id(slot)=="magnet"?"자석":"몬스터 증식 물약");
        public static string Keys(int index) => index>=0&&index<4?$"{index+1} / {"ZXCV"[index]}":"약함 터치";
        public static string Body(InventorySlot slot,int index)
        {
            string description=Entry(slot)?.what;
            if(string.IsNullOrEmpty(description)) description=Id(slot)=="bomb"?"화면 안의 적에게 피해를 줍니다.":Id(slot)=="health"?"체력을 즉시 회복합니다. 최대 체력을 넘겨 회복하지 않습니다.":Id(slot)=="magnet"?"필드의 코인과 경험치 보석을 한꺼번에 끌어옵니다.":"일정 시간 일반 몬스터의 스폰량을 늘립니다.";
            return description+$"\n\n{slot.CapacityDescription}\n사용: {Keys(index)} 또는 하단 약함 터치";
        }
    }
}
