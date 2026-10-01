using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        bool prescriptionExpanded=true;
        RectTransform prescriptionHud;
        TextMeshProUGUI prescriptionTitle;
        readonly TextMeshProUGUI[] prescriptionRows=new TextMeshProUGUI[3];
        Button prescriptionClaim, overchargeButton;
        Action skillRewardCallback;
        bool skillRewardQueued;
        PrescriptionRuntime Prescription => level!=null && level.PlayerCharacter!=null?level.PlayerCharacter.GetComponent<PrescriptionRuntime>():null;
        void BuildPrescriptionHud()
        {
            prescriptionHud=Rect("Prescription HUD",content,0,0,1,1);
            var header=ActionButton(prescriptionHud,"",.72f,.795f,.97f,.858f,()=>{prescriptionExpanded=!prescriptionExpanded;Render();});
            header.name="Prescription toggle";
            prescriptionTitle=header.GetComponentInChildren<TextMeshProUGUI>();prescriptionTitle.fontSize=18;
            for(int i=0;i<3;i++)prescriptionRows[i]=null;
            prescriptionClaim=null;
            if(prescriptionExpanded)
            {
                Panel(prescriptionHud,.68f,.43f,.97f,.79f);
                for(int i=0;i<3;i++)
                {
                    var row=Label(prescriptionHud,"",.695f,.685f-i*.08f,.955f,.755f-i*.08f,15);
                    row.alignment=TextAlignmentOptions.MidlineLeft;prescriptionRows[i]=row;
                }
                prescriptionClaim=ActionButton(prescriptionHud,"전설 증강 선택",.73f,.445f,.95f,.503f,()=>Prescription?.TryClaim(FindObjectOfType<EntityManager>()?.AbilitySelectionDialog),true);
                prescriptionClaim.GetComponentInChildren<TextMeshProUGUI>().fontSize=17;
            }
            overchargeButton=ActionButton(prescriptionHud,"혈전 과충전 / Q",.70f,.14f,.97f,.22f,()=>FindNearbyOvercharge()?.Begin(),true);
            overchargeButton.gameObject.SetActive(false);
        }
        BloodClotOvercharge FindNearbyOvercharge()
        {
            foreach(var charge in FindObjectsOfType<BloodClotOvercharge>())if(charge.PlayerInside)return charge;
            return null;
        }
        void UpdatePrescriptionUI()
        {
            if(Page=="hud")
            {
                bool rewardOpen=level!=null && level.EntityManager!=null && level.EntityManager.AbilitySelectionDialog.MenuOpen;
                if(content!=null)content.gameObject.SetActive(!rewardOpen);
                if(prescriptionHud!=null)prescriptionHud.gameObject.SetActive(!rewardOpen);
                var quest=Prescription;
                if(quest!=null && prescriptionTitle!=null)
                {
                    prescriptionTitle.text=$"비전 처방전 {quest.Completed}/3  {(prescriptionExpanded?"접기":"펼치기")}";
                    for(int i=0;i<3;i++)if(prescriptionRows[i]!=null)
                    {
                        string line=$"[{PrescriptionRuntime.Tags[quest.Tag]}] {quest.Description(i)}\n{Mathf.FloorToInt(quest.Progress(i))}/{quest.Target(i):0}";
                        prescriptionRows[i].text=quest.IsComplete(i)?"<s>"+line+"</s>  완료":line;
                        prescriptionRows[i].color=quest.IsComplete(i)?new Color(.35f,.4f,.3f):Ink;
                    }
                    if(prescriptionClaim!=null){prescriptionClaim.gameObject.SetActive(quest.Ready);prescriptionClaim.interactable=Time.timeScale>0;}
                }
                if(overchargeButton!=null)
                {
                    var charge=FindNearbyOvercharge();
                    overchargeButton.gameObject.SetActive(charge!=null&&!charge.Started&&!MiniStageRuntimeState.IsInsideMiniStage);
                    overchargeButton.interactable=Time.timeScale>0;
                }
                if(skillRewardQueued && Time.timeScale>0 && CurrentSkills!=null && !CurrentSkills.IsCutin)
                {
                    var dialog=FindObjectOfType<EntityManager>()?.AbilitySelectionDialog;
                    if(dialog==null || !dialog.MenuOpen)
                    {previousTime=Time.timeScale;ownsPause=true;Time.timeScale=0;Show("skillReward");}
                }
            }
        }
        public bool RequestSkillReward(Action complete)
        {
            if(skillRewardQueued || CurrentSkills==null)return false;
            skillRewardQueued=true;skillRewardCallback=complete;
            return true;
        }
        void BuildSkillReward()
        {
            Label(content,"처방 강화",.25f,.84f,.75f,.94f,34);
            Label(content,"패시브 또는 액티브 하나를 강화하세요 · 최대 Lv.5",.15f,.75f,.85f,.82f,23);
            var skills=CurrentSkills;
            if(skills==null)return;
            for(int i=0;i<2;i++)
            {
                bool active=i==1;float x=.16f+i*.36f;
                Panel(content,x,.20f,x+.32f,.72f);
                var d=skills.Definition;int current=active?skills.ActiveLevel:skills.PassiveLevel;
                ImageAt(content,active?d.activeIcon:d.passiveIcon,x+.08f,.49f,x+.24f,.68f);
                Label(content,active?"액티브 강화":"패시브 강화",x+.02f,.425f,x+.30f,.49f,25);
                Label(content,skills.UpgradeDescription(active),x+.025f,.30f,x+.295f,.425f,19);
                ActionButton(content,current>=5?"최대 레벨":$"Lv.{current} → Lv.{current+1}",x+.04f,.22f,x+.28f,.295f,()=>FinishSkillReward(active),true,current<5);
            }
            if(skills.PassiveLevel>=5 && skills.ActiveLevel>=5)
            {
                Label(content,"두 스킬 모두 최대 레벨 · 최대 체력의 20% 회복으로 대체",.12f,.12f,.88f,.185f,20);
                ActionButton(content,"회복하고 계속",.34f,.025f,.66f,.105f,()=>FinishSkillReward(null),true);
            }
        }
        void FinishSkillReward(bool? active)
        {
            if(!skillRewardQueued || CurrentSkills==null)return;
            if(active.HasValue && !CurrentSkills.TryUpgrade(active.Value))return;
            if(!active.HasValue)level.PlayerCharacter.GainHealth(level.PlayerCharacter.MaxHealth*.2f);
            skillRewardQueued=false;var callback=skillRewardCallback;skillRewardCallback=null;
            CloseRunBook();
            callback?.Invoke();
        }
    }
}
