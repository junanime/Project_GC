using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ParentType = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;

namespace Vampire
{
    public sealed class Ver4AugmentRuntime : Ability, IRunSceneConditionalAugmentState
    {
        [Serializable] private sealed class Acquisition
        {
            public int version=4;
            public string parent;
            public bool original;
            public int option;
            public int grade;
            public float amount;
        }
        public Ver4AugmentBalance Balance { get; private set; }
        public OriginalAugmentProgress Progress { get; private set; } = new OriginalAugmentProgress();
        public Ver4CombatSnapshot Combat { get; private set; }
        private SyringeSpecialAugmentAbility[] parents;
        private readonly List<string> acquisitions = new List<string>();
        private SyringeDartAbility syringe;
        public override bool RequirementsMet() => false;

        public void Configure(Ver4AugmentBalance balance, AbilityManager manager, EntityManager entities, Character character)
        {
            base.Init(manager,entities,character);
            Balance=balance;
            syringe=manager.GetComponentInChildren<SyringeDartAbility>(true);
            parents=manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true);
            owned=true; level=1; maxLevel=int.MaxValue; canAppearAsOwnedUpgrade=false;
            syringe.Ver4=this;
            RefreshProgress();
            Monster.Died += OnMonsterDied;
        }

        private void OnDestroy() => Monster.Died -= OnMonsterDied;
        private void OnMonsterDied(Monster monster)
        {
            if (!(monster is MiniBossMonster) || !monster.WasKilledByPlayer || UnityEngine.Random.value >= Balance.miniBossLegendaryChestChance) return;
            SpawnLegendaryReward(monster.transform.position);
        }
        public void SpawnLegendaryReward(Vector2 position)
        {
            if (Balance.legendaryChest != null && playerCharacter != null && playerCharacter.CurrentHealth > 0)
                entityManager.SpawnChest(Balance.legendaryChest,position);
        }

        private void RefreshProgress()
        {
            foreach(var parent in parents)
                if(parent.Owned) Progress.RegisterOwnedParent(parent.Type.ToString());
            Combat=new Ver4CombatSnapshot(Progress);
        }

