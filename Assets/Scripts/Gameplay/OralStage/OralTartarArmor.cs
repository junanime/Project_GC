using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>A separate inner defense layer; soda remains the outer needle barrier.</summary>
    [DefaultExecutionOrder(21000)]
    public sealed class OralTartarArmor : MonoBehaviour
    {
        public const int RequiredHits = 3;
        public const float SpawnStart = 90f, SpawnChance = .10f;
        public int Hits { get; private set; }
        public bool IsProtected => armed && Hits < RequiredHits && owner != null && owner.HP > 0 && isActiveAndEnabled;
        readonly List<SpriteRenderer> plates = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> pins = new List<SpriteRenderer>();
        readonly List<Vector2> pinAnchors = new List<Vector2>();
        Monster owner; bool armed; float fractureAge, hitAge;
        static readonly Vector2[] offsets = { new Vector2(-.33f,.28f),new Vector2(.33f,.28f),new Vector2(-.41f,-.08f),new Vector2(.41f,-.08f),new Vector2(-.22f,-.34f),new Vector2(.22f,-.34f),new Vector2(0,-.20f) };

        public static void ConfigureSpawn(Monster monster)
        {
            var prior=monster.GetComponent<OralTartarArmor>(); if(prior!=null)prior.ResetForPool();
            var stage=OralFieldEvents.Instance; var level=OralAmbientContent.Instance!=null?OralAmbientContent.Instance.Level:null;
            if(stage==null || level==null || !OralMonsterRoster.IsBlueprint(monster.Blueprint) || !MonsterNeedleBarrier.Eligible(monster) || level.CurrentLevelTime<SpawnStart || Random.value>=SpawnChance)return;
            (prior??monster.gameObject.AddComponent<OralTartarArmor>()).Arm(monster);
        }
        public void Arm(Monster monster)
        {
            ResetForPool();owner=monster;
            if(owner==null||owner.BodyRenderer==null)return;
            // Every needle implementation already intercepts MonsterNeedleBarrier before damage/procs.
            if(owner.GetComponent<MonsterNeedleBarrier>()==null)owner.gameObject.AddComponent<MonsterNeedleBarrier>();
            if(plates.Count==0)for(int i=0;i<offsets.Length;i++)plates.Add(OralEventArt.Sprite(transform,"치석 갑옷 판 "+i,OralAmbientContent.EventSprite("Rock"),0));
            armed=true;enabled=true;hitAge=1;Pose();
        }
        public bool AbsorbNeedle(SpriteRenderer source,Vector2 direction,Vector2 position)
        {
            if(!IsProtected||owner.IsFieldRuntimeSuspended)return false;
            var level=OralAmbientContent.Instance!=null?OralAmbientContent.Instance.Level:null;
            if(level!=null&&OralEventArt.Paused(level))return false;
            Hits++;hitAge=0;
            if(source!=null&&source.sprite!=null)
            {
                var pin=OralEventArt.Sprite(transform,"치석에 박힌 침",source.sprite,0);pin.flipX=source.flipX;pin.flipY=source.flipY;
                pin.transform.rotation=source.transform.rotation;pin.transform.localScale=Vector3.Scale(source.transform.lossyScale,new Vector3(1/Mathf.Max(.001f,Mathf.Abs(transform.lossyScale.x)),1/Mathf.Max(.001f,Mathf.Abs(transform.lossyScale.y)),1));pins.Add(pin);
                Vector2 entry=direction.sqrMagnitude>.001f?-direction.normalized:Vector2.left;entry=OralMapGeometry.Rotate(entry,(Hits-2)*13);pinAnchors.Add(entry);
            }
            if(Hits>=RequiredHits)fractureAge=0;
            Pose();return true;
        }
        void LateUpdate()
        {
            if(!armed)return;
            if(owner==null||owner.HP<=0){ResetForPool();return;}
            var level=OralAmbientContent.Instance!=null?OralAmbientContent.Instance.Level:null;
            bool paused=owner.IsFieldRuntimeSuspended||(level!=null&&OralEventArt.Paused(level));
            if(!paused){hitAge+=Time.deltaTime;if(Hits>=RequiredHits)fractureAge+=Time.deltaTime;}
            if(fractureAge>=.42f){ResetForPool();return;}Pose();
        }
        void Pose()
        {
            var body=owner.BodyRenderer;if(body==null)return;var b=PromptSpriteGeometry.WorldBounds(body);float scatter=Mathf.Clamp01(fractureAge/.42f);
            // Hiccup owns the original renderer's forceRenderingOff flag and reads
            // its enabled state to draw a lifted copy. Keep that source available.
            bool lifted=owner.GetComponent<OralHiccupLift>()?.Active??false;
            bool bouncing=owner.GetComponent<OralTasteBounce>()?.Active??false;
            bool hidden=!body.enabled||(!lifted&&body.forceRenderingOff)||(!lifted&&!bouncing&&owner.IsFieldRuntimeSuspended);
            for(int i=0;i<plates.Count;i++)
            {
                var r=plates[i];r.enabled=!hidden&&armed;if(r.sprite==null)continue;
                var p=offsets[i];r.transform.position=b.center+new Vector3(p.x*b.size.x,p.y*b.size.y,0)*(1+scatter*1.4f)+Vector3.down*(scatter*scatter*.25f);
                r.transform.rotation=Quaternion.Euler(0,0,i*53+scatter*(i%2==0?90:-90));
                float size=Mathf.Min(b.size.x,b.size.y)*(.29f+(i%3)*.025f);r.transform.localScale=Vector3.one*(size/r.sprite.bounds.size.x/Mathf.Max(.001f,Mathf.Abs(transform.lossyScale.x)));
                r.sortingLayerID=body.sortingLayerID;r.sortingOrder=body.sortingOrder+3;
                float flash=Mathf.Clamp01(1-hitAge/.15f);r.color=Color.Lerp(new Color(1,.96f,.83f,1-scatter),new Color(1,1,1,1-scatter),flash);
            }
            for(int i=0;i<pins.Count;i++){var p=pins[i];p.enabled=!hidden;p.sortingLayerID=body.sortingLayerID;p.sortingOrder=body.sortingOrder+4;p.transform.position=b.center+(Vector3)Vector2.Scale(pinAnchors[i],new Vector2(b.extents.x*.9f,b.extents.y*.8f));p.color=new Color(1,1,1,1-scatter);}
        }
        public void ResetForPool()
        {
            armed=false;Hits=0;fractureAge=0;
            foreach(var r in plates)if(r!=null)r.enabled=false;
            foreach(var r in pins)if(r!=null){r.gameObject.SetActive(false);Destroy(r.gameObject);}pins.Clear();pinAnchors.Clear();owner=null;
        }
        void OnDisable(){ResetForPool();}
    }
}
