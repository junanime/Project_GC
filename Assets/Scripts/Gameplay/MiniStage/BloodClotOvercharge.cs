using TMPro;
using UnityEngine;

namespace Vampire
{
    public sealed class BloodClotOvercharge : MonoBehaviour
    {
        public enum Challenge { Kills, Stay }
        public Challenge Kind { get; private set; }
        public float Progress { get; private set; }
        public bool Started { get; private set; }
        public bool Enhanced { get; private set; }
        public float Target => Kind==Challenge.Kills ? RemakeBalance.Current.overchargeKills : RemakeBalance.Current.overchargeStaySeconds;
        public bool PlayerInside => player!=null && Vector2.Distance(player.transform.position,transform.position)<=RemakeBalance.Current.overchargeRadius;
        public bool CanProgress => Started && !Enhanced && PlayerInside && player.IsAlive && Time.timeScale>0 && !MiniStageRuntimeState.IsInsideMiniStage;
        Character player;
        LineRenderer ring;
        TextMeshPro label;
        Material ringMaterial;
        void Awake()
        {
            player=FindObjectOfType<LevelManager>()?.PlayerCharacter;
            Kind=(Challenge)Random.Range(0,2);
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
            if(Started || !PlayerInside || !player.IsAlive || Time.timeScale<=0 || MiniStageRuntimeState.IsInsideMiniStage)return false;
            Started=true;
            FindObjectOfType<EntityManager>()?.SpawnOverchargeElite((Vector2)transform.position+Vector2.up*(RemakeBalance.Current.overchargeRadius+2));
            return true;
        }
        void Update()
        {
            if(player==null)player=FindObjectOfType<LevelManager>()?.PlayerCharacter;
            if(PlayerInside && GameInput.GetKeyDown(KeyCode.Q))Begin();
            if(CanProgress && Kind==Challenge.Stay)Advance(Time.deltaTime);
            var color=Enhanced?new Color(1,.75f,.18f):CanProgress?new Color(1,.4f,.3f):new Color(.7f,.65f,.5f,.55f);
            ring.startColor=ring.endColor=color;
            string kind=Kind==Challenge.Kills?"처치":"체류";
            label.text=Enhanced?"격화 혈전\n스킬 강화 + 레벨업 상자":!Started?$"Q · 과충전 ({kind})\nE · 일반 입장":$"{kind} {Mathf.FloorToInt(Progress)}/{Target:0}{(Kind==Challenge.Stay?"초":"")}\n{(PlayerInside?"범위 안에서 진행":"범위 밖 · 진행 정지")}";
        }
        void OnKilled(Monster monster)
        {
            if(CanProgress && Kind==Challenge.Kills && monster.WasKilledByPlayer && !monster.IsMiniStageOwned && !monster.IsFieldRuntimeSuspended && !(monster is BloodClotMonster)) Advance(1);
        }
        public void Advance(float amount)
        {
            if(!CanProgress || amount<=0)return;
            Progress=Mathf.Min(Target,Progress+amount);
            if(Progress>=Target)
            {
                Enhanced=true;
                player.GetComponent<PrescriptionRuntime>()?.Record(PrescriptionRuntime.Goal.Overcharge);
                GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.MiniStageClear);
            }
        }
    }
}
