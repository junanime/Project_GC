using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Vampire
{
    [Serializable]
    public sealed class RunScoreData
    {
        public int normal, special, elite, miniBoss, boss, entered, cleared, overcharge, gold, silver;
        public float seconds;
        public bool success;
        public string rules = "qa_s1_v1";
        public int Total => rules == GStarScoreRules.Version ? GStarScoreRules.Total(this) : normal * 30 + special * 50 + elite * 100 + miniBoss * 500 + boss * 1500 + entered * 100 + cleared * 300 + overcharge * 200 + (success ? 1000 : 0);
    }

    // One instance per run. Room/portal identities prevent re-entry rewards.
    public sealed class RunScore : MonoBehaviour
    {
        public static RunScore Current { get; private set; }
        public RunScoreData Data { get; private set; } = new RunScoreData();
        public string Exclusion { get; private set; }
        public string Submission { get; private set; } = "온라인 기록 준비 중";
        public string RunId => runId;
        public string SessionId { get; private set; }
        public int StageId { get; private set; }
        public bool IsFinished => finished;
        readonly HashSet<int> portals = new HashSet<int>();
        readonly HashSet<int> clearedPortals = new HashSet<int>();
        readonly HashSet<int> overcharges = new HashSet<int>();
        int activePortal;
        string activeMiniKind = "";
        LevelManager level;
        string runId;
        bool finished;
        Task<string> ticket;
        static Task pendingFinish = Task.CompletedTask;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; pendingFinish = Task.CompletedTask; }
        static async Task<string> BeginAfterPrevious()
        {
            await pendingFinish;
            return await QaLeaderboard.BeginRun();
        }

        public static void Install(LevelManager owner)
        {
            if (owner.GetComponent<RunScore>() != null) return;
            var run = owner.gameObject.AddComponent<RunScore>();
            Current = run; run.level = owner;
            if (GStarDemo.Enabled)
            {
                run.Data.rules = GStarScoreRules.Version;
                run.runId = GStarSession.RunId;
                run.SessionId = GStarSession.SessionId;
                run.StageId = GStarSession.StageId;
                run.Submission = "이 PC에 기록 저장 준비 중";
                if (!GStarSession.ActiveAttempt || string.IsNullOrEmpty(run.runId))
                    Exclude("체험 출전 절차를 거치지 않은 연습 실행");
                if (QaLeaderboard.IsBatchQa) Exclude("자동 검수 실행");
            }
            else run.ticket = BeginAfterPrevious();
        }
        void OnDestroy() { if (Current == this) Current = null; }
        void Update()
        {
            if (!finished && !StageEntryLoading.IsLoading && level != null && !level.IsLevelEnded)
                Data.seconds += Time.deltaTime;
        }
        public static void Exclude(string reason)
        {
            if (Current != null) { Current.Exclusion = reason; Current.Submission = "연습 기록 · 순위 등록 제외"; }
        }
        public void Kill(Monster monster)
        {
            if (finished || monster == null) return;
            if (monster is BossMonster) GStarScoreRules.Add(ref Data.boss);
            else if (monster is MiniBossMonster) GStarScoreRules.Add(ref Data.miniBoss);
            else if (monster.Blueprint is EliteMonsterBlueprint) GStarScoreRules.Add(ref Data.elite);
            else if (monster is BloodClotMonster) return;
            else if (IsSpecial(monster)) GStarScoreRules.Add(ref Data.special);
            else GStarScoreRules.Add(ref Data.normal);
        }
        public static bool IsSpecial(Monster m) => m is SniperMonster || m is TrapMonster || m is AcidLeechMonster || m is MonsterSupportCaster || m is NutritionThiefBacteriaMonster || m is TreasureRunnerMonster ||
            (m != null && ColonMonsterRoster.InColon && ColonMonsterRoster.Active.IsSpecial(m.Blueprint));
        public void SpecialKill() { if (!finished) GStarScoreRules.Add(ref Data.special); }
        public void NormalKill() { if (!finished) GStarScoreRules.Add(ref Data.normal); }
        public void MiniBossKill() { if (!finished) GStarScoreRules.Add(ref Data.miniBoss); }
        public void BossKill() { if (!finished) GStarScoreRules.Add(ref Data.boss); }
        public void Gold(int amount) { if (!finished && Data.rules == GStarScoreRules.Version) GStarScoreRules.Add(ref Data.gold, amount); }
        public void Silver(int amount) { if (!finished && Data.rules == GStarScoreRules.Version) GStarScoreRules.Add(ref Data.silver, amount); }
        public void Enter(UnityEngine.Object portal)
        {
            if (finished || portal == null) return;
            activePortal = portal.GetInstanceID();
            var mini = FindObjectOfType<MiniStageDirector>();
            activeMiniKind = mini != null && mini.CurrentRoom != null ? mini.CurrentRoom.GetType().Name : portal.GetType().Name;
            if (portals.Add(activePortal))
            {
                GStarScoreRules.Add(ref Data.entered);
                GStarAnalytics.Record("mini_enter", activePortal.ToString(), activeMiniKind);
            }
        }
        public void Clear()
        {
            if (finished) return;
            if (Data.rules == GStarScoreRules.Version && (activePortal == 0 || !clearedPortals.Add(activePortal))) return;
            GStarScoreRules.Add(ref Data.cleared);
            GStarAnalytics.Record("mini_success", activePortal.ToString(), activeMiniKind);
        }
        public void Overcharge(UnityEngine.Object source)
        {
            if (finished || source == null || !overcharges.Add(source.GetInstanceID())) return;
            GStarScoreRules.Add(ref Data.overcharge);
            GStarAnalytics.Record("overcharge_success", source.GetInstanceID().ToString(), source.GetType().Name);
        }
        public void Finish(bool success)
        {
            if (finished) return;
            finished = true; Data.success = success;
            if (Data.rules == GStarScoreRules.Version)
            {
                bool saved = GStarLeaderboard.CaptureCompleted(runId, SessionId, StageId, Data, Exclusion);
                Submission = saved ? (Exclusion == null ? "이 PC에 저장 완료 · 이름을 등록해 주세요" : "연습 기록 · 순위 등록 제외") : GStarLeaderboard.LastError;
            }
            else pendingFinish = FinishOnline();
        }
        async Task FinishOnline()
        {
            if (Exclusion != null) return;
            try
            {
                runId = ticket != null ? await ticket : null;
                if (Exclusion != null) return;
                if (string.IsNullOrEmpty(runId)) { Submission = "오프라인 출전 · 순위 등록 제외"; return; }
                Submission = "기록 전송 중";
                bool sent = await QaLeaderboard.Submit(runId, Data);
                Submission = sent ? (string.IsNullOrEmpty(QaLeaderboard.LastRejection)?"최고기록 반영 완료":"검증 조건 불일치 · 순위 등록 제외") : "연결 복구 후 기록 재전송";
            }
            catch (Exception) { Submission = "온라인 기록 연결 실패"; }
        }
    }
}
