using System.Collections;
using UnityEngine;

namespace Vampire
{
    // Owns the roll, not the interactable: disabling a part must never reroll a stage.
    public sealed class BuriedBossDirector : MonoBehaviour
    {
        public FoodBossKind SelectedKind { get; private set; }
        public int PreparedStage { get; private set; }
        public BuriedBossPart Part { get; private set; }
        public Vector2 EntryPosition { get; private set; }
        public bool Consumed { get; private set; }
        public static BuriedBossDirector Pending { get; private set; }
        LevelManager level;
        BuriedBossEmergence emergence;
        float nextAutomaticAttempt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Pending = null; }

        public static BuriedBossDirector Ensure(LevelManager owner)
        {
            var director = owner.GetComponent<BuriedBossDirector>();
            if (director == null) director = owner.gameObject.AddComponent<BuriedBossDirector>();
            director.level = owner;
            return director;
        }

        // Called while the loading overlay is still up, after the normal object layout.
        public void PrepareStage(Vector2 start)
        {
            int stage = StageProgression.Ensure(level).StageNumber;
            if (PreparedStage == stage) return;
            StopAllCoroutines();
            Cleanup();
            if (Part != null) { Part.gameObject.SetActive(false); Destroy(Part.gameObject); }
            Part = null;
            if(stage!=1)
            {
                // Stage two has six permanent homes; its final boss is separately reserved.
                Consumed=true;PreparedStage=stage;EntryPosition=start;
                return;
            }
            Consumed = false;
            EntryPosition = start;
            SelectedKind = (FoodBossKind)Random.Range(0, 5);
            if (!TryPosition(start, out var point))
                throw new System.InvalidOperationException("No reachable boss-part position in the 6–8 unit entry ring.");
            PreparedStage = stage;
            nextAutomaticAttempt = 0;
            Part = BuriedBossPart.Create(this, SelectedKind, point);
        }

        public static bool TryPosition(Vector2 start, out Vector2 point)
        {
            var map = FiniteStomachMap.Instance;
            var layout = FindObjectOfType<FieldObjectLayout>();
            float offset = Random.value * 360;
            for (int i = 0; i < 720; i++)
            {
                float radius = Mathf.Sqrt(Mathf.Lerp(36, 64, Random.value));
                point = start + FoodBossRuntime.Dir(offset + i * 137.50776f) * radius;
                if (map != null && map.NavigationDistance(point, out _, true) < map.WallWidth + 2.5f) continue;
                if (AcidLakeEvent.Contains(point)) continue;
                // The ring must also be reachable from the entry, not across a colon wall.
                bool blocked = false;
                if (map != null)
                    for (int step = 1; step <= 32; step++)
                        if (map.NavigationDistance(Vector2.Lerp(start, point, step / 32f), out _, true) < map.ClimbLimit + .35f)
                        { blocked = true; break; }
                if (blocked) continue;
                if(layout!=null)
                    foreach(var slot in layout.Slots)
                        if(Vector2.Distance(point,slot.position)<2.8f){blocked=true;break;}
                if(blocked)continue;
                foreach (var collider in Physics2D.OverlapCircleAll(point, 2.1f))
                {
                    if (collider.GetComponentInParent<Character>() != null || collider.GetComponentInParent<Monster>() != null) continue;
                    if (!collider.isTrigger || collider.GetComponentInParent<InteractableEventObject>() != null || collider.GetComponentInParent<Chest>() != null)
                    { blocked = true; break; }
                }
                if (!blocked) return true;
            }
            point = start;
            return false;
        }

        public bool CanSummon => isActiveAndEnabled && !Consumed && PreparedStage > 0 && level != null &&
            StageProgression.Ensure(level).StageNumber==1 &&
            !level.IsLevelEnded && !level.IsRunFlowPaused && !MiniStageRuntimeState.IsInsideMiniStage &&
            !StageProgression.Ensure(level).FirstBossDefeated && !FinalBossSummonInteractable.IsSummoning &&
            FindObjectOfType<FoodBossRuntime>() == null && FindObjectOfType<BossMonster>() == null &&
            FindObjectOfType<BossController>() == null && FindObjectOfType<SnailBossRuntime>() == null;

        public bool TrySummon(BuriedBossPart source, Character player)
        {
            if (source == null || source != Part || player == null || !source.isActiveAndEnabled ||
                Time.timeScale <= 0 || !CanSummon || AcidLakeEvent.Contains(source.transform.position)) return false;
            Pending = this;
            StartCoroutine(Arrive(source, player));
            return true;
        }

        public bool TryAutomaticSummon()
        {
            if (!CanSummon || Time.time < nextAutomaticAttempt) return false;
            nextAutomaticAttempt = Time.time + 1;
            if (Part != null && Part.isActiveAndEnabled && !AcidLakeEvent.Contains(Part.transform.position))
                return Part.Activate(level.PlayerCharacter);
            // Acid may erase both interactables. At scheduled expiry, surface the SAME boss on
            // reachable dry ground instead of silently losing the stage's only completion path.
            if(!TryPosition(level.PlayerCharacter.transform.position,out var point))return false;
            if(Part!=null){Part.gameObject.SetActive(false);Destroy(Part.gameObject);}
            Part=BuriedBossPart.Create(this,SelectedKind,point);
            return Part.Activate(level.PlayerCharacter);
        }

        IEnumerator Arrive(BuriedBossPart source, Character player)
        {
            bool success = false;
            var blueprint = level.CurrentLevelBlueprint;
            try
            {
                emergence = source.Presentation;
                GameAudioManager.PlayBossAppearOnly();
                yield return emergence.Play(player);
                if (level == null || level.IsLevelEnded || level.CurrentLevelBlueprint != blueprint || source == null) yield break;
                var spawned = level.EntityManager.SpawnFinalBoss(blueprint, source.transform.position, 0, SelectedKind);
                if (spawned == null) yield break;
                success = true;
                NotifySpawned();
                level.NotifyExternalFinalBossSpawned();
                GameAudioManager.StartBossAudio();
            }
            finally
            {
                Cleanup();
                if (!success && source != null) source.Restore();
            }
        }

        public void NotifySpawned()
        {
            Consumed = true;
            if (Part != null) { Part.gameObject.SetActive(false); Destroy(Part.gameObject); Part = null; }
        }
        void Cleanup()
        {
            if (Pending == this) Pending = null;
            if (emergence != null) emergence.ResetStandby();
            emergence = null;
        }
        void Update()
        {
            if (Pending != this) return;
            if (Part == null || !Part.isActiveAndEnabled || level.IsLevelEnded || MiniStageRuntimeState.IsInsideMiniStage)
            { StopAllCoroutines(); Cleanup(); if (Part != null) Part.Restore(); }
        }
        void OnDisable() { StopAllCoroutines(); Cleanup(); }
        void OnDestroy() { if (Part != null) Destroy(Part.gameObject); }
    }
}
