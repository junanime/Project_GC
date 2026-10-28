using TMPro;
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
        public float DashRemaining => dashWindowStarted ? Mathf.Max(0,DashWindowSeconds-(Time.time-dashWindowStart)) : 0;
        public bool PlayerInside => player!=null && Vector2.Distance(player.transform.position,transform.position)<=RemakeBalance.Current.overchargeRadius;
        public bool CanProgress => Started && !Enhanced && !travelReserved && PlayerInside && player.IsAlive && Time.timeScale>0 && !MiniStageRuntimeState.IsInsideMiniStage;
        Character player;
        LineRenderer ring;
        TextMeshPro label;
        Material ringMaterial;
        bool travelReserved;
        int observedDash;
        float dashWindowStart;
        bool dashWindowStarted;
        public void SetTravelReserved(bool value)
        {
            travelReserved=value;
            if(ring!=null)ring.enabled=!value;
            if(label!=null)label.gameObject.SetActive(!value);
        }
        void Awake()
        {
            player=FindObjectOfType<LevelManager>()?.PlayerCharacter;
            Kind=PickChallenge(Random.Range(0,100));
            ring=gameObject.AddComponent<LineRenderer>();
            ringMaterial=new Material(Shader.Find("Sprites/Default"));ring.sharedMaterial=ringMaterial;
            ring.useWorldSpace=true;ring.loop=true;ring.positionCount=64;ring.widthMultiplier=.065f;ring.sortingOrder=12;
            for(int i=0;i<64;i++){float angle=i*Mathf.PI*2/64;ring.SetPosition(i,transform.position+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle))*RemakeBalance.Current.overchargeRadius);}
            var textObject=new GameObject("혈전 과충전 진행");textObject.transform.SetParent(transform,false);textObject.transform.localPosition=Vector3.up*1.3f;
            label=textObject.AddComponent<TextMeshPro>();
            var font=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig")?.font;if(font!=null)label.font=font;
            label.fontSize=1.8f;label.color=new Color(.26f,.09f,.06f);label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(8,2);
            label.GetComponent<MeshRenderer>().sortingOrder=100;
        }
        void OnEnable(){Monster.Died+=OnKilled;}
        void OnDisable(){Monster.Died-=OnKilled;}
        void OnDestroy(){if(ringMaterial!=null)Destroy(ringMaterial);}
        public bool Begin()
        {
            if(travelReserved || Started || !PlayerInside || !player.IsAlive || Time.timeScale<=0 || MiniStageRuntimeState.IsInsideMiniStage)return false;
            Started=true;
            observedDash=player.Skills!=null?player.Skills.DashSequence:0;
            FindObjectOfType<EntityManager>()?.SpawnOverchargeElite((Vector2)transform.position+Vector2.up*(RemakeBalance.Current.overchargeRadius+2));
            return true;
        }
        void Update()
        {
            if(travelReserved)return;
            if(player==null)player=FindObjectOfType<LevelManager>()?.PlayerCharacter;
            if(PlayerInside && GameInput.Pressed(GameKeyAction.Overcharge))Begin();
            RefreshDashWindow();
            var color=Enhanced?new Color(1,.75f,.18f):CanProgress?new Color(1,.4f,.3f):new Color(.7f,.65f,.5f,.55f);
            ring.startColor=ring.endColor=color;
            string kind=Kind==Challenge.Kills?"몬스터 처치":Kind==Challenge.ActiveKills?"액티브 효과 중 처치":"대쉬 후 5초 내 처치";
            string hint=Kind==Challenge.DashKills ? (DashRemaining>0?$"남은 시간 {DashRemaining:0.0}초":"대쉬로 도전 시작 · 실패 시 재도전") : "범위 안에서 진행";
            label.text=Enhanced?"격화 혈전\n스킬 강화 + 레벨업 상자":!Started?$"{GameInput.Label(GameKeyAction.Overcharge)} · 과충전\n{kind} {Target:0}마리 / {GameInput.Label(GameKeyAction.Interact)} · 일반 입장":$"{kind} {Mathf.FloorToInt(Progress)}/{Target:0}\n{(PlayerInside?hint:"범위 밖 · 처치 집계 정지")}";
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
                Progress=0;
            }
            if(dashWindowStarted && Time.time-dashWindowStart>DashWindowSeconds)
            {
                dashWindowStarted=false;
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
