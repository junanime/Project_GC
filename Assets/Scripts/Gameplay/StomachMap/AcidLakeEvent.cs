using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vampire
{
    // One clock and one world-space waterline own presentation, spawning and immersion.
    [DefaultExecutionOrder(800)]
    public sealed class AcidLakeEvent : MonoBehaviour
    {
        public const float WarningTime = 910, FloodTime = 930, FullTime = 1140;
        public const float FinalHeightUV = .68f, ErosionLimit = 4f, HealthFractionPerSecond = .25f;
        public static AcidLakeEvent Instance { get; private set; }
        public float EventTime { get; private set; }
        public float ErosionSeconds { get; private set; }
        public float SurfaceY => map.WorldRect.yMin + map.WorldRect.height * Mathf.Lerp(.095f, FinalHeightUV, ProgressAt(EventTime));
        public bool Raining => Active && EventTime >= WarningTime;
        public bool Flooding => Active && EventTime >= FloodTime;
        public bool Active => map != null && map.StageOne;
        public float VisualTime { get; private set; }
        public FiniteStomachMap Map => map;
        public Renderer SurfaceRenderer => visual != null ? visual.SurfaceRenderer : null;
        LevelManager level;
        FiniteStomachMap map;
        AcidLakeVisual visual;
        bool warned;
        float debugOffset, sweepDelay;
        readonly List<Monster> monsters = new List<Monster>();

        public static void Install(LevelManager owner)
        {
            if (FiniteStomachMap.Instance == null) return;
            var lake = owner.GetComponent<AcidLakeEvent>() ?? owner.gameObject.AddComponent<AcidLakeEvent>();
            lake.level = owner; lake.map = FiniteStomachMap.Instance; Instance = lake;
            lake.visual = AcidLakeVisual.Create(lake);
        }
        public static float ProgressAt(float seconds) => Mathf.Clamp01((seconds - FloodTime) / (FullTime - FloodTime));
        public static bool Contains(Vector2 point) => Instance != null && Instance.IsSubmerged(point);
        public bool IsSubmerged(Vector2 point) => Flooding && map.WorldRect.Contains(point) &&
            point.y <= SurfaceY && map.DistanceToRim(point, out _) >= 0;

        // Does not advance the boss, monster, capsule or other run schedules.
        public void StartDebugFlood()
        {
            RunScore.Exclude("위산 이벤트 테스트");
            if (!Active || level.IsLevelEnded || level.IsRunFlowPaused || MiniStageRuntimeState.IsInsideMiniStage) return;
            debugOffset = Mathf.Max(debugOffset, FloodTime - level.CurrentLevelTime);
            EventTime = Mathf.Max(EventTime, FloodTime);
            ShowWarning();
        }
        void ShowWarning()
        {
            warned = true;
            var toast = FindObjectOfType<StageEventToastUI>();
            if (toast == null) toast = gameObject.AddComponent<StageEventToastUI>();
            toast.ShowAcidEmergency();
        }
        void Update()
        {
            if (!Active) { if (visual != null) visual.SetVisible(false); return; }
            if (GameInput.DebugPressed(KeyCode.Equals) && Time.timeScale > 0) StartDebugFlood();
            bool paused = level.IsRunFlowPaused || MiniStageRuntimeState.IsInsideMiniStage;
            float dt = level.IsLevelEnded ? Time.unscaledDeltaTime : paused ? 0 : Time.deltaTime;
            if (level.IsLevelEnded) EventTime += dt;
            else if (!paused) EventTime = Mathf.Max(EventTime, level.CurrentLevelTime + debugOffset);
            float visualDt=paused&&level.GetComponent<StageProgression>()?.Travelling==true?Time.deltaTime:dt;
            VisualTime += visualDt;
            if (Raining && !warned && !level.IsLevelEnded) ShowWarning();
            visual.SetVisible(map.FieldVisible && Raining);
            visual.Tick(visualDt);
            if (!Flooding || paused || dt <= 0) return;
            RemoveSubmergedMonsters();
            sweepDelay -= dt;
            if (sweepDelay <= 0) { sweepDelay = .15f; RemoveSubmergedObjects(); }
            if (level.IsLevelEnded) return;
            var player = level.PlayerCharacter;
            var progression=level.GetComponent<StageProgression>();
            if(progression!=null&&progression.CapsuleBoarded&&progression.Travelling){ErosionSeconds=0;return;}
            if (player == null || !player.IsAlive || player.IsPortalTravelling) return;
            if (!IsSubmerged(player.transform.position)) { ErosionSeconds = 0; return; }
            ErosionSeconds += dt;
            player.TakeAcidLakeDamage(dt, ErosionSeconds >= ErosionLimit);
        }

        public Vector2 NearestDryLand(Vector2 from, float margin = 1.5f, float inset = -1)
        {
            if (!Active) return from;
            if (inset < 0) inset = map.WallWidth + .6f;
            float minY = Flooding ? SurfaceY + margin : map.WorldRect.yMin;
            var vertical=new Vector2(from.x,Mathf.Max(from.y,minY));
            if(map.NavigationDistance(vertical,out _)>=inset)return vertical;
            Vector2 best = from; float bestDistance = float.MaxValue;
            // Boundary projection alone can bounce between a concave wall and the waterline.
            // Search horizontal slices of BOTH upper lobes, then refine the nearest valid point.
            for (float y = minY; y <= map.WorldRect.yMax; y += 1f)
            {
                if(y>from.y && (y-from.y)*(y-from.y)>bestDistance)break;
                for (float x = map.WorldRect.xMin; x <= map.WorldRect.xMax; x += 1f)
                {
                    var p = new Vector2(x, y); float d = (p - from).sqrMagnitude;
                    if (d < bestDistance && map.NavigationDistance(p, out _) >= inset)
                    { best = p; bestDistance = d; }
                }
            }
            for (float step = .5f; step >= .03125f; step *= .5f)
                for (int pass = 0; pass < 4; pass++)
                    for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
                    {
                        var p = best + new Vector2(x, y) * step;
                        float d = (p - from).sqrMagnitude;
                        if (p.y >= minY && d < bestDistance && map.NavigationDistance(p, out _) >= inset)
                        { best = p; bestDistance = d; }
                    }
            return best;
        }
        public static Vector2 DrySpawn(Vector2 point, float radius, bool miniStageOwned)
        {
            var lake = Instance;
            if (miniStageOwned || lake == null || !lake.Flooding) return point;
            float clearance = Mathf.Max(.8f, radius);
            if (point.y > lake.SurfaceY + clearance) return point;
            // Keep spawn count/timing intact. Move rejected positions to dry ground instead.
            return lake.NearestDryLand(point, clearance + .5f, lake.map.WallWidth + clearance);
        }
        public void RelocateRevivedPlayer(Character player)
        {
            if (!IsSubmerged(player.transform.position)) return;
            player.RelocateAfterAcidRevival(NearestDryLand(player.transform.position));
            ErosionSeconds = 0;
        }
        void RemoveSubmergedMonsters()
        {
            monsters.Clear();
            foreach (var m in level.EntityManager.LivingMonsters) monsters.Add(m);
            foreach (var m in monsters)
                if (m != null && m.gameObject.activeInHierarchy && !m.IsMiniStageOwned && !(m is BossMonster) &&
                    !(m.Blueprint is BossMonsterBlueprint) && IsSubmerged(m.transform.position)) m.DissolveInAcidLake();
        }
        void RemoveSubmergedObjects()
        {
            RemoveSubmergedMonsters();
            foreach (var chest in FindObjectsOfType<Chest>())
                if (IsSubmerged(chest.transform.position)) { chest.StopAllCoroutines(); PixelInteractionPrompt.Show(chest, false); level.EntityManager.DespawnChest(chest); }
            Remove<MerchantNPC>(); Remove<InteractableEventObject>(); Remove<BloodClotMiniStagePortal>();
            Remove<CurrencyJar>(); Remove<CurrencyJarDrop>(); Remove<SupplyRelicOrb>(); Remove<BossClearRewardPickup>();
            foreach (var pickup in FindObjectsOfType<Collectable>())
                if (IsSubmerged(pickup.transform.position)) pickup.DissolveInAcidLake();
        }
        void Remove<T>() where T : Component
        {
            foreach (var item in FindObjectsOfType<T>())
            {
                if (!item.gameObject.activeInHierarchy || !IsSubmerged(item.transform.position)) continue;
                if (item is AmphibiousCapsule) continue; // Acid-resistant rescue vehicle.
                if (item is StomachOpening) continue; // Anatomical terrain stays, interaction is already blocked by water.
                // Reserved portal travel is atomic; a dive already started may complete.
                if (item is StageExitPortal && level.PlayerCharacter.IsPortalTravelling) continue;
                if (item is BloodClotMiniStagePortal p && p.Reserved) continue;
                PixelInteractionPrompt.Show(item, false);
                item.gameObject.SetActive(false); Destroy(item.gameObject);
            }
        }
        void OnDestroy() { if (Instance == this) Instance = null; if (visual != null) Destroy(visual.gameObject); }
    }
}
