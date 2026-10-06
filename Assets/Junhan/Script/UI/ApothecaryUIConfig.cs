using TMPro;
using UnityEngine;

namespace Vampire
{
    public sealed class ApothecaryUIConfig : ScriptableObject
    {
        public Sprite mainBackground, panelBackground, failureAshi, buttonBody;
        public Sprite bookBackground, characterStage, scrollPanel, primaryButton, sectionRibbon, inventorySlot;
        public TMP_FontAsset font;
        public CharacterBlueprint[] characters;
        public RelicBlueprint[] relics;
        public MerchantItemBlueprint[] items;
        public AugmentInfo[] augments;
        public SyringeSpecialAugmentAbility[] weapons;
        public Sprite basicNeedle;
        private void OnEnable() { PhoenixNobleTheme.RefreshCatalog(this); }
        [System.Serializable] public class AugmentInfo
        {
            public string title, description;
            public Sprite icon;
            public Ability.AugmentTier tier;
        }
    }
}
