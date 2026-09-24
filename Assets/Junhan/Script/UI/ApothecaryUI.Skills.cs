using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        Image activeCooldown, passiveDuration, cutinPortrait, activeDurationBar;
        TextMeshProUGUI activeSeconds, passiveSeconds;
        Button activeSkillButton;
        GameObject cutinPanel, skillDetails;
        RectTransform skillHudRoot;
        CharacterSkillRuntime CurrentSkills => level!=null && level.PlayerCharacter!=null ? level.PlayerCharacter.Skills : null;
        void ClearSkillUI()
        {
            activeCooldown=passiveDuration=cutinPortrait=activeDurationBar=null;
            activeSeconds=passiveSeconds=null; activeSkillButton=null;cutinPanel=skillDetails=null;skillHudRoot=null;
        }
        void ProfileSkills(CharacterBlueprint data,float x,float y,float right,float top)
        {
            var d=data!=null?data.skills:null;
            float width=(right-x)/2;
            for(int i=0;i<2;i++)
            {
                bool active=i==1;
                var b=ActionButton(content,"",x+i*width,y,x+(i+1)*width-.006f,top,()=>ShowSkillDetails(data,active));
                b.name=active?"Active skill slot":"Passive skill slot";SlotArt(b);
                var visual=b.transform.Find("Visual");
                ImageAt(visual,d!=null?(active?d.activeIcon:d.passiveIcon):null,.16f,.29f,.84f,.96f);
                if(d==null)Label(visual,"?",.2f,.3f,.8f,.9f,24);
                var caption=ImageAt(visual,null,.09f,.13f,.91f,.31f,false);caption.color=Paper;
                Label(visual,d!=null?(active?d.activeName:d.passiveName):(active?"액티브 준비 중":"패시브 준비 중"),.05f,.13f,.95f,.31f,13);
            }
        }
        void ShowSkillDetails(CharacterBlueprint data,bool active)
        {
            if(CurrentSkills!=null && CurrentSkills.IsCutin)return;
            if(skillDetails!=null){Destroy(skillDetails);skillDetails=null;}
            var overlay=Rect("Skill details",content,.2f,.32f,.8f,.7f);skillDetails=overlay.gameObject;
            Panel(overlay,0,0,1,1);
            var d=data!=null?data.skills:null;
            var icon=ImageAt(overlay,d!=null?(active?d.activeIcon:d.passiveIcon):null,.05f,.32f,.29f,.88f); icon.name="Skill detail icon";
            Label(overlay,d!=null?(active?d.activeName:d.passiveName):"스킬 준비 중",.34f,.70f,.94f,.93f,27);
            Label(overlay,d!=null?(active?d.activeDescription:d.passiveDescription):"이 캐릭터의 스킬은 추후 추가됩니다.",.34f,.30f,.94f,.69f,21);
            ActionButton(overlay,"닫기",.34f,.06f,.66f,.27f,()=>{Destroy(skillDetails);skillDetails=null;});
        }
        void BuildSkillHud()
        {
            // Kept above the mobile movement stick, inside the landscape safe area.
            float y=.035f;
            skillHudRoot=Rect("Skill HUD",content,0,0,1,1);
            var d=character!=null?character.skills:null;
            for(int i=0;i<2;i++)
            {
                bool active=i==1;float x=.025f+i*.074f;
                var b=ActionButton(skillHudRoot,"",x,y,x+.065f,y+.115f,()=>{if(active)CurrentSkills?.TryActivate();else ShowSkillDetails(character,false);});
                b.name=active?"Active skill R":"Passive skill status";SlotArt(b);
                var visual=b.transform.Find("Visual");
                ImageAt(visual,d!=null?(active?d.activeIcon:d.passiveIcon):null,.03f,.03f,.97f,.97f);
                if(d==null)Label(visual,"?",.2f,.15f,.8f,.85f,24);
                var mask=ImageAt(visual,null,.03f,.03f,.97f,.97f,false);
                mask.name="Clockwise cooldown cover";mask.color=new Color(0,0,0,.72f);
                mask.sprite=SkillSquare;mask.type=Image.Type.Filled;mask.fillMethod=Image.FillMethod.Radial360;
                mask.fillOrigin=(int)Image.Origin360.Top;mask.fillClockwise=false;mask.fillAmount=0;
                var seconds=Label(visual,"",.15f,.25f,.85f,.75f,22);seconds.color=Color.white;seconds.fontStyle=FontStyles.Bold;
                Label(skillHudRoot,active?"R · 액티브":"패시브",x,y-.028f,x+.065f,y,13).color=Color.white;
                if(active)
                {
                    activeCooldown=mask;activeSeconds=seconds;activeSkillButton=b;
                    activeDurationBar=ImageAt(visual,SkillSquare,.03f,0,.97f,.05f,false);
                    activeDurationBar.color=new Color(.5f,1,.83f);activeDurationBar.type=Image.Type.Filled;activeDurationBar.fillMethod=Image.FillMethod.Horizontal;
                }
                else {passiveDuration=mask;passiveSeconds=seconds;}
            }
            var band=Rect("Sprint cut-in",content,0,.27f,1,.73f);cutinPanel=band.gameObject;
            var shade=band.gameObject.AddComponent<Image>();shade.color=new Color(0,0,0,.76f);shade.raycastTarget=true;
            cutinPortrait=ImageAt(band,null,.33f,0,.67f,1);
            cutinPortrait.name="Ashi cut-in portrait";cutinPortrait.color=Color.white;
            Label(band,"단거리 경주",.04f,.37f,.30f,.64f,34).color=new Color(1,.85f,.38f);
            Label(band,"발사체 ×2\n이동속도 ×2\n발사체 속도 ×2",.72f,.25f,.96f,.75f,24).color=Color.white;
            cutinPanel.SetActive(false);
        }
        static Sprite square;
        static Sprite SkillSquare
        {
            get
            {
                if(square==null) square=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),new Vector2(.5f,.5f));
                return square;
            }
        }
        void UpdateSkillUI()
        {
            if(level!=null && ExplorationMapSystem.Instance!=null)ExplorationMapSystem.Instance.SetMiniMapVisible(false);
            var skill=CurrentSkills;
            if(activeSkillButton==null)return;
            skillHudRoot.anchoredPosition=new Vector2(0,(Application.isMobilePlatform||MobileGameplayInput.Active)?205:0);
            var d=skill!=null?skill.Definition:null;
            activeSkillButton.interactable=skill!=null&&skill.CanActivate;
            activeCooldown.fillAmount=d!=null?skill.CooldownRemaining/d.cooldown:0;
            activeSeconds.text=skill!=null&&skill.IsSummoning?"소환":d!=null&&skill.CooldownRemaining>0?Mathf.CeilToInt(skill.CooldownRemaining).ToString():"";
            // Marathon has no separate cooldown: its countdown is the remaining buff duration.
            passiveDuration.fillAmount=0;
            passiveSeconds.text=d!=null&&skill.IsHyuki?Mathf.RoundToInt(skill.IceProcChance*100)+"%":d!=null&&skill.PassiveActive?Mathf.CeilToInt(skill.PassiveRemaining).ToString():"";
            activeDurationBar.fillAmount=d!=null?skill.ActiveRemaining/d.activeDuration:0;
            cutinPanel.SetActive(skill!=null&&skill.IsCutin);
            if(skill!=null&&skill.IsCutin&&d.cutin!=null&&d.cutin.Length>0)
            {
                float progress=Mathf.Clamp01(skill.CutinElapsed/d.cutinDuration);
                int frame=Mathf.Min(d.cutin.Length-1,(int)(progress*d.cutin.Length));
                cutinPortrait.sprite=d.cutin[frame];
                cutinPortrait.rectTransform.anchoredPosition=new Vector2(Mathf.Lerp(-110,0,Mathf.Clamp01(progress*7)),0);
            }
        }
    }
}
