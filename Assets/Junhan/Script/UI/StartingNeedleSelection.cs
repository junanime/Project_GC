using System.Linq;
using UnityEngine;

namespace Vampire
{
    public static class StartingNeedleSelection
    {
        public static string SaveKey(CharacterBlueprint character)=>"October.StartingNeedle."+OctoberArt.CharacterKey(character);
        public static int DefaultId(CharacterBlueprint character)
        {
            switch(OctoberArt.CharacterKey(character))
            {
                case "Hyuki":return (int)SyringeSpecialAugmentAbility.SpecialAugmentType.IceNeedle;
                case "Shini":return (int)SyringeSpecialAugmentAbility.SpecialAugmentType.FireNeedle;
                case "Ari":return (int)SyringeSpecialAugmentAbility.SpecialAugmentType.AcupunctureFormation;
                default:return (int)SyringeSpecialAugmentAbility.SpecialAugmentType.WindNeedle;
            }
        }
        public static int Id=>SelectedId(CrossSceneData.CharacterBlueprint);
        public static int SelectedId(CharacterBlueprint character)=>PlayerPrefs.GetInt(SaveKey(character),DefaultId(character));
        public static string EnabledKey(SyringeSpecialAugmentAbility.SpecialAugmentType type)=>"October.NeedleEnabled."+type;
        public static bool Enabled(SyringeSpecialAugmentAbility weapon)=>weapon==null || PlayerPrefs.GetInt(EnabledKey(weapon.Type),1)!=0;
        public static bool SetEnabled(SyringeSpecialAugmentAbility weapon,bool enabled,ApothecaryUIConfig config)
        {
            if(weapon==null || !Owned(weapon))return false;
            if(!enabled && config.weapons.Count(w=>w!=null && Owned(w) && Enabled(w))<=1)return false;
            PlayerPrefs.SetInt(EnabledKey(weapon.Type),enabled?1:0);PlayerPrefs.Save();return true;
        }
        public static SyringeSpecialAugmentAbility Selected(ApothecaryUIConfig config,CharacterBlueprint character=null)
        {
            int id=SelectedId(character);
            var weapon=config.weapons?.FirstOrDefault(w=>(int)w.Type==id);
            if(weapon!=null&&!Owned(weapon))weapon=config.weapons?.FirstOrDefault(w=>(int)w.Type==DefaultId(character));
            if(weapon!=null&&!Enabled(weapon))return null;
            return weapon;
        }
        public static bool Owned(SyringeSpecialAugmentAbility weapon)
        {
            if(weapon==null)return true;
            if(LobbyUnlockSave.IsUnlocked("Needle",weapon.Type.ToString(),false))return true;
            var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            return config!=null&&config.characters.Any(c=>c!=null&&DefaultId(c)==(int)weapon.Type&&LobbyUnlockSave.IsUnlocked("Character",c.name,c.owned));
        }
        public static int Price(SyringeSpecialAugmentAbility weapon)=>weapon==null?0:120+(int)weapon.Type/4*30;
        public static void Set(SyringeSpecialAugmentAbility weapon,CharacterBlueprint character=null)
        {
            if(!Owned(weapon)||!Enabled(weapon))return;
            PlayerPrefs.SetInt(SaveKey(character),weapon==null?-1:(int)weapon.Type);PlayerPrefs.Save();
        }
        public static void Apply(AbilityManager manager,CharacterBlueprint character=null)
        {
            if(manager==null)return;
            var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            var selected=config!=null?Selected(config,character!=null?character:CrossSceneData.CharacterBlueprint):null;
            if(selected==null)return;
            var ability=manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true).FirstOrDefault(w=>w.Type==selected.Type);
            if(ability!=null&&Owned(ability)&&!ability.Owned)manager.AcquireVer4Ability(ability);
        }
    }
}