        public List<Ability> CreateOffers(bool legendaryOnly, int requestedCount)
        {
            RefreshProgress();
            var result=new List<Ability>();
            if(legendaryOnly)
            {
                var candidates=abilityManager.GetComponentsInChildren<SyringeLegendaryAugmentAbility>(true)
                    .Where(a=>!a.Owned && a.RequirementsMet()).Cast<Ability>().ToList();
                for(int slot=0;slot<Mathf.Min(requestedCount,Balance.legendaryChoiceCount) && candidates.Count>0;slot++)
                {
                    int index=UnityEngine.Random.Range(0,candidates.Count); var source=candidates[index]; candidates.RemoveAt(index);
                    result.Add(Offer(Ver4RewardKind.LegendaryAbility,AugmentUpgradeGrade.Legendary,source,0,0,
                        "전설 증강: "+source.Name,source.Description+"\n전용 상자 획득 · 강화 불가"));
                }
                return result;
            }

            var newParents=parents.Where(p=>!p.Owned && p.RequirementsMet()).ToList();
            var ownedParents=parents.Where(p=>p.Owned).ToList();
            var originals=new List<(SyringeSpecialAugmentAbility parent,int option)>();
            foreach(var parent in ownedParents)
                for(int option=0;option<3;option++)
                    if(Progress.CanSelect(parent.Type.ToString(),option)) originals.Add((parent,option));

            for(int slot=0;slot<requestedCount;slot++)
            {
                // Acquisition is a reward type decision, never a seventh grade.
                // With no parent yet, expose three distinct acquisition choices.
                if(newParents.Count>0 && (ownedParents.Count==0 || UnityEngine.Random.value<Balance.newSpecialChance))
                {
                    int index=UnityEngine.Random.Range(0,newParents.Count); var parent=newParents[index]; newParents.RemoveAt(index);
                    result.Add(Offer(Ver4RewardKind.NewSpecial,AugmentUpgradeGrade.Common,parent,0,0,
                        "특수 증강: "+ParentName(parent),parent.Description+"\n최초 획득 · 오리지널 Lv.0/9"));
                    continue;
                }
                if(ownedParents.Count==0) break;
                // Random.value includes 1; TryRoll deliberately uses [0,1).
                double sample=Math.Min(UnityEngine.Random.value,.999999999);
                if(!Balance.odds.TryRoll(sample,g=>g!=AugmentUpgradeGrade.Original || originals.Count>0,out var grade)) break;
                if(grade==AugmentUpgradeGrade.Original)
                {
                    var candidate=originals[UnityEngine.Random.Range(0,originals.Count)];
                    int index=(int)candidate.parent.Type*3+candidate.option;
                    int count=Progress.Count(candidate.parent.Type.ToString(),candidate.option);
                    int originalLevel=Progress.Level(candidate.parent.Type.ToString());
                    result.Add(Offer(Ver4RewardKind.Original,grade,candidate.parent,candidate.option,0,
                        ParentName(candidate.parent)+" · 오리지널 강화",
                        Ver4AugmentCatalog.OriginalNames[index]+"\n"+Ver4AugmentCatalog.OriginalDescriptions[index]+
                        $"\n이 항목 {count} → {count+1}/3\n오리지널 Lv.{originalLevel} → {originalLevel+1}/9"));
                }
                else
                {
                    var parent=ownedParents[UnityEngine.Random.Range(0,ownedParents.Count)];
                    grade=Progress.ResolveNumericGrade(parent.Type.ToString(),grade);
                    var options=Enumerable.Range(0,9).Where(i=>NumericEligible(i)).ToList();
                    if(options.Count==0) break;
                    int option=options[UnityEngine.Random.Range(0,options.Count)];
                    float amount=Balance.NumericValue(grade,option);
                    if(option==1) amount=Mathf.Min(amount,Mathf.Max(0,1f-playerCharacter.CritChance));
                    result.Add(Offer(Ver4RewardKind.Numeric,grade,parent,option,amount,ParentName(parent)+" · 수치 강화",
                        "강화 등급: "+AugmentUpgradeOdds.DisplayName(grade)+"\n"+Ver4AugmentCatalog.NumericDescription(option,amount)+
                        (Progress.Level(parent.Type.ToString())==9 ? "\n지존 승급 완료" : "")));
                }
            }
            return result;
        }

        private bool NumericEligible(int option) => option!=1 || playerCharacter.CritChance<1f;
        private static string ParentName(SyringeSpecialAugmentAbility parent) => Ver4AugmentCatalog.ParentNames[(int)parent.Type];
        private Ver4AugmentOffer Offer(Ver4RewardKind kind,AugmentUpgradeGrade grade,Ability source,int option,float amount,string title,string text)
        {
            var obj=new GameObject("Ver4 Preview"); obj.transform.SetParent(transform,false);
            var offer=obj.AddComponent<Ver4AugmentOffer>();
            offer.Configure(this,kind,grade,source,option,amount,title,text);
            return offer;
        }

        public bool IsValid(Ver4AugmentOffer offer)
        {
            if(offer.Source==null) return false;
            switch(offer.Kind)
            {
                case Ver4RewardKind.NewSpecial: return !offer.Source.Owned && offer.Source.RequirementsMet();
                case Ver4RewardKind.LegendaryAbility: return offer.Source is SyringeLegendaryAugmentAbility && !offer.Source.Owned && offer.Source.RequirementsMet();
                case Ver4RewardKind.Original: return offer.Parent!=null && offer.Parent.Owned && Progress.CanSelect(offer.Parent.Type.ToString(),offer.Option);
                case Ver4RewardKind.Numeric: return offer.Parent!=null && offer.Parent.Owned && NumericEligible(offer.Option) && AugmentUpgradeOdds.IsNumeric(offer.Grade);
                default:return false;
            }
        }

