using UnityEngine;
using ParentType = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;
namespace Vampire
{
    // Immutable at emission: already launched needles retain their original
    // acquisition state. A new snapshot is created only when progression changes.
    public sealed class Ver4CombatSnapshot
    {
        private readonly int[,] counts = new int[21,3];
        private readonly bool[] owned = new bool[21];
        public Ver4CombatSnapshot(OriginalAugmentProgress progress)
        {
            for(int parent=0;parent<21;parent++)
            {
                string id=((ParentType)parent).ToString();
                owned[parent]=progress.IsOwned(id);
                for(int option=0;option<3;option++) counts[parent,option]=progress.Count(id,option);
            }
        }
        public bool Has(ParentType parent) => owned[(int)parent];
        public int Count(ParentType parent,int option) => counts[(int)parent,option];
        public float Factor(ParentType parent,int option,float perSelection) => 1f+Count(parent,option)*perSelection;
        public void Modify(ref SyringeSpecialRuntime runtime)
        {
            runtime.poisonTickDamage*=Factor(ParentType.Poison,0,.12f);
            runtime.poisonDuration*=Factor(ParentType.Poison,1,.12f);
            runtime.poisonTickInterval=Mathf.Max(.08f,runtime.poisonTickInterval*(1f-.08f*Count(ParentType.Poison,2)));
            runtime.explosionRadius*=Factor(ParentType.Explosion,0,.15f);
            runtime.explosionDamage*=Factor(ParentType.Explosion,1,.15f);
            runtime.homingRange*=Factor(ParentType.Homing,0,.14f);
            runtime.homingLerpSpeed*=Factor(ParentType.Homing,1,.06f);
            if(runtime.pierceEnabled) runtime.pierceCount+=Count(ParentType.Pierce,0);
            runtime.honeyDuration*=Factor(ParentType.Honey,0,.12f);
            runtime.honeySlowMultiplier=Mathf.Max(.05f,runtime.honeySlowMultiplier-.05f*Count(ParentType.Honey,1));
            runtime.mosquitoHealPerHit*=Factor(ParentType.Mosquito,0,.05f);
            runtime.returnNeedleDamageMultiplier*=Factor(ParentType.ReturnNeedle,0,.20f);
            runtime.returnNeedleSpeedMultiplier*=Factor(ParentType.ReturnNeedle,1,.12f);
            runtime.fiberTrailLifetime*=Factor(ParentType.FiberNeedle,0,.18f);
            runtime.fiberTrailDamagePerSecond*=Factor(ParentType.FiberNeedle,1,.15f);
            runtime.fiberTrailWidth*=Factor(ParentType.FiberNeedle,2,.12f);
            runtime.corrosionDuration*=Factor(ParentType.CorrosionNeedle,0,.12f);
            runtime.corrosionDamageTakenBonusPerStack*=Factor(ParentType.CorrosionNeedle,1,.10f);
            runtime.corrosionBossDamageTakenBonusPerStack*=Factor(ParentType.CorrosionNeedle,1,.10f);
            runtime.corrosionMaxStacks+=Count(ParentType.CorrosionNeedle,2);
            runtime.pressureDamageBonusPerDistance*=Factor(ParentType.PressureNeedle,0,.12f);
            runtime.pressureMaxDamageBonus*=Factor(ParentType.PressureNeedle,1,.10f);
            runtime.markDuration*=Factor(ParentType.MarkNeedle,0,.12f);
            runtime.markBonusDamageMultiplier*=Factor(ParentType.MarkNeedle,1,.12f);
            runtime.digestiveAcidPuddleLifetime*=Factor(ParentType.DigestiveAcidSacNeedle,0,.12f);
            runtime.digestiveAcidPuddleRadius*=Factor(ParentType.DigestiveAcidSacNeedle,1,.12f);
            runtime.digestiveAcidPuddleDamagePerSecond*=Factor(ParentType.DigestiveAcidSacNeedle,2,.12f);
            runtime.hungerStackDuration*=Factor(ParentType.HungerNeedle,0,.12f);
            runtime.hungerAttackSpeedBonusPerStack*=Factor(ParentType.HungerNeedle,1,.10f);
            runtime.hungerMaxStacks+=Count(ParentType.HungerNeedle,2);
            runtime.gutBacteriaStackDuration*=Factor(ParentType.GutBacteriaNeedle,0,.12f);
        }
    }
}

