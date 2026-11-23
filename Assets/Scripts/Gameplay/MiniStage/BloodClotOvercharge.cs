using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public sealed class BloodClotOvercharge : MonoBehaviour
    {
        public enum Challenge { Kills, ActiveKills, DashKills }
        public const float DashWindowSeconds = 5f;
        public static Challenge PickChallenge(int roll)
        {
            roll=Mathf.Clamp(roll,0,99);
            return roll<38 ? Challenge.Kills : roll<69 ? Challenge.ActiveKills : Challenge.DashKills;
        }
        public Challenge Kind { get; private set; }
        public float Progress { get; private set; }
        public bool Started { get; private set; }
        public bool Enhanced { get; private set; }
        public float Target => Kind==Challenge.Kills ? RemakeBalance.Current.overchargeKills : 8;
        public bool DashExpired { get; private set; }
        public float DashRemaining => dashWindowStarted ? Mathf.Max(0,DashWindowSeconds-(Time.time-dashWindowStart)) : 0;
        public bool PlayerInside => player!=null && Vector2.Distance(player.transform.position,transform.position)<=RemakeBalance.Current.overchargeRadius;
        public bool CanProgress => Started && !Enhanced && !travelReserved && PlayerInside && player.IsAlive && Time.timeScale>0 && !MiniStageRuntimeState.IsInsideMiniStage;
        Character player;
        LineRenderer ring;
        // A single HUD/input owner chooses the nearest eligible clot when rings overlap.
        static readonly List<BloodClotOvercharge> active = new List<BloodClotOvercharge>();
        BloodClotMiniStagePortal portal;
        public bool Available => !travelReserved && (portal == null || !portal.Reserved) && !AcidLakeEvent.Contains(transform.position);
        public bool AtEntrance => portal != null && portal.CanInteract && InteractionFocus.IsFocused(portal);
        public bool CanBegin => Available && !Started && PlayerInside && player.IsAlive && Time.timeScale > 0f &&
            !MiniStageRuntimeState.IsInsideMiniStage && !player.IsPortalTravelling;
        public static BloodClotOvercharge NearestGuide(Vector2 position)
        {
            BloodClotOvercharge nearest = null;
            float best = float.PositiveInfinity;
            bool guidingActiveRing = false;
            foreach (var charge in active)
            {
                if (charge == null || !charge.Available) continue;
                float distance = ((Vector2)charge.transform.position - position).sqrMagnitude;
                float radius = RemakeBalance.Current.overchargeRadius;
                bool underway = charge.Started && !charge.Enhanced;
                bool insideActiveRing = underway && distance <= radius * radius;
                float cueRadius = underway ? radius * 3f : radius;
                if (distance > cueRadius * cueRadius || (guidingActiveRing && !insideActiveRing)) continue;
                if (insideActiveRing != guidingActiveRing || distance < best)
                {
                    guidingActiveRing = insideActiveRing;
                    best = distance;
                    nearest = charge;
                }
            }
            return nearest;
        }
        public string Objective
        {
            get
            {
                switch (Kind)
                {
                    case Challenge.ActiveKills: return $"액티브 효과 중 {Target:0}마리 처치";
                    case Challenge.DashKills: return $"대시 한 번 후 {DashWindowSeconds:0}초 안에 {Target:0}마리 처치";
                    default: return $"몬스터 {Target:0}마리 처치";
                }
            }
        }
        Material ringMaterial;
        bool travelReserved;
        int observedDash;
        float dashWindowStart;
        bool dashWindowStarted;
        public void SetTravelReserved(bool value)
        {
            travelReserved=value;
            if(ring!=null)ring.enabled=!value;
        }
        void Awake()
        {
            player=FindObjectOfType<LevelManager>()?.PlayerCharacter;
            portal=GetComponent<BloodClotMiniStagePortal>();
            Kind=PickChallenge(Random.Range(0,100));
            ring=gameObject.AddComponent<LineRenderer>();
            ringMaterial=new Material(Shader.Find("Sprites/Default"));ring.sharedMaterial=ringMaterial;
            ring.useWorldSpace=true;ring.loop=true;ring.positionCount=64;ring.widthMultiplier=.065f;ring.sortingOrder=12;
            for(int i=0;i<64;i++){float angle=i*Mathf.PI*2/64;ring.SetPosition(i,transform.position+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle))*RemakeBalance.Current.overchargeRadius);}
        }
        void OnEnable(){active.Add(this);Monster.Died+=OnKilled;}
        void OnDisable(){active.Remove(this);Monster.Died-=OnKilled;}
        void OnDestroy(){if(ringMaterial!=null)Destroy(ringMaterial);}
        public bool Begin()
        {
            if(!CanBegin)return false;
            Started=true;
            FindObjectOfType<LevelManager>()?.ReserveOverchargeLanes(this);
            observedDash=player.Skills!=null?player.Skills.DashSequence:0;
            FindObjectOfType<EntityManager>()?.SpawnOverchargeElite((Vector2)transform.position+Vector2.up*(RemakeBalance.Current.overchargeRadius+2));
            return true;
        }
        void Update()
        {
            if(travelReserved)return;
            if(player==null)player=FindObjectOfType<LevelManager>()?.PlayerCharacter;
            RefreshDashWindow();
            var color=Enhanced?new Color(1,.75f,.18f):CanProgress?new Color(1,.4f,.3f):new Color(.7f,.65f,.5f,.55f);
            ring.startColor=ring.endColor=color;
        }
        void RefreshDashWindow()
        {
            if(!Started || Enhanced || Kind!=Challenge.DashKills || player==null || player.Skills==null)return;
            var skills=player.Skills;
            if(skills.DashSequence!=observedDash)
            {
                observedDash=skills.DashSequence;
                dashWindowStart=skills.LastDashTime;
                dashWindowStarted=true;
                DashExpired=false;
                Progress=0;
            }
            if(dashWindowStarted && Time.time-dashWindowStart>DashWindowSeconds)
            {
                dashWindowStarted=false;
                DashExpired=true;
                Progress=0;
            }
        }
        void OnKilled(Monster monster)
        {
            RefreshDashWindow();
            if(!CanProgress || monster==null || !monster.WasKilledByPlayer || monster.IsMiniStageOwned || monster.IsFieldRuntimeSuspended || monster is BloodClotMonster)return;
            if(Kind==Challenge.ActiveKills && (player.Skills==null || !player.Skills.Active))return;
            if(Kind==Challenge.DashKills && (!dashWindowStarted || Time.time-dashWindowStart>DashWindowSeconds))return;
            Advance(1);
        }
        public void Advance(float amount)
        {
            if(!CanProgress || amount<=0)return;
            Progress=Mathf.Min(Target,Progress+amount);
            if(Progress>=Target)
            {
                Enhanced=true;
                RunScore.Current?.Overcharge(this);
                BloodClotOverchargedArt.Apply(gameObject);
                player.GetComponent<PrescriptionRuntime>()?.Record(PrescriptionRuntime.Goal.Overcharge);
                GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.MiniStageClear);
            }
        }
    }
}
