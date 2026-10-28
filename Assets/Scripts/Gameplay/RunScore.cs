using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Vampire
{
    [Serializable]
    public sealed class RunScoreData
    {
        public int normal, special, elite, miniBoss, boss, entered, cleared, overcharge;
        public float seconds;
        public bool success;
        public string rules = "qa_s1_v1";
        public int Total => normal * 30 + special * 50 + elite * 100 + miniBoss * 500 + boss * 1500 + entered * 100 + cleared * 300 + overcharge * 200 + (success ? 1000 : 0);
    }

    // One instance per run. Room/portal identities prevent re-entry rewards.
    public sealed class RunScore : MonoBehaviour
    {
        public static RunScore Current { get; private set; }
        public RunScoreData Data { get; private set; } = new RunScoreData();
        public string Exclusion { get; private set; }
        public string Submission { get; private set; } = "온라인 기록 준비 중";
        readonly HashSet<int> portals = new HashSet<int>();
        readonly HashSet<int> overcharges = new HashSet<int>();
        LevelManager level;
        string runId;
        bool finished;
        Task<string> ticket;
        static Task pendingFinish = Task.CompletedTask;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Current = null; pendingFinish = Task.CompletedTask; }
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
            run.ticket = BeginAfterPrevious();
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
            if (monster is BossMonster) Data.boss++;
            else if (monster is MiniBossMonster) Data.miniBoss++;
            else if (monster.Blueprint is EliteMonsterBlueprint) Data.elite++;
            else if (monster is BloodClotMonster) return;
            else if (IsSpecial(monster)) Data.special++;
            else Data.normal++;
        }
        public static bool IsSpecial(Monster m) => m is SniperMonster || m is TrapMonster || m is AcidLeechMonster || m is MonsterSupportCaster || m is NutritionThiefBacteriaMonster || m is TreasureRunnerMonster ||
            (m != null && ColonMonsterRoster.InColon && ColonMonsterRoster.Active.IsSpecial(m.Blueprint));
        public void SpecialKill() { if (!finished) Data.special++; }
        public void BossKill() { if (!finished) Data.boss++; }
        public void Enter(UnityEngine.Object portal) { if (!finished && portal != null && portals.Add(portal.GetInstanceID())) Data.entered++; }
        public void Clear() { if (!finished) Data.cleared++; }
        public void Overcharge(UnityEngine.Object source) { if (!finished && source != null && overcharges.Add(source.GetInstanceID())) Data.overcharge++; }
        public void Finish(bool success)
        {
            if (finished) return;
            finished = true; Data.success = success;
            pendingFinish = FinishOnline();
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
