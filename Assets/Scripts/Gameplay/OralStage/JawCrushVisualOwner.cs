using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// Restore visibility before ordinary animation scripts run, then the proxy reacquires
    /// it after LateUpdate. Active-form/dash/death animation remains the visibility authority.
    /// Pool disable releases flags before the same actor can be assigned another blueprint.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-20000)]
    public sealed class JawCrushVisualOwner : MonoBehaviour
    {
        public int Generation { get; private set; }
        readonly List<JawCrushVisual> tracked = new List<JawCrushVisual>();
        void OnEnable(){Generation++;}
        void Update()
        {
            for(int i=tracked.Count-1;i>=0;i--)
                if(tracked[i]!=null)tracked[i].RestoreBeforeAnimation(); else tracked.RemoveAt(i);
        }
        internal void Track(JawCrushVisual visual){if(!tracked.Contains(visual))tracked.Add(visual);}
        internal void Untrack(JawCrushVisual visual){tracked.Remove(visual);}
        internal void ReleaseFinishedCorpse()
        {
            for(int i=tracked.Count-1;i>=0;i--)
                if(tracked[i]!=null && !tracked[i].IsPlaying)tracked[i].Cancel();
        }
        void OnDisable()
        {
            var copy=tracked.ToArray();tracked.Clear();
            foreach(var visual in copy)if(visual!=null)visual.OwnerDisabled();
        }
    }
}
