using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // No SpriteRenderer/world transform: camera movement, tilt and shake cannot move this panel.
    public sealed class ChameleonPortrait : MonoBehaviour
    {
        public const float ClapLead = .8f;
        Canvas canvas;RectTransform safe, panel;Image face;CanvasGroup group;
        ChameleonKind kind;bool left, faceOnly;float age;
        public bool Left => left;
        public RectTransform Panel => panel;
        public Vector3 MouthWorld
        {
            get
            {
                var cam=Camera.main;if(cam==null)return Vector3.zero;
                Vector3 p=panel.TransformPoint(new Vector3(left?panel.rect.xMax-36:panel.rect.xMin+36, panel.rect.center.y,0));
                Vector2 screen=RectTransformUtility.WorldToScreenPoint(null,p);
                var world=cam.ScreenToWorldPoint(new Vector3(screen.x,screen.y,-cam.transform.position.z));world.z=0;return world;
            }
        }
        public static ChameleonPortrait Create(Transform owner,ChameleonKind kind,bool? fromLeft=null,bool faceOnly=false)
        {
            var go=new GameObject("Chameleon event panel",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(CanvasGroup));
            go.transform.SetParent(owner,false);
            var v=go.AddComponent<ChameleonPortrait>();v.kind=kind;v.left=fromLeft??Random.value<.5f;v.faceOnly=faceOnly;
            v.canvas=go.GetComponent<Canvas>();v.canvas.renderMode=RenderMode.ScreenSpaceOverlay;v.canvas.sortingOrder=145;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            v.group=go.GetComponent<CanvasGroup>();v.group.blocksRaycasts=false;v.group.interactable=false;
            v.safe=AugmentPanelTheme.Rect("Safe screen",go.transform);AugmentPanelTheme.Anchors(v.safe,Vector2.zero,Vector2.one);
            v.panel=AugmentPanelTheme.Rect("Fixed side panel",v.safe);v.panel.anchorMin=v.panel.anchorMax=new Vector2(v.left?0:1,.53f);v.panel.pivot=new Vector2(v.left?0:1,.5f);v.panel.anchoredPosition=new Vector2(v.left?8:-8,0);v.panel.sizeDelta=new Vector2(260,170);
            var bg=v.panel.gameObject.AddComponent<Image>();bg.raycastTarget=false;bg.color=new Color(.11f,.10f,.15f,.78f);
            for(int edge=0;edge<4;edge++)
            {
                var line=AugmentPanelTheme.Rect("Panel edge",v.panel);bool vertical=edge<2;float side=edge%2;
                line.anchorMin=vertical?new Vector2(side,0):new Vector2(0,side);line.anchorMax=vertical?new Vector2(side,1):new Vector2(1,side);line.sizeDelta=vertical?new Vector2(2,0):new Vector2(0,2);
                var border=line.gameObject.AddComponent<Image>();border.color=ChameleonArt.Accent(kind);border.raycastTarget=false;
            }
            var crop=AugmentPanelTheme.Rect("Upper body crop",v.panel);AugmentPanelTheme.Anchors(crop,new Vector2(.02f,.12f),new Vector2(.98f,.99f));crop.gameObject.AddComponent<RectMask2D>();
            var r=AugmentPanelTheme.Rect("Animated portrait",crop);AugmentPanelTheme.Anchors(r,Vector2.zero,Vector2.one);
            v.face=r.gameObject.AddComponent<Image>();v.face.raycastTarget=false;v.face.preserveAspect=true;r.localScale=new Vector3(v.left?1:-1,1,1);
            var label=AugmentPanelTheme.Label("Name",v.panel,TrainingUITheme.Font,ChameleonArt.Names[(int)kind].Split('·')[0].Trim(),26);label.raycastTarget=false;
            AugmentPanelTheme.Anchors(label.rectTransform,new Vector2(.03f,.01f),new Vector2(.97f,.12f));
            v.Sample(0);v.Update();return v;
        }
        public void Sample(int frame){if(face!=null)face.sprite=ChameleonArt.Portrait(kind,frame,faceOnly);}
        void Update()
        {
            if(canvas==null)return;
            canvas.enabled=!ChameleonTime.Paused;
            var a=Screen.safeArea;safe.anchorMin=a.min/new Vector2(Screen.width,Screen.height);safe.anchorMax=a.max/new Vector2(Screen.width,Screen.height);
            if(!ChameleonTime.Paused)age+=Time.deltaTime;
            group.alpha=Mathf.Clamp01(age/.15f);
        }
        public static IEnumerator Clap(Transform owner)
        {
            var p=Create(owner,ChameleonKind.Drift);
            try {p.Sample(10);yield return ChameleonTime.Wait(ClapLead*.65f);p.Sample(11);GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.PeristalsisTilt);yield return ChameleonTime.Wait(ClapLead*.35f);}
            finally {if(p!=null)Destroy(p.gameObject);}
        }
        public static IEnumerator Foam(Transform owner,float duration)
        {
            var p=Create(owner,ChameleonKind.Foam);
            try{for(int i=8;i<=12;i++){p.Sample(i);yield return ChameleonTime.Wait(Mathf.Max(.1f,duration)/5);}}
            finally{if(p!=null)Destroy(p.gameObject);}
        }
        public static IEnumerator Coffee(Transform owner)
        {
            var p=Create(owner,ChameleonKind.Latte);p.canvas.sortingOrder=32751;ChameleonCoffeeWash wash=null;
            try
            {
                p.Sample(6);yield return ChameleonTime.Wait(.45f);p.Sample(7);
                wash=ChameleonCoffeeWash.Play(owner,p.Left);GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.CoffeeTransfusionPour);
                yield return ChameleonTime.Wait(ChameleonCoffeeWash.Duration);p.Sample(12);
            }
            finally{if(wash!=null)Destroy(wash.transform.parent.gameObject);if(p!=null)Destroy(p.gameObject);}
        }
    }
    public sealed class ChameleonCoffeeWash : MaskableGraphic
    {
        public const float Duration=2.4f;
        float age;bool left;Canvas overlayCanvas;
        public static ChameleonCoffeeWash Play(Transform owner,bool left)
        {
            var root=new GameObject("Coffee breath screen wash",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(owner,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32750;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
            var rect=AugmentPanelTheme.Rect("Latte flowing from side",root.transform);AugmentPanelTheme.Anchors(rect,Vector2.zero,Vector2.one);rect.gameObject.AddComponent<CanvasRenderer>();
            var v=rect.gameObject.AddComponent<ChameleonCoffeeWash>();v.left=left;v.overlayCanvas=canvas;v.raycastTarget=false;return v;
        }
        void Update(){overlayCanvas.enabled=!ChameleonTime.Paused;if(ChameleonTime.Paused)return;age+=Time.deltaTime;SetVerticesDirty();if(age>=Duration)Destroy(transform.parent.gameObject);}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float fill=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.85f));float drain=Mathf.Clamp01((age-1.05f)/1.35f);
            for(int i=0;i<64;i++)
            {
                float y0=r.yMin+r.height*i/64,y1=r.yMin+r.height*(i+1)/64;
                float nearCenter=1-Mathf.Abs((i+.5f)/64-.54f)*2;
                float edge=Mathf.Clamp01(fill*1.45f-.4f*(1-nearCenter)+Mathf.Sin(i*.5f+age*12)*.025f);
                float trail=Mathf.Clamp01(drain*1.3f+Mathf.Sin(i*.28f-age*8)*.045f);
                if(edge<=trail)continue;
                float x0=left?r.xMin+trail*r.width:r.xMax-edge*r.width,x1=left?r.xMin+edge*r.width:r.xMax-trail*r.width;
                var c=Color.Lerp(new Color(.68f,.43f,.21f),new Color(.91f,.71f,.43f),.5f+.5f*Mathf.Sin(i*.23f+age*5));
                Quad(vh,x0,y0,x1,y1,c);
                float foam=12+Mathf.Sin(i*.7f)*5;Quad(vh,left?x1-foam:x0,y0,left?x1:x0+foam,y1,new Color(1,.93f,.77f));
            }
        }
        static void Quad(VertexHelper v,float x0,float y0,float x1,float y1,Color c){int n=v.currentVertCount;v.AddVert(new Vector2(x0,y0),c,Vector2.zero);v.AddVert(new Vector2(x1,y0),c,Vector2.zero);v.AddVert(new Vector2(x1,y1),c,Vector2.zero);v.AddVert(new Vector2(x0,y1),c,Vector2.zero);v.AddTriangle(n,n+1,n+2);v.AddTriangle(n,n+2,n+3);}
    }
}
