using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    [DisallowMultipleComponent]
    public class StageEventDirector : RuntimeModuleHost
    {
        [SerializeField] private List<StageEventSchedule> basicEvents = new List<StageEventSchedule>();
        [SerializeField] private List<AdvancedStageFieldEventDirector> advancedEvents = new List<AdvancedStageFieldEventDirector>();
        [SerializeField] private List<AntacidBubbleSurgeEventController> antacidEvents = new List<AntacidBubbleSurgeEventController>();
        protected override IEnumerable<RuntimeModule> Modules
        {
            get
            {
                if (basicEvents != null) foreach (var module in basicEvents) yield return module;
                if (advancedEvents != null) foreach (var module in advancedEvents) yield return module;
                if (antacidEvents != null) foreach (var module in antacidEvents) yield return module;
            }
        }
    }
}
