using System.Collections;
using UnityEngine;
namespace Vampire
{
    [DisallowMultipleComponent]
    public sealed class StageProgression : MonoBehaviour
    {
        public int StageNumber { get; private set; } = 1;
        public bool FirstBossDefeated { get; private set; }
        public bool Travelling { get; private set; }
        public float EntryHealth { get; private set; }
        public StageExitPortal Portal { get; private set; }
        LevelManager level;
        StageTwoDefinition definition;
        LevelBlueprint runtimeStage;
        float bossDps, defeatPower = 1;
        BloodClotTravel travel;
        public static StageProgression Ensure(LevelManager level)
        {
            var result = level.GetComponent<StageProgression>() ?? level.gameObject.AddComponent<StageProgression>();
            result.level = level; return result;
        }
        void Awake() { level = GetComponent<LevelManager>(); definition = Resources.Load<StageTwoDefinition>("StageTwoDefinition"); }
        public bool FinalBossDefeated(Vector3 position, float measuredDps)
        {
            if (StageNumber != 1) return false;
            if (FirstBossDefeated || level == null || level.IsLevelEnded) return true;
            if (definition == null || !definition.IsReady)
            { Debug.LogError("Stage 2 content missing; cannot open exit."); return true; }
            FirstBossDefeated = true; bossDps = measuredDps; defeatPower = Power(level.PlayerCharacter);
            level.NotifyExternalFinalBossSpawned();
            for (int i = 0; i < 3; i++)
            {
                float angle = (210 + i * 60) * Mathf.Deg2Rad;
                level.EntityManager.SpawnChest(definition.rewardChest,
                    (Vector2)position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 3.2f);
            }
            var go = new GameObject("녹아내린 롤케이크 · 스테이지 2 입구"); go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = definition.crater;
            GroundVisualSorting.ApplyBackground(renderer, -10);
            go.AddComponent<CircleCollider2D>().radius = 2.6f;
            Portal = go.AddComponent<StageExitPortal>(); Portal.Configure(this);
            go.AddComponent<MapMarker>().Configure(MapMarkerKind.Event, "스테이지 2 · 휘핑크림 지대");
            GameAudioManager.FinishStageBossAudio(); return true;
        }
        static float Power(Character p) => p == null ? 1 : Mathf.Max(.01f, p.DamageMultiplier * p.AttackSpeedMultiplier);
        public bool CanEnter(Character p) => FirstBossDefeated && StageNumber == 1 && !Travelling &&
            level != null && !level.IsLevelEnded && !level.IsRunFlowPaused && !MiniStageRuntimeState.IsInsideMiniStage &&
            BloodClotTravel.CanTravel(p) && Time.timeScale > 0;
        public bool TryEnter(Character player, StageExitPortal portal)
        {
            if (portal != Portal || !CanEnter(player) || definition == null || !definition.IsReady) return false;
            Travelling = true; StartCoroutine(Enter(player, portal)); return true;
        }
        IEnumerator Enter(Character player, StageExitPortal portal)
        {
            bool completed = false;
            travel = BloodClotTravel.Ensure(player);
            level.SetRunFlowPaused(true); level.EntityManager.SetFieldMonsterRuntimeSuspended(true);
            try
            {
                GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.MiniStageEnter);
                yield return travel.Dive(portal.transform);
                if (!player.IsAlive || level.IsLevelEnded || !travel.Busy) yield break;
                runtimeStage = StageTwoBalance.Build(level.CurrentLevelBlueprint, definition, bossDps,
                    Power(player) / defeatPower, out float hp);
                EntryHealth = hp; level.BeginNextStage(runtimeStage); StageNumber = 2; SnailFieldProgress.Reset();
                yield return travel.Eject(portal.transform, portal.transform.position + Vector3.left * 3.1f);
                completed = player.IsAlive && !level.IsLevelEnded;
                Debug.Log($"STAGE_2_ENTERED character={travel.CharacterKey} hpBase={EntryHealth:0.0} level={player.CurrentLevel}");
            }
            finally
            {
                if (!completed && travel != null) travel.Cancel();
                level.EntityManager.SetFieldMonsterRuntimeSuspended(false); level.SetRunFlowPaused(false);
                Travelling = false;
                if (StageNumber == 1 && portal != null) portal.AllowRetry();
                if (StageNumber == 2 && portal != null) Destroy(portal.gameObject);
            }
        }
        void OnDisable() { if (Travelling && travel != null) travel.Cancel(); }
        void OnDestroy()
        {
            if (runtimeStage == null) return;
            foreach (var bp in runtimeStage.monsters[runtimeStage.monsters.Length - 1].monsterBlueprints) Destroy(bp);
            Destroy(runtimeStage);
        }
    }
}
