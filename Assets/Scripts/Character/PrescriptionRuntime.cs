using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vampire
{
    [Serializable]
    public sealed class PrescriptionState
    {
        public int tag;
        public int[] quests;
        public float[] progress;
        public bool claimed;
    }

    [DisallowMultipleComponent]
    public sealed class PrescriptionRuntime : MonoBehaviour
    {
        // Stable IDs are saved across scenes. Each run draws three distinct goals from ONE tag.
        public enum Goal { Kills, Elite, Burst, ActiveKills, Portal, Overcharge, Room, Distance, ActiveUses, Dashes, DashKills, ActiveTime }
        static readonly Goal[][] pools = {
            new[] { Goal.Kills, Goal.Elite, Goal.Burst, Goal.ActiveKills },
            new[] { Goal.Portal, Goal.Overcharge, Goal.Room, Goal.Distance },
            new[] { Goal.ActiveUses, Goal.Dashes, Goal.DashKills, Goal.ActiveTime }
        };
        public static readonly string[] Tags = { "전투", "탐험", "스킬" };
        static readonly string[] descriptions = { "몬스터 150마리 처치", "엘리트 몬스터 1마리 처치", "15초 안에 몬스터 20마리 처치", "액티브 사용 중 몬스터 25마리 처치", "혈전 입구 1개 개방", "혈전 과충전 1회 완료", "미니스테이지 1회 클리어", "필드에서 200m 이동", "액티브 스킬 3회 사용", "대시 8회 사용", "대시 후 3초 내 몬스터 30마리 처치", "액티브 효과 누적 20초 유지" };
        static readonly float[] targets = { 150, 1, 20, 25, 1, 1, 1, 200, 3, 8, 30, 20 };
        Character owner;
        PrescriptionState state;
        Vector2 previous;
        float lastDash = -100;
        readonly Queue<float> recentKills = new Queue<float>();
        public int Tag => state != null ? state.tag : 0;
        public int Completed => state == null ? 0 : Enumerable.Range(0,3).Count(IsComplete);
        public bool Claimed => state != null && state.claimed;
        public bool Ready => Completed == 3 && !Claimed;
        public void Initialize(Character character)
        {
            owner = character; previous = transform.position;
            var balance = RemakeBalance.Current;
            int tag = balance.randomTagEachRun ? UnityEngine.Random.Range(0,3) : (int)balance.designatedTag;
            StartPrescription(tag, UnityEngine.Random.Range(1,int.MaxValue));
        }
        public void StartPrescription(int tag, int seed)
        {
            tag = Mathf.Clamp(tag,0,2);
            var random = new System.Random(seed);
            var candidates = pools[tag].OrderBy(_ => random.Next()).Take(3).Select(x => (int)x).ToArray();
            state = new PrescriptionState { tag=tag, quests=candidates, progress=new float[3] };
            recentKills.Clear(); lastDash=-100;
        }
        void OnEnable() { Monster.Died += OnKilled; }
        void OnDisable() { Monster.Died -= OnKilled; }
        public string Description(int index) => descriptions[state.quests[index]];
        public float Target(int index) => targets[state.quests[index]];
        public float Progress(int index) => state.progress[index];
        public bool IsComplete(int index) => state.progress[index] >= Target(index);
        public void Record(Goal goal, float amount=1)
        {
            if(state==null || state.claimed || amount<=0) return;
            for(int i=0;i<3;i++) if(state.quests[i]==(int)goal)
            {
                bool wasComplete=IsComplete(i);
                state.progress[i]=Mathf.Min(Target(i),state.progress[i]+amount);
                if(!wasComplete && IsComplete(i) && Application.isPlaying)
                    GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.RewardEvent);
            }
        }
        public void RecordDash() { lastDash=Time.time; Record(Goal.Dashes); }
        void OnKilled(Monster monster)
        {
            if(owner==null || !owner.IsAlive || !monster.WasKilledByPlayer || monster is BloodClotMonster || monster.IsFieldRuntimeSuspended) return;
            Record(Goal.Kills);
            if(monster.Blueprint is EliteMonsterBlueprint) Record(Goal.Elite);
            if(owner.Skills!=null && owner.Skills.Active) Record(Goal.ActiveKills);
            if(Time.time-lastDash<=3) Record(Goal.DashKills);
            recentKills.Enqueue(Time.time);
            while(recentKills.Count>0 && Time.time-recentKills.Peek()>15)recentKills.Dequeue();
            for(int i=0;i<3;i++) if(state.quests[i]==(int)Goal.Burst && !IsComplete(i))state.progress[i]=Mathf.Min(20,recentKills.Count);
        }
        void Update()
        {
            Vector2 position=transform.position;
            if(owner!=null && owner.IsAlive && Time.deltaTime>0)
            {
                if(!MiniStageRuntimeState.IsInsideMiniStage)
                {
                    float distance=Vector2.Distance(previous,position);
                    if(distance<3)Record(Goal.Distance,distance); // Do not count room/scene teleports.
                }
                if(owner.Skills!=null && owner.Skills.Active)Record(Goal.ActiveTime,Time.deltaTime);
                while(recentKills.Count>0 && Time.time-recentKills.Peek()>15)recentKills.Dequeue();
                for(int i=0;i<3;i++)if(state.quests[i]==(int)Goal.Burst&&!IsComplete(i))state.progress[i]=recentKills.Count;
            }
            previous=position;
        }
        public bool TryClaim(AbilitySelectionDialog dialog)
        {
            if(!Ready || owner==null || !owner.IsAlive || Time.timeScale<=0 || dialog==null || dialog.MenuOpen || owner.Skills!=null&&owner.Skills.IsCutin) return false;
            if(!dialog.HasAvailableLegendaryAbilities())return false;
            dialog.OpenLegendary();
            if(!dialog.MenuOpen)return false;
            state.claimed=true;
            return true;
        }
        public PrescriptionState Capture() => state==null?null:JsonUtility.FromJson<PrescriptionState>(JsonUtility.ToJson(state));
        public void Restore(PrescriptionState saved)
        {
            if(saved==null || saved.tag<0 || saved.tag>=pools.Length || saved.quests==null || saved.quests.Length!=3 || saved.progress==null || saved.progress.Length!=3 || saved.quests.Distinct().Count()!=3 || saved.quests.Any(x=>!pools[saved.tag].Contains((Goal)x))) return;
            state=JsonUtility.FromJson<PrescriptionState>(JsonUtility.ToJson(saved));
            for(int i=0;i<3;i++)state.progress[i]=float.IsNaN(state.progress[i])?0:Mathf.Clamp(state.progress[i],0,Target(i));
            recentKills.Clear(); previous=transform.position;
        }
    }
}
