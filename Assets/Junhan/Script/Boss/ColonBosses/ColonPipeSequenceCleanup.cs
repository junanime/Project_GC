using UnityEngine;

namespace Vampire
{
    // The house outlives its boss; interruption must not leave a half-closed door or pipe mask behind.
    public sealed class ColonPipeSequenceCleanup : MonoBehaviour
    {
        ColonBossRuntime owner;int slot;bool restored;
        public static ColonPipeSequenceCleanup Create(ColonBossRuntime boss)
        {
            var go=new GameObject("Pipe sequence · interruption cleanup");go.transform.SetParent(boss.Effects,false);
            var cleanup=go.AddComponent<ColonPipeSequenceCleanup>();cleanup.owner=boss;return cleanup;
        }
        public void AtDoor(int index)=>slot=index;
        public void Complete(){Restore(false);Destroy(gameObject);}
        void Restore(bool interrupted)
        {
            if(restored)return;restored=true;
            if(owner==null||owner.Habitat==null)return;
            owner.Habitat.RestoreDoors(owner.Visual);
            if(interrupted&&!owner.Dead){owner.PeekFromDoor(slot,false);owner.SetHidden(false);owner.Visual.ResetPose();}
        }
        void OnDisable()=>Restore(true);
        void OnDestroy()=>Restore(true);
    }
}
