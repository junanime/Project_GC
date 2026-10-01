using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public partial class AbilityCard
    {
        IdentityShape trainingFrame, trainingGrade, trainingEffect;
        TextMeshProUGUI effectLabel, progressLabel;
        Image progressRule;
        bool UsesTrainingLayout => themedCard && !UsesNoblePanel;

        void InstallTrainingDecoration(AugmentUpgradeGrade grade)
        {
            if(trainingFrame==null)
            {
                trainingFrame=TrainingUITheme.Pill("Glossy training frame",cardBackgroundImage.transform,Color.white,Vector2.zero,Vector2.one);
                trainingFrame.frame=true;trainingFrame.sparkle=true;trainingFrame.glossy=true;trainingFrame.radius=19;
                trainingFrame.insetColor=new Color(1,.975f,.90f);
                trainingGrade=TrainingUITheme.Pill("Rounded grade ribbon",transform,Color.white,new Vector2(.29f,.568f),new Vector2(.71f,.613f));
                trainingEffect=TrainingUITheme.Pill("Effect name ribbon",transform,new Color(1,.79f,.81f),new Vector2(.08f,.410f),new Vector2(.92f,.474f));
                effectLabel=AugmentPanelTheme.Label("Effect name",trainingEffect.transform,TrainingUITheme.Font,"",18);
                AugmentPanelTheme.Anchors(effectLabel.rectTransform,new Vector2(.03f,.02f),new Vector2(.97f,.98f));
                effectLabel.color=OctoberArt.Ink;
                progressRule=AugmentPanelTheme.Fill("Progress divider",transform,new Color(.88f,.69f,.53f,.65f));
                AugmentPanelTheme.Anchors(progressRule.rectTransform,new Vector2(.12f,.133f),new Vector2(.88f,.135f));
                progressLabel=AugmentPanelTheme.Label("Upgrade progress",transform,TrainingUITheme.Font,"",14);
                AugmentPanelTheme.Anchors(progressLabel.rectTransform,new Vector2(.10f,.038f),new Vector2(.90f,.124f));
                progressLabel.color=new Color(.12f,.25f,.45f);
                // A small gold flourish separates weapon identity from the effect below it.
                for(int i=0;i<3;i++)
                    TrainingUITheme.Pill("Name ornament "+i,transform,new Color(.80f,.55f,.35f),new Vector2(.41f+i*.065f,.478f),new Vector2(.445f+i*.065f,.487f));
                for(int i=0;i<2;i++)
                {
                    var leaf=AugmentPanelTheme.Fill("Training leaf "+i,transform,Color.white);
                    leaf.sprite=OctoberArt.Get("TrainingUI/HudLeaf");leaf.preserveAspect=true;
                    AugmentPanelTheme.Anchors(leaf.rectTransform,new Vector2(i==0?.025f:.912f,.02f),new Vector2(i==0?.088f:.975f,.07f));
                    if(i==1)leaf.rectTransform.localScale=new Vector3(-1,1,1);
                }
            }
            bool enabled=UsesTrainingLayout;
            trainingFrame.gameObject.SetActive(enabled);trainingGrade.gameObject.SetActive(enabled);
            trainingEffect.gameObject.SetActive(enabled);progressRule.gameObject.SetActive(enabled);progressLabel.gameObject.SetActive(enabled);
            for(int i=0;i<3;i++)transform.Find("Name ornament "+i).gameObject.SetActive(enabled);
            for(int i=0;i<2;i++)transform.Find("Training leaf "+i).gameObject.SetActive(enabled);
            if(!enabled)return;
            var accent=AugmentPanelTheme.Accent(grade);
            trainingFrame.color=accent;trainingGrade.color=Color.Lerp(accent,Color.white,.3f);
            gradeBorder.color=Color.clear;gradeBody.color=Color.clear;
            AugmentPanelTheme.Anchors(gradeBorder.rectTransform,new Vector2(.29f,.51f),new Vector2(.71f,.555f));
            gradeLabel.transform.SetParent(trainingGrade.transform,false);
            AugmentPanelTheme.Anchors(gradeLabel.rectTransform,Vector2.zero,Vector2.one);
            TrainingUITheme.Text(gradeLabel,16,OctoberArt.Ink);
            titleRule.gameObject.SetActive(false);
            cardBackgroundImage.color=Color.clear;
            var button=cardBackgroundImage.GetComponent<Button>();
            if(button!=null)
            {
                button.targetGraphic=trainingFrame;
                button.colors=ColorBlock.defaultColorBlock;
                if(button.GetComponent<UIButtonPressEffect>()==null)button.gameObject.AddComponent<UIButtonPressEffect>();
            }
            // Original PNG frames remain available to the merchant and Noble card paths.
            AugmentPanelTheme.Anchors(abilityImageRect,new Vector2(.11f,.618f),new Vector2(.89f,.988f));
        }

        void SetTrainingText()
        {
            foreach(var localization in GetComponentsInChildren<LocalizeFontEvent>(true))
            {
                localization.ThemeFontOverride=UsesTrainingLayout?TrainingUITheme.Font:null;
                if(!UsesTrainingLayout&&localization.LocalizedFont!=null)
                {
                    nameText.font=descriptionText.font=localization.LocalizedFont;
                    nameText.fontSharedMaterial=descriptionText.fontSharedMaterial=localization.LocalizedFont.material;
                }
            }
            if(!UsesTrainingLayout)return;
            TrainingUITheme.Text(nameText,28,OctoberArt.Ink);
            FitCardText(nameText,new Vector2(.075f,.495f),new Vector2(.925f,.562f),28,TextAlignmentOptions.Center);
            TrainingUITheme.Text(descriptionText,17,OctoberArt.Ink);
            FitCardText(descriptionText,new Vector2(.09f,.148f),new Vector2(.91f,.402f),17,TextAlignmentOptions.Center);
            descriptionText.fontSizeMin=12;
            var lines=PanelDescription().Split('\n').Where(s=>!string.IsNullOrWhiteSpace(s)).ToList();
            var progress=lines.Where(s=>s.StartsWith("이 항목 ")||s.StartsWith("오리지널 Lv.")||s.StartsWith("최초 획득")||s.Contains("지존 승급 완료")).ToList();
            lines.RemoveAll(s=>progress.Contains(s));
            if(ability is Ver4AugmentOffer offer)
            {
                effectLabel.text=offer.Kind==Ver4RewardKind.Original&&lines.Count>0?lines[0]:offer.Kind==Ver4RewardKind.NewSpecial?"새로운 침 획득":"공유 침 강화";
                if(offer.Kind==Ver4RewardKind.Original&&lines.Count>0)lines.RemoveAt(0);
                if(progress.Count==0)progress.Add(offer.Kind==Ver4RewardKind.Numeric?"모든 침에 적용":"선택 시 획득");
            }
            else effectLabel.text="증강 효과";
            descriptionText.text=string.Join("\n",lines);
            progressLabel.text=string.Join("\n",progress);
            descriptionText.transform.SetAsLastSibling();nameText.transform.SetAsLastSibling();
        }
    }
}
