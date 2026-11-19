using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public static class StartingNeedleSelection
    {
        static readonly Dictionary<string, int> demoSelections = new Dictionary<string, int>();
        static readonly Dictionary<SyringeSpecialAugmentAbility.SpecialAugmentType, bool> demoEnabled = new Dictionary<SyringeSpecialAugmentAbility.SpecialAugmentType, bool>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetDemoSelections() { demoSelections.Clear(); demoEnabled.Clear(); }
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
        public static bool Available(SyringeSpecialAugmentAbility.SpecialAugmentType type) =>
            type != SyringeSpecialAugmentAbility.SpecialAugmentType.Honey && type != SyringeSpecialAugmentAbility.SpecialAugmentType.WoodNeedle;
        public static int SelectedId(CharacterBlueprint character)
        {
            if (GStarDemo.Enabled)
                return demoSelections.TryGetValue(SaveKey(character), out int selected) ? selected : DefaultId(character);
            int id=ProgressPrefs.GetInt(SaveKey(character),DefaultId(character));
            return Available((SyringeSpecialAugmentAbility.SpecialAugmentType)id) ? id : DefaultId(character);
        }
        public static string EnabledKey(SyringeSpecialAugmentAbility.SpecialAugmentType type)=>"October.NeedleEnabled."+type;
        public static bool Enabled(SyringeSpecialAugmentAbility weapon)=>weapon==null || (Available(weapon.Type) &&
            (GStarDemo.Enabled && demoEnabled.TryGetValue(weapon.Type, out bool enabled) ? enabled : ProgressPrefs.GetInt(EnabledKey(weapon.Type),1)!=0));
        public static bool SetEnabled(SyringeSpecialAugmentAbility weapon,bool enabled,ApothecaryUIConfig config)
        {
            if(weapon==null || !Available(weapon.Type) || !Owned(weapon))return false;
            if(!enabled && config.weapons.Count(w=>w!=null && Owned(w) && Enabled(w))<=1)return false;
            if (GStarDemo.Enabled) { demoEnabled[weapon.Type] = enabled; return true; }
            ProgressPrefs.SetInt(EnabledKey(weapon.Type),enabled?1:0);ProgressPrefs.Save();return true;
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
            if (GStarDemo.Enabled) return Available(weapon.Type);
            if(LobbyUnlockSave.IsUnlocked("Needle",weapon.Type.ToString(),false))return true;
            var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            return config!=null&&config.characters.Any(c=>c!=null&&DefaultId(c)==(int)weapon.Type&&LobbyUnlockSave.IsUnlocked("Character",c.name,c.owned));
        }
        public static int Price(SyringeSpecialAugmentAbility weapon)=>weapon==null?0:120+(int)weapon.Type/4*30;
        public static void Set(SyringeSpecialAugmentAbility weapon,CharacterBlueprint character=null)
        {
            if(!Owned(weapon)||!Enabled(weapon))return;
            if (GStarDemo.Enabled) { demoSelections[SaveKey(character)] = weapon==null?-1:(int)weapon.Type; return; }
            ProgressPrefs.SetInt(SaveKey(character),weapon==null?-1:(int)weapon.Type);ProgressPrefs.Save();
        }
        public static void Apply(AbilityManager manager,CharacterBlueprint character=null)
        {
            if(manager==null)return;
            var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            character = character != null ? character : CrossSceneData.CharacterBlueprint;
            var selected = config != null ? Selected(config,character) : null;
            var parents = manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true);
            var chosen = selected != null ? parents.FirstOrDefault(w=>w.Type==selected.Type) : null;
            if (chosen != null && !chosen.Owned) manager.AcquireVer4Ability(chosen);

            // Scene restore rebuilds parents and their exact saved acquisition ledger.
            if (!GStarDemo.Enabled || character == null || CrossSceneData.HasPendingRunSceneTransfer) return;
            var signature = parents.FirstOrDefault(w=>(int)w.Type==DefaultId(character));
            if (signature != null && (signature.Owned || manager.AcquireVer4Ability(signature, true)))
                manager.Ver4.SeedSignatureOriginals(signature);
        }
    }
}
