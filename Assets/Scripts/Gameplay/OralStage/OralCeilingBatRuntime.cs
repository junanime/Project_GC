using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>Ceiling-only presentation on the ordinary monster damage/loot runtime.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(18000)]
    public sealed class OralCeilingBatRuntime : MonoBehaviour
    {
        public enum BatStage { Hanging, Unfolding, Turning, Flying }
        public const int Count=10;
        public const float ArrivalDelay=1f, UnfoldSeconds=.45f, TurnSeconds=.55f, FlapPeriod=.6f;
        public BatStage Stage {get;private set;}
        public float ArrivalAge {get;private set;}
        public float FlightOrientationDegrees {get;private set;}=180;
        public bool IsFlying=>active&&Stage==BatStage.Flying;
        public Monster Monster=>monster;
        public SpriteRenderer Body=>body;
        public SpriteRenderer HangingVisual=>hanging;
        public SpriteRenderer LeftWing=>leftWing;
        public SpriteRenderer RightWing=>rightWing;
        public bool ShadowsHidden
        {
            get
            {
                foreach(var item in shadows)if(item.Renderer!=null&&item.Renderer.gameObject.activeInHierarchy&&item.Renderer.enabled&&!item.Renderer.forceRenderingOff)return false;
                return true;
            }
        }
        sealed class ShadowState { public SpriteRenderer Renderer; public bool Active; }
        readonly List<ShadowState> shadows=new List<ShadowState>();
        OralFieldEvents field;Monster monster;SpriteRenderer body,hanging,leftWing,rightWing;
        SpriteAnimator animator;bool animatorEnabled,active,ownsSuspension,originalFlipX,originalFlipY;
        Vector3 origin,scale;Quaternion rotation;Color originalColor;Sprite originalSprite;
        float phase;Transform parts;

        public void Configure(OralFieldEvents events,Monster actor,int index)
        {
            ResetForPool();field=events;monster=actor;body=actor!=null?actor.BodyRenderer:null;
            if(body==null||!OralCeilingBatArt.Load())return;
            active=true;enabled=true;Stage=BatStage.Hanging;ArrivalAge=0;FlightOrientationDegrees=180;phase=index*.73f;
            origin=body.transform.localPosition;scale=body.transform.localScale;rotation=body.transform.localRotation;
            originalColor=body.color;originalSprite=body.sprite;originalFlipX=body.flipX;originalFlipY=body.flipY;
            animator=body.GetComponent<SpriteAnimator>();animatorEnabled=animator!=null&&animator.enabled;if(animator!=null)animator.enabled=false;
            actor.GetComponent<OralMonsterMotion>()?.ResetForPool();
            actor.GetComponent<MonsterNeedleBarrier>()?.ResetBarrier();actor.GetComponent<OralTartarArmor>()?.ResetForPool();
            foreach(var r in actor.GetComponentsInChildren<SpriteRenderer>(true))
                if(r!=body&&r.name.ToLowerInvariant().Contains("shadow"))
                {shadows.Add(new ShadowState{Renderer=r,Active=r.gameObject.activeSelf});r.gameObject.SetActive(false);}
            parts=new GameObject("입천장 박쥐 · 펼침 파츠").transform;parts.SetParent(actor.transform,false);
            hanging=OralEventArt.Sprite(parts,"M02 · 거꾸로 매달린 박쥐",OralCeilingBatArt.Hanging);
            leftWing=OralEventArt.Sprite(body.transform,"M01 · 왼쪽 날개",OralCeilingBatArt.LeftWing);
            rightWing=OralEventArt.Sprite(body.transform,"M01 · 오른쪽 날개",OralCeilingBatArt.RightWing);
            body.sprite=OralCeilingBatArt.Body;body.flipX=body.flipY=false;
            actor.SetFieldRuntimeSuspended(true);ownsSuspension=true;
            Pose();
        }

        bool CanAdvance
        {
            get
            {
                var level=OralStageRuntime.Instance!=null?OralStageRuntime.Instance.Level:null;
                return field!=null&&field.OnCeiling&&level!=null&&!level.IsLevelEnded&&level.PlayerCharacter!=null&&level.PlayerCharacter.IsAlive&&
                    !level.PlayerCharacter.IsChoosingAugment&&!level.PlayerCharacter.IsPortalTravelling&&Time.timeScale>0&&
                    !MiniStageRuntimeState.IsInsideMiniStage&&!StageEntryLoading.IsLoading;
            }
        }
        void LateUpdate()
        {
            if(!active||monster==null||body==null)return;
            foreach(var item in shadows)if(item.Renderer!=null)item.Renderer.gameObject.SetActive(false);
            if(!monster.IsAlive)
            {
                if(hanging!=null)hanging.enabled=false;
                SyncDeathWing(leftWing);SyncDeathWing(rightWing);
                return;
            }
            if(CanAdvance)
            {
                if(field.CeilingPhase==2)ArrivalAge+=Time.deltaTime;
                phase+=Time.deltaTime*Mathf.PI*2/FlapPeriod;
                Stage=ArrivalAge<ArrivalDelay?BatStage.Hanging:
                    ArrivalAge<ArrivalDelay+UnfoldSeconds?BatStage.Unfolding:
                    ArrivalAge<ArrivalDelay+UnfoldSeconds+TurnSeconds?BatStage.Turning:BatStage.Flying;
                if(Stage==BatStage.Flying&&ownsSuspension){ownsSuspension=false;monster.SetFieldRuntimeSuspended(false);}
            }
            Pose();
        }
        void Pose()
        {
            if(body==null||hanging==null)return;
            bool folded=Stage==BatStage.Hanging;
            float spread=folded?0:Mathf.SmoothStep(0,1,Mathf.Clamp01((ArrivalAge-ArrivalDelay)/UnfoldSeconds));
            float turn=Stage==BatStage.Turning?Mathf.SmoothStep(0,1,Mathf.Clamp01((ArrivalAge-ArrivalDelay-UnfoldSeconds)/TurnSeconds)):Stage==BatStage.Flying?1:0;
            float bob=Stage==BatStage.Flying?Mathf.Sin(phase)*.025f:Mathf.Sin(phase*.25f)*.008f;
            body.transform.localPosition=origin+Vector3.up*bob;body.transform.localScale=scale;
            // The delivered body cell is inverted, whereas the two wing cells are upright.
            // Correct the body alone by 180 degrees; child wings undo that correction.
            FlightOrientationDegrees=Mathf.Lerp(180,0,turn);
            body.transform.localRotation=rotation*Quaternion.Euler(0,0,FlightOrientationDegrees+180);
            body.flipY=false;if(Stage!=BatStage.Flying)body.flipX=false;
            var tint=body.color;tint.a=originalColor.a*spread;body.color=tint;
            hanging.enabled=body.enabled&&spread<1;
            hanging.forceRenderingOff=body.forceRenderingOff;hanging.sharedMaterial=body.sharedMaterial;
            hanging.color=new Color(tint.r,tint.g,tint.b,originalColor.a*(1-spread));
            hanging.sortingLayerID=body.sortingLayerID;hanging.sortingOrder=body.sortingOrder;
            hanging.transform.position=body.transform.position;hanging.transform.rotation=body.transform.parent!=null?body.transform.parent.rotation*rotation:rotation;
            var worldScale=body.transform.lossyScale;var parentScale=parts.lossyScale;
            hanging.transform.localScale=new Vector3(worldScale.x/Mathf.Max(.0001f,Mathf.Abs(parentScale.x)),worldScale.y/Mathf.Max(.0001f,Mathf.Abs(parentScale.y)),1);
            float flap=Stage==BatStage.Flying?Mathf.Sin(phase):0;
            var size=OralCeilingBatArt.Body.bounds.size;
            PoseWing(leftWing,new Vector3(-size.x*.36f,size.y*.07f),-1,spread,flap,tint);
            PoseWing(rightWing,new Vector3(size.x*.36f,size.y*.07f),1,spread,flap,tint);
            // The wings are decorative; target anatomy remains the readable central body.
            monster.GetComponent<MonsterBodyFit>()?.Refresh();
            if(monster.CenterTransform!=null&&monster.BodyHitbox!=null)
                monster.CenterTransform.position=monster.BodyHitbox.transform.TransformPoint(monster.BodyHitbox.offset);
        }
        void PoseWing(SpriteRenderer wing,Vector3 offset,float side,float spread,float flap,Color tint)
        {
            if(wing==null)return;
            wing.enabled=body.enabled&&spread>0;wing.forceRenderingOff=body.forceRenderingOff;
            wing.sharedMaterial=body.sharedMaterial;wing.color=tint;wing.sortingLayerID=body.sortingLayerID;wing.sortingOrder=body.sortingOrder-1;
            wing.transform.localPosition=-offset;
            wing.transform.localRotation=Quaternion.Euler(0,0,180+side*(Mathf.Lerp(76,0,spread)+flap*17));
            wing.transform.localScale=new Vector3(Mathf.Lerp(.12f,1,spread),1-Mathf.Abs(flap)*.10f,1);
        }
        void SyncDeathWing(SpriteRenderer wing)
        {
            if(wing==null)return;
            wing.enabled=body.enabled;wing.forceRenderingOff=body.forceRenderingOff;
            wing.sharedMaterial=body.sharedMaterial;wing.color=body.color;
        }
        public void ResetForPool()
        {
            if(!active)return;active=false;
            if(body!=null){body.transform.localPosition=origin;body.transform.localScale=scale;body.transform.localRotation=rotation;body.color=originalColor;body.sprite=originalSprite;body.flipX=originalFlipX;body.flipY=originalFlipY;}
            if(animator!=null)animator.enabled=animatorEnabled;
            foreach(var item in shadows)if(item.Renderer!=null)item.Renderer.gameObject.SetActive(item.Active);shadows.Clear();
            if(leftWing!=null){leftWing.gameObject.SetActive(false);Destroy(leftWing.gameObject);}
            if(rightWing!=null){rightWing.gameObject.SetActive(false);Destroy(rightWing.gameObject);}
            if(parts!=null){parts.gameObject.SetActive(false);Destroy(parts.gameObject);}parts=null;leftWing=rightWing=hanging=null;
            if(ownsSuspension&&monster!=null&&monster.IsAlive&&!MiniStageRuntimeState.IsInsideMiniStage&&!StageEntryLoading.IsLoading)monster.SetFieldRuntimeSuspended(false);
            ownsSuspension=false;field=null;monster=null;body=null;animator=null;
        }
        void OnDisable(){ResetForPool();}
        void OnDestroy(){ResetForPool();}
    }

    /// <summary>Four transparent cells: inverted folded bat, upright body, left/right wings.</summary>
    static class OralCeilingBatArt
    {
        public static Sprite Hanging {get;private set;}
        public static Sprite Body {get;private set;}
        public static Sprite LeftWing {get;private set;}
        public static Sprite RightWing {get;private set;}
        public static bool Load()
        {
            if(Body!=null&&Hanging!=null&&LeftWing!=null&&RightWing!=null)return true;
            var atlas=Resources.Load<Texture2D>("OralMap/CeilingBats/Atlas");if(atlas==null)return false;
            // Read once on first use, rather than allocating the full atlas four times.
            var pixels=atlas.isReadable?atlas.GetPixels32():null;
            Hanging=Slice(atlas,pixels,0,new Vector2(.5f,.5f),.54f,false);
            Body=Slice(atlas,pixels,1,new Vector2(.5f,.5f),.50f,false);
            LeftWing=Slice(atlas,pixels,2,new Vector2(.98f,.52f),.52f,true);
            RightWing=Slice(atlas,pixels,3,new Vector2(.02f,.52f),.52f,true);
            return Body!=null&&Hanging!=null&&LeftWing!=null&&RightWing!=null;
        }
        static Sprite Slice(Texture2D atlas,Color32[] pixels,int index,Vector2 pivot,float size,bool width)
        {
            int w=atlas.width/2,h=atlas.height/2,x0=index%2*w,y0=(1-index/2)*h;
            int l=x0,r=x0+w-1,b=y0,t=y0+h-1;
            if(pixels!=null)
            {
                l=x0+w;r=x0;b=y0+h;t=y0;
                for(int y=y0;y<y0+h;y++)for(int x=x0;x<x0+w;x++)if(pixels[y*atlas.width+x].a>32)
                {l=Mathf.Min(l,x);r=Mathf.Max(r,x);b=Mathf.Min(b,y);t=Mathf.Max(t,y);}
                if(r<=l||t<=b)return null;
            }
            var rect=new Rect(l,b,r-l+1,t-b+1);
            return Sprite.Create(atlas,rect,pivot,(width?rect.width:rect.height)/size,0,SpriteMeshType.FullRect);
        }
    }
}
