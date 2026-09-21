using System.Collections.Generic;
using UnityEngine;
using ParentType = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;
namespace Vampire
{
    public partial class SyringeDartAbility
    {
        public Ver4AugmentRuntime Ver4 { get; set; }
        private readonly HashSet<ParentType> ver4Specials = new HashSet<ParentType>();
        public void EnableVer4Special(ParentType type)
        {
            if((int)type>=16 && (int)type<=20) ver4Specials.Add(type);
        }
        public bool HasVer4Special(ParentType type) => ver4Specials.Contains(type);
        private int OriginalCount(ParentType type,int option) => Ver4 != null ? Ver4.Progress.Count(type.ToString(),option) : 0;
        public int Ver4FormationAdditionalCount => OriginalCount(ParentType.AcupunctureFormation,0)*2;
    }
}
