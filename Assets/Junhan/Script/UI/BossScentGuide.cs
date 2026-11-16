using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Screen-space scent only: no world collider, damage, force or marker occlusion.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BossScentGuide : MaskableGraphic
    {
        BuriedBossDirector director;
        LevelManager level;
        Canvas root;
        Camera view;
        Vector2 edge, inward, sideways, previousPlayer;
        float strength, clock;
        public float Strength => strength;
        public Vector2 SourceEdge => edge;

        public static BossScentGuide Ensure(BuriedBossDirector owner, LevelManager manager)
        {
            var prior = manager.GetComponentInChildren<BossScentGuide>(true);
            if (prior != null) { prior.director=owner; return prior; }
            var go = new GameObject("달콤한 향기 · 화면 가장자리", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(manager.transform,false);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=45;
            var child=new GameObject("옅은 노란 향기 바람",typeof(RectTransform));child.transform.SetParent(go.transform,false);
            var rect=(RectTransform)child.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var scent=child.AddComponent<BossScentGuide>();scent.root=canvas;scent.level=manager;scent.director=owner;
            scent.raycastTarget=false;scent.color=Color.white;
            return scent;
        }

        public static Vector2 ProjectEdge(Vector2 direction,Vector2 halfSize,float inset)
        {
            if(direction.sqrMagnitude<.0001f)return Vector2.zero;
            var safe=new Vector2(Mathf.Max(1,halfSize.x-inset),Mathf.Max(1,halfSize.y-inset));
            float scale=Mathf.Min(Mathf.Abs(direction.x)<.0001f?float.MaxValue:safe.x/Mathf.Abs(direction.x),
                Mathf.Abs(direction.y)<.0001f?float.MaxValue:safe.y/Mathf.Abs(direction.y));
            return direction*scale;
        }

        void LateUpdate()
        {
            if(level==null||director==null)return;
            if(view==null)view=Camera.main;
            bool available=view!=null && director.PreparedStage==1&&!director.Consumed&&director.Part!=null&&director.Part.isActiveAndEnabled&&
                !FinalBossSummonInteractable.IsSummoning&&!level.IsLevelEnded&&!level.IsRunFlowPaused&&
                !StageEntryLoading.IsLoading&&!MiniStageRuntimeState.IsInsideMiniStage&&Time.timeScale>0&&
                !TutorialGuide.IsOpen&&(ApothecaryUI.Instance==null||ApothecaryUI.Instance.Page=="hud");
            float wanted=0;
            if(available)
            {
                var target=director.Part.transform.position;
                var player=(Vector2)level.PlayerCharacter.transform.position;
                var viewport=view.WorldToViewportPoint(target);
                var delta=(Vector2)target-player;
                bool outside=viewport.z<0||viewport.x<.04f||viewport.x>.96f||viewport.y<.04f||viewport.y>.96f;
                if(outside&&delta.magnitude<55)
                {
                    rectTransform.anchorMin=view.rect.min;rectTransform.anchorMax=view.rect.max;
                    var direction=new Vector2((viewport.x-.5f)*rectTransform.rect.width,(viewport.y-.5f)*rectTransform.rect.height);
                    if(viewport.z<0)direction=-direction;
                    edge=ProjectEdge(direction,rectTransform.rect.size*.5f,1);
                    inward=-direction.normalized;sideways=new Vector2(-inward.y,inward.x);
                    float approaching=Vector2.Dot(player-previousPlayer,delta.normalized)>0.001f?1:0;
                    wanted=Mathf.Lerp(.48f,.75f,approaching)*Mathf.InverseLerp(55,18,delta.magnitude);
                }
                previousPlayer=player;
            }
            // Never draw across menus, loading, summoned bosses or a different stage.
            strength=available?Mathf.MoveTowards(strength,wanted,Time.unscaledDeltaTime*1.8f):0;
            if(strength>0)clock+=Time.deltaTime;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(strength<=.001f)return;
            float scale=Mathf.Min(rectTransform.rect.width/960f,rectTransform.rect.height/540f);
            bool reduced=GamePreferences.Current.reducedMotion;
            for(int j=0;j<9;j++)
            {
                float u=reduced?.35f:Mathf.Repeat(clock/3.5f+j*.163f,1);
                float alpha=Mathf.Sin(u*Mathf.PI)*strength;
                Vector2 prev=Vector2.zero;
                for(int k=0;k<=24;k++)
                {
                    float s=k/24f;
                    float depth=(-40+245*u-82*s)*scale;
                    Vector2 p=edge+inward*depth+sideways*((j-4)*12+14*Mathf.Sin(u*5+j*.6f+s*2.5f))*scale;
                    if(k>0)
                    {
                        float taper=Mathf.Sin(s*Mathf.PI);
                        Segment(vh,prev,p,5*scale,new Color(1,.83f,.34f,alpha*taper*.13f));
                        Segment(vh,prev,p,1.7f*scale,new Color(1,.94f,.61f,alpha*taper*.62f));
                    }
                    prev=p;
                }
            }
            for(int j=0;j<3;j++)
            {
                float u=reduced?.45f:Mathf.Repeat(clock/3.5f+j/3f,1);
                var center=edge+inward*((-20+205*u)*scale)+sideways*((j-1)*47*scale);
                Vector2 prev=Vector2.zero;
                for(int k=0;k<=30;k++)
                {
                    float q=k/30f,a=q*Mathf.PI*2.05f,r=(18*(1-q)+2)*scale;
                    var p=center+inward*(Mathf.Cos(a)*r)+sideways*(Mathf.Sin(a)*r*.64f);
                    if(k>0)Segment(vh,prev,p,1.4f*scale,new Color(1,.93f,.53f,strength*Mathf.Sin(u*Mathf.PI)*Mathf.Sin(q*Mathf.PI)*.52f));
                    prev=p;
                }
            }
        }

        void Segment(VertexHelper vh,Vector2 a,Vector2 b,float width,Color tint)
        {
            // Clip to this camera's viewport, including non-16:9 letterboxed displays.
            var bounds=rectTransform.rect;
            if(!bounds.Contains(a)||!bounds.Contains(b))return;
            var d=b-a;if(d.sqrMagnitude<.00001f)return;
            var n=new Vector2(-d.y,d.x).normalized*width*.5f;
            int start=vh.currentVertCount;
            vh.AddVert(a-n,tint,Vector2.zero);vh.AddVert(a+n,tint,Vector2.zero);
            vh.AddVert(b+n,tint,Vector2.zero);vh.AddVert(b-n,tint,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
        }
    }
}
