using UnityEngine;

namespace Vampire
{
    [DisallowMultipleComponent]
    public sealed class ConstipationKingDirector : MonoBehaviour
    {
        public const float ArrivalSeconds = 15 * 60;
        public ConstipationKingRuntime Boss { get; private set; }
        public bool Spawned { get; private set; }
        public bool Defeated { get; private set; }
        LevelManager level;
        bool inColon;
        public static ConstipationKingDirector Ensure(LevelManager owner)
        {
            var d = owner.GetComponent<ConstipationKingDirector>() ?? owner.gameObject.AddComponent<ConstipationKingDirector>();
            d.level = owner; return d;
        }
        void Update()
        {
            if (level == null) return;
            bool colon = StageProgression.Ensure(level).StageNumber == 2 && FiniteStomachMap.Instance != null && FiniteStomachMap.Instance.IsColon;
            if (!colon)
            {
                if (inColon) ResetEncounter();
                inColon = false; return;
            }
            inColon = true;
            ColonExitSequence.Ensure(level);
            if (!Spawned && level.CurrentLevelTime >= ArrivalSeconds) TrySpawn();
        }
        public bool TrySpawn()
        {
            var map = FiniteStomachMap.Instance;
            if (Spawned || level == null || level.IsLevelEnded || level.IsRunFlowPaused || StageEntryLoading.IsLoading ||
                Time.timeScale <= 0 || MiniStageRuntimeState.IsInsideMiniStage || level.PlayerCharacter == null || !level.PlayerCharacter.IsAlive ||
                map == null || !map.IsColon || StageProgression.Ensure(level).StageNumber != 2 || level.CurrentLevelTime < ArrivalSeconds) return false;
            var opening = StomachOpening.Find(false);
            var shape = map.OpeningShape(false);
            var from = opening != null ? opening.FrontLip + opening.Inward * 3 : map.UVToWorld(new Vector2(shape.x, shape.y)) + Vector2.up * 7;
            var at = map.ClampToFloor(from, ConstipationKingRuntime.BodyRadius + .4f);
            // Claim synchronously before creation. Optional bosses have no role in this gate.
            Spawned = true;
            Boss = ConstipationKingRuntime.Spawn(level, at, this);
            if (Boss == null) { Spawned = false; return false; }
            opening?.SetOpened(0);
            level.NotifyExternalFinalBossSpawned(); GameAudioManager.StartBossAudio();
            ColonBossDirector.ShowNotice("변비왕 등장", "변비왕의 지팡이 열쇠로 좌측 하단 출구의 봉인을 풀어주세요");
            return true;
        }
        public void NotifyDefeated(ConstipationKingRuntime boss)
        {
            if (Defeated || !Spawned || boss == null || boss != Boss || !boss.Dead || level == null || level.IsLevelEnded || StageProgression.Ensure(level).StageNumber != 2) return;
            Defeated = true;
            ColonExitSequence.Ensure(level).FinishDefeat();
            GameAudioManager.FinishStageBossAudio();
        }
        public void BeginDefeat(ConstipationKingRuntime boss)
        {
            if (!Spawned || Defeated || boss == null || boss != Boss || !boss.Dead || level == null || level.IsLevelEnded) return;
            Vector3 point = boss.Visual != null ? (Vector3)boss.Visual.KeyDropPoint : boss.transform.position;
            boss.Visual?.ReleaseKeyScepter();
            ColonExitSequence.Ensure(level).DropKey(point, boss.transform.position);
        }
        void ResetEncounter()
        {
            if (Boss != null) { Boss.gameObject.SetActive(false); Destroy(Boss.gameObject); }
            Boss = null; Spawned = Defeated = false;
        }
        void OnDestroy() { ResetEncounter(); }
    }
}