        public bool Apply(Ver4AugmentOffer offer)
        {
            if(!IsValid(offer)) return false;
            if(offer.Kind==Ver4RewardKind.NewSpecial || offer.Kind==Ver4RewardKind.LegendaryAbility)
            {
                if(!abilityManager.AcquireVer4Ability(offer.Source)) return false;
                RefreshProgress(); return true;
            }
            var acquisition=new Acquisition { parent=offer.Parent.Type.ToString(), original=offer.Kind==Ver4RewardKind.Original,
                option=offer.Option, grade=(int)offer.Grade, amount=offer.Amount };
            ApplyAcquisition(acquisition);
            acquisitions.Add(JsonUtility.ToJson(acquisition));
            level=1+acquisitions.Count;
            RefreshProgress();
            return true;
        }

        private void ApplyAcquisition(Acquisition acquisition)
        {
            if(acquisition.original)
            {
                Progress.TrySelect(acquisition.parent,acquisition.option);
                if(acquisition.parent==nameof(ParentType.Mosquito))
                {
                    if(acquisition.option==1) PlayerGeneralStatRuntime.GetOrCreate(playerCharacter).AddDamageReduction(.05f);
                    if(acquisition.option==2) playerCharacter.AddMaxHealthBonus(20f);
                }
                return;
            }
            float value=acquisition.amount;
            var stats=PlayerGeneralStatRuntime.GetOrCreate(playerCharacter);
            switch(acquisition.option)
            {
                case 0:playerCharacter.AddDamageMultiplier(value);break;
                case 1:playerCharacter.AddCritChance(value);break;
                case 2:stats.AddCritDamageMultiplier(value);break;
                case 3:playerCharacter.AddAttackSpeed(value);break;
                case 4:playerCharacter.AddProjectileSpeed(value);break;
                case 5:stats.AddKnockbackMultiplier(value);break;
                case 6:playerCharacter.AddRangeBoost(value);break;
                case 7:playerCharacter.AddProjectileSize(value);break;
                case 8:playerCharacter.AddProjectileCount(Mathf.RoundToInt(value));break;
            }
        }

        public List<string> CaptureRunSceneConditionalAugmentIds() => new List<string>(acquisitions);
        public bool RestoreRunSceneConditionalAugments(IReadOnlyList<string> ids)
        {
            if(ids==null) return false;
            // Repeated restore on the same live run must never double-apply stats.
            if(acquisitions.Count>0) return acquisitions.SequenceEqual(ids);
            RefreshProgress();
            var validated=new List<Acquisition>();
            var validationProgress=new OriginalAugmentProgress();
            foreach(var parent in parents.Where(p=>p.Owned)) validationProgress.RegisterOwnedParent(parent.Type.ToString());
            try
            {
                foreach(string json in ids)
                {
                    var entry=JsonUtility.FromJson<Acquisition>(json);
                    if(entry==null || entry.version!=4 || !validationProgress.IsOwned(entry.parent)) return false;
                    if(entry.original)
                    {
                        if(entry.grade!=(int)AugmentUpgradeGrade.Original || entry.option<0 || entry.option>=3 || !validationProgress.TrySelect(entry.parent,entry.option)) return false;
                    }
                    else if(entry.option<0 || entry.option>8 || !AugmentUpgradeOdds.IsNumeric((AugmentUpgradeGrade)entry.grade)
                        || float.IsNaN(entry.amount) || float.IsInfinity(entry.amount) || entry.amount<=0) return false;
                    validated.Add(entry);
                }
            }
            catch(ArgumentException) { return false; }
            foreach(var entry in validated) ApplyAcquisition(entry);
            acquisitions.AddRange(ids); level=1+acquisitions.Count; RefreshProgress();
            return true;
        }
    }
}
