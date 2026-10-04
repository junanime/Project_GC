using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Screen-sized meters follow stable idle-art landmarks, not animation bounds.
    public sealed class PlayerCombatBars : MonoBehaviour
    {
        public const float Width=76, Height=12, RowGap=3, DividerWidth=3;
        sealed class Meter
        {
            public RectTransform root, track;
            public Image icon;
            public readonly List<RectTransform> fills=new List<RectTransform>();
            public readonly List<GameObject> cells=new List<GameObject>();
            public Color color;
            public int segments;
        }
        Character owner;
        Canvas canvas;
        RectTransform surface;
        Transform body;
        Vector3 headLocal, feetLocal;
        Meter health, dash, active;
        RectTransform relicBarrier;
        static Sprite heart, arrows;
        static readonly Dictionary<Sprite,Bounds> visibleBounds=new Dictionary<Sprite,Bounds>();
        public int DashSegments => dash!=null ? dash.segments : 0;
        public RectTransform HealthRect => health?.root;
        public RectTransform DashRect => dash?.root;
        public RectTransform ActiveRect => active?.root;

        public void Bind(Character character,PointBar legacy)
        {
            owner=character;
            if(legacy!=null)
            {
                var group=legacy.GetComponent<CanvasGroup>();
                if(group==null)group=legacy.gameObject.AddComponent<CanvasGroup>();
                group.alpha=0;group.blocksRaycasts=false;
            }
            var go=new GameObject("Player combat meters",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=190;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            surface=(RectTransform)go.transform;
            var renderer=character.GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();
            body=renderer!=null?renderer.transform:character.transform;
            Bounds bounds=renderer!=null&&renderer.sprite!=null?renderer.sprite.bounds:new Bounds(Vector3.zero,Vector3.one*.65f);
            var idle=character.Blueprint?.idleSpriteSequence;
            if(idle!=null&&idle.Length>0&&idle[0]!=null)bounds=VisibleBounds(idle[0]);
            headLocal=character.transform.InverseTransformPoint(body.TransformPoint(new Vector3(bounds.center.x,bounds.max.y,0)));
            feetLocal=character.transform.InverseTransformPoint(body.TransformPoint(new Vector3(bounds.center.x,bounds.min.y,0)));
            if(heart==null)heart=PixelIcon(new[]{"0110110","1111111","1111111","0111110","0011100","0001000"});
            if(arrows==null)arrows=PixelIcon(new[]{"11001100","01100110","00110011","01100110","11001100"});
            health=Create("Health meter",new Color(.96f,.31f,.38f),heart);
            dash=Create("Dash meter",new Color(.15f,.85f,.82f),arrows);
            active=Create("Active meter",new Color(1,.78f,.24f),character.Blueprint?.skills?.activeIcon);
            SetSegments(health,1);SetSegments(active,1);
            relicBarrier=Box("Relic barrier",health.root,16,Height-3,Width-19,2,new Color(.3f,.8f,1));
        }
        static Bounds VisibleBounds(Sprite sprite)
        {
            if(visibleBounds.TryGetValue(sprite,out var result))return result;
            result=sprite.bounds;
            // Read one idle frame once; transparent canvas margins must not separate the meters from the feet.
            if(sprite.packed || SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return result;
            Rect rect=sprite.rect;int width=Mathf.RoundToInt(rect.width),height=Mathf.RoundToInt(rect.height);
            var old=RenderTexture.active;var rt=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
            var pixels=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try
            {
                var texture=sprite.texture;
                Graphics.Blit(texture,rt,new Vector2(rect.width/texture.width,rect.height/texture.height),new Vector2(rect.x/texture.width,rect.y/texture.height));
                RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,width,height),0,0,false);
                var data=pixels.GetPixels32();int left=width,right=-1,bottom=height,top=-1;
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(data[y*width+x].a>=128)
                {left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}
                if(right>=left)
                {
                    Vector2 min=(new Vector2(left,bottom)-sprite.pivot)/sprite.pixelsPerUnit;
                    Vector2 max=(new Vector2(right+1,top+1)-sprite.pivot)/sprite.pixelsPerUnit;
                    result=new Bounds((min+max)*.5f,max-min);
                }
            }
            finally {RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Destroy(pixels);}
            visibleBounds[sprite]=result;return result;
        }
        static Sprite PixelIcon(string[] pixels)
        {
            int h=pixels.Length,w=pixels[0].Length;
            var texture=new Texture2D(w,h,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)texture.SetPixel(x,h-1-y,pixels[y][x]=='1'?Color.white:Color.clear);
            texture.Apply();return Sprite.Create(texture,new Rect(0,0,w,h),new Vector2(.5f,.5f),100);
        }
        static RectTransform Box(string name,Transform parent,float x,float y,float w,float h,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));
            var rect=(RectTransform)go.transform;rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(w,h);
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return rect;
        }
        Meter Create(string name,Color color,Sprite icon)
        {
            var root=Box(name,surface,0,0,Width,Height,new Color(.05f,.045f,.06f,.98f));root.pivot=new Vector2(.5f,.5f);root.anchorMin=root.anchorMax=new Vector2(.5f,.5f);
            Box("Light edge",root,1,1,Width-2,Height-2,new Color(.76f,.72f,.63f));
            Box("Inset",root,2,2,Width-4,Height-4,new Color(.1f,.11f,.13f));
            Box("Icon divider",root,13,2,2,Height-4,new Color(.02f,.03f,.04f));
            var iconRect=Box("Icon",root,3,2,9,8,Color.white);var image=iconRect.GetComponent<Image>();image.sprite=icon;image.preserveAspect=true;
            var track=Box("Charge track",root,16,3,Width-19,Height-6,new Color(.18f,.19f,.21f));
            return new Meter{root=root,track=track,icon=image,color=color};
        }
        void SetSegments(Meter meter,int count)
        {
            count=Mathf.Max(1,count);if(meter.segments==count)return;
            foreach(var cell in meter.cells){cell.SetActive(false);Destroy(cell);}meter.cells.Clear();meter.fills.Clear();meter.segments=count;
            float width=meter.track.sizeDelta.x;
            // Keep separators visible for normal charge counts; also handle unusually large builds.
            float gap=Mathf.Min(DividerWidth,width/(count*3f));
            float cellWidth=(width-gap*(count-1))/count;
            for(int i=0;i<count;i++)
            {
                var cell=Box("Charge "+(i+1),meter.track,i*(cellWidth+gap),0,cellWidth,6,new Color(.09f,.1f,.12f));
                var fill=Box("Fill",cell,0,0,cellWidth,6,meter.color);fill.anchorMax=Vector2.one;fill.sizeDelta=Vector2.zero;
                meter.cells.Add(cell.gameObject);meter.fills.Add(fill);
            }
        }
        public static float SegmentFill(int index,int available,float progress)=>index<available?1f:index==available?Mathf.Clamp01(progress):0f;
        static void Fill(Meter meter,int index,float value)
        {meter.fills[index].anchorMax=new Vector2(Mathf.Clamp01(value),1);}
        void LateUpdate()
        {
            if(owner==null||canvas==null)return;
            var camera=Camera.main;var ui=ApothecaryUI.Instance;
            bool shown=owner.IsAlive&&camera!=null&&(ui==null||ui.Page=="hud")&&Time.timeScale>0;
            canvas.enabled=shown;if(!shown)return;
            SetSegments(dash,owner.MaxDashCharges);
            Fill(health,0,owner.MaxHealth>0?owner.CurrentHealth/owner.MaxHealth:0);
            float barrierMax=owner.RelicBonus(RelicBlueprint.RelicEffectType.Barrier);
            relicBarrier.gameObject.SetActive(barrierMax>0 && owner.Relics.State.barrier>0);
            relicBarrier.sizeDelta=new Vector2((Width-19)*(barrierMax>0?owner.Relics.State.barrier/barrierMax:0),2);
            for(int i=0;i<dash.segments;i++)Fill(dash,i,SegmentFill(i,owner.CurrentDashCharges,owner.DashRechargeProgress));
            var skill=owner.Skills;
            active.root.gameObject.SetActive(skill?.Definition!=null);
            if(skill?.Definition!=null)Fill(active,0,1-skill.CooldownRemaining/Mathf.Max(.01f,skill.EffectiveCooldown));
            Vector2 head,feet;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,camera.WorldToScreenPoint(owner.transform.TransformPoint(headLocal)),null,out head);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,camera.WorldToScreenPoint(owner.transform.TransformPoint(feetLocal)),null,out feet);
            health.root.anchoredPosition=head+Vector2.up*(Height*.5f+5);
            dash.root.anchoredPosition=feet-Vector2.up*(Height*.5f+6);
            active.root.anchoredPosition=dash.root.anchoredPosition-Vector2.up*(Height+RowGap);
        }
    }
}
