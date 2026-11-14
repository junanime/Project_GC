using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vampire
{
    // A run's launch loadout is immutable. Persistent saves and score are never reset here.
    [DisallowMultipleComponent]
    public sealed class StageGrowthReset : MonoBehaviour
    {
        LevelManager level;
        AbilityManager abilities;
        RunSceneCharacterSnapshot launchCharacter;
        List<RunSceneAbilitySnapshot> launchAbilities;
        string launchGeneralStats;
        public int CurrentStage { get; private set; } = 1;
        public int ResetCount { get; private set; }
        public bool Resetting { get; private set; }

        public static StageGrowthReset CaptureLaunch(LevelManager owner, AbilityManager manager)
        {
            var reset=owner.GetComponent<StageGrowthReset>()??owner.gameObject.AddComponent<StageGrowthReset>();
            if(reset.launchCharacter!=null)return reset;
            reset.level=owner;reset.abilities=manager;
            reset.launchCharacter=owner.PlayerCharacter.CaptureRunSceneState();
            reset.launchAbilities=manager.CaptureRunSceneAbilities();
            reset.launchGeneralStats=JsonUtility.ToJson(PlayerGeneralStatRuntime.GetOrCreate(owner.PlayerCharacter));
            return reset;
        }

        // Direct entry loads the shared scene internally; it is still the first field of this run.
        public void SetInitialStage(int stage)
        {
            if(ResetCount!=0||Resetting)throw new System.InvalidOperationException("Cannot replace a run's initial stage after travelling.");
            CurrentStage=stage;
        }

        public IEnumerator ResetForTransition(int targetStage, LevelBlueprint next, StageEntryLoading loading=null)
        {
            if(targetStage<0||targetStage>2)throw new System.ArgumentOutOfRangeException(nameof(targetStage));
            if(targetStage==CurrentStage||MiniStageRuntimeState.IsInsideMiniStage||Resetting)yield break;
            if(launchCharacter==null||level==null||abilities==null||next==null)
                throw new System.InvalidOperationException("The original departure loadout must be captured before a field transfer.");
            Resetting=true;
            var player=level.PlayerCharacter;
            float previousTimeScale=Time.timeScale;
            Time.timeScale=0;
            try
            {
                player.PrepareStageGrowthReset();
                HungerNeedleRuntime.Clear(player);
                PoisonContagionRuntime.Disable();
                player.GetComponent<OctoberItemRuntime>()?.ResetForStage();
                // Dynamic reward chests are not all owned by FieldObjectLayout.
                foreach(var chest in FindObjectsOfType<Chest>())level.EntityManager.DespawnChest(chest);
                foreach(var pickup in FindObjectsOfType<Collectable>())pickup.DissolveInAcidLake();
                foreach(var orb in FindObjectsOfType<SupplyRelicOrb>()){orb.gameObject.SetActive(false);Destroy(orb.gameObject);}
                foreach(var capsule in FindObjectsOfType<SupplyCapsule>()){capsule.gameObject.SetActive(false);Destroy(capsule.gameObject);}
                foreach(var projectile in FindObjectsOfType<Projectile>())projectile.ReleaseForPlayerExit(player);
                foreach(var projectile in OctoberEnemyProjectile.Active.ToArray())if(projectile!=null)projectile.Remove();
                RemovePlayerEffect<LegendaryConditionalSpecialRuntime>(player);
                RemovePlayerEffect<GastricPeristalsisWaveController>(player);
                RemovePlayerEffect<MucosalFortressShieldController>(player);
                RemovePlayerEffect<HungrySpiritController>(player);
                RemovePlayerEffect<SnailSlowStatus>(player);
                RemoveWorldEffects<AcupunctureFormationController>();
                RemoveWorldEffects<CursorControlledNeedleController>();
                RemoveWorldEffects<HedgehogNeedleController>();
                RemoveWorldEffects<SyringeCloneController>();
                RemoveWorldEffects<OrganCompressionField>();
                // These attacks outlive their weapon/projectile as independent world objects.
                RemoveWorldEffects<Ver4GroundEffect>();
                RemoveWorldEffects<DigestiveAcidPuddle>();
                RemoveWorldEffects<FiberTrailSegment>();
                // Rebuild offers and weapon controllers without consulting changed lobby preferences.
                yield return abilities.ResetForStage(next,level.EntityManager,player,launchAbilities);
                JsonUtility.FromJsonOverwrite(launchGeneralStats,PlayerGeneralStatRuntime.GetOrCreate(player));
                player.ResetStageGrowth(JsonUtility.FromJson<RunSceneCharacterSnapshot>(JsonUtility.ToJson(launchCharacter)));
                PlayerGeneralStatRuntime.GetOrCreate(player).SyncPickupCollider();
                SynergyManager.Instance?.RestoreOwnership(new MerchantOwnershipSnapshot());
                ApothecaryUI.Instance?.ResetStageItems();
                level.PlayerInventory?.ResetForStage();
                StageEventRuntimeModifiers.ResetCoinModifiers();
                DigestiveEnzymeDifficultyManager.Instance?.ResetForStage();
                CurrentStage=targetStage;ResetCount++;
                if(loading!=null)yield return loading.PlayEnergyAbsorption();
            }
            finally { Resetting=false;Time.timeScale=previousTimeScale; }
        }

        static void RemovePlayerEffect<T>(Character player) where T:MonoBehaviour
        {
            foreach(var effect in player.GetComponents<T>())
            {effect.enabled=false;effect.StopAllCoroutines();Destroy(effect);}
        }
        static void RemoveWorldEffects<T>() where T:MonoBehaviour
        {
            foreach(var effect in FindObjectsOfType<T>())
            {effect.gameObject.SetActive(false);Destroy(effect.gameObject);}
        }
    }
}
