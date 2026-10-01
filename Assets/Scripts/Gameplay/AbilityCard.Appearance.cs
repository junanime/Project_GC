using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public partial class AbilityCard
    {
        bool themedCard;
        TextMeshProUGUI gradeLabel;
        Image gradeBorder, gradeBody, titleRule, opaqueBacking;
        Image legacyDescriptionPaper;
        Sprite legacyPanelSprite;

        void ApplyPanelTheme()
        {
            var offer = ability as Ver4AugmentOffer;
            var grade = offer != null ? offer.Grade : ability.Tier == Ability.AugmentTier.Legendary ? AugmentUpgradeGrade.Legendary : AugmentUpgradeGrade.Common;
            // Serialized Legendary tier/kind is the mechanic-changing Noble reward,
            // independent of the numeric Legendary upgrade grade.
            bool noble = offer != null ? offer.Kind == Ver4RewardKind.LegendaryAbility : ability.Tier == Ability.AugmentTier.Legendary;
            var frame = noble ? null : AugmentPanelTheme.Frame(grade);
            themedCard = frame != null;
            if (!themedCard && legacyPanelSprite == null && legendaryCardBackgroundSprite != null)
            {
                var texture=legendaryCardBackgroundSprite.texture;
                // Keep the existing gold illustration; remove its large transparent margins in the UI rect.
                legacyPanelSprite=Sprite.Create(texture,new Rect(texture.width*.137f,texture.height*.114f,texture.width*.728f,texture.height*.84f),Vector2.one*.5f,100);
                legacyPanelSprite.name="Existing legendary frame";
            }
            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(282,454);
            var rootImage = GetComponent<Image>(); if (rootImage != null) rootImage.enabled = false;
            if (iconFrameImage != null) iconFrameImage.enabled = false;
            if (bottomEmblemImage != null) bottomEmblemImage.enabled = false;
            if (descriptionBackgroundImage != null) descriptionBackgroundImage.enabled = false;
            if (selectionButtonImage != null) selectionButtonImage.gameObject.SetActive(false);
            var level = transform.Find("Level"); if (level != null) level.gameObject.SetActive(false);
            if (opaqueBacking == null)
            {
                opaqueBacking = AugmentPanelTheme.Fill("Opaque card backing",transform,new Color(.035f,.065f,.06f,1));
                AugmentPanelTheme.Anchors(opaqueBacking.rectTransform,new Vector2(.055f,.04f),new Vector2(.945f,.96f));
                opaqueBacking.transform.SetAsFirstSibling();
                gradeBorder = AugmentPanelTheme.Fill("Grade border",transform,Color.white);
                AugmentPanelTheme.Anchors(gradeBorder.rectTransform,new Vector2(.34f,.50f),new Vector2(.66f,.554f));
                gradeBody = AugmentPanelTheme.Fill("Grade body",gradeBorder.transform,new Color(.035f,.065f,.06f));
                AugmentPanelTheme.Anchors(gradeBody.rectTransform,Vector2.zero,Vector2.one);
                gradeBody.rectTransform.offsetMin=Vector2.one; gradeBody.rectTransform.offsetMax=-Vector2.one;
                gradeLabel = AugmentPanelTheme.Label("Grade",gradeBody.transform,nameText.font,"",17);
                AugmentPanelTheme.Anchors(gradeLabel.rectTransform,Vector2.zero,Vector2.one);
                titleRule = AugmentPanelTheme.Fill("Name divider",transform,new Color(.7f,.75f,.65f,.7f));
                AugmentPanelTheme.Anchors(titleRule.rectTransform,new Vector2(.14f,.362f),new Vector2(.86f,.362f));
                titleRule.rectTransform.sizeDelta=new Vector2(0,1);
            }
            opaqueBacking.gameObject.SetActive(themedCard);
            gradeBorder.gameObject.SetActive(themedCard);
            titleRule.gameObject.SetActive(themedCard);
            if (cardBackgroundImage != null)
            {
                cardBackgroundImage.sprite = themedCard ? frame : legacyPanelSprite;
                cardBackgroundImage.type = Image.Type.Simple; cardBackgroundImage.color = Color.white;
                AugmentPanelTheme.Anchors(cardBackgroundImage.rectTransform,Vector2.zero,Vector2.one);
                var button = cardBackgroundImage.GetComponent<Button>();
                if (button != null)
                {
                    var colors = ColorBlock.defaultColorBlock;
                    colors.highlightedColor=new Color(.89f,1, .95f); colors.pressedColor=new Color(.7f,.8f,.75f);
                    button.colors=colors; button.targetGraphic=cardBackgroundImage;
                    button.transition=Selectable.Transition.ColorTint;
                }
            }
            if (legacyDescriptionPaper == null)
            {
                legacyDescriptionPaper=AugmentPanelTheme.Fill("Legacy description paper",transform,new Color(1,.958f,.81f));
                AugmentPanelTheme.Anchors(legacyDescriptionPaper.rectTransform,new Vector2(.10f,.09f),new Vector2(.90f,.44f));
            }
            legacyDescriptionPaper.gameObject.SetActive(!themedCard);
            descriptionText.transform.SetAsLastSibling();
            // Reparent the icon out of the old fixed-size Bounds object.
            abilityImage.transform.SetParent(transform,false);
            if (abilityImageRect == abilityImage.rectTransform)
                abilityImageRect = AugmentPanelTheme.Rect("Panel icon bounds", transform);
            AugmentPanelTheme.Anchors(abilityImageRect,new Vector2(.09f,.56f),new Vector2(.91f,themedCard ? .98f : .91f));
            abilityImage.transform.SetParent(abilityImageRect,false);
            AugmentPanelTheme.Box(abilityImage.rectTransform,Vector2.zero,Vector2.one*160);
            abilityImage.preserveAspect=true; abilityImage.raycastTarget=false;
            nameText.color=themedCard ? AugmentPanelTheme.Paper : new Color(.22f,.12f,.06f);
            nameText.fontStyle=FontStyles.Bold; nameText.raycastTarget=false;
            descriptionText.color=nameText.color; descriptionText.raycastTarget=false;
            gradeLabel.text = noble ? "고귀" : offer != null && offer.Kind == Ver4RewardKind.NewSpecial ? "특수" : AugmentUpgradeOdds.DisplayName(grade);
            gradeLabel.color=gradeBorder.color=AugmentPanelTheme.Accent(grade);
        }

        string PanelTitle()
        {
            if (!(ability is Ver4AugmentOffer offer) || !themedCard) return ability.Name;
            if (offer.Kind == Ver4RewardKind.NewSpecial && ability.Name.StartsWith("특수 증강: ")) return ability.Name.Substring(7);
            return ability.Name.Replace(" · 수치 강화", "").Replace(" · 오리지널 강화", "");
        }

        string PanelDescription()
        {
            if (!(ability is Ver4AugmentOffer offer) || !themedCard) return ability.Description;
            // The grade has its own badge. Keep every effect, numeric value and progress line.
            return ability.Description.Replace("강화 등급: "+AugmentUpgradeOdds.DisplayName(offer.Grade)+"\n", "");
        }

        void OnDestroy() { if (legacyPanelSprite != null) Destroy(legacyPanelSprite); }
    }
}
