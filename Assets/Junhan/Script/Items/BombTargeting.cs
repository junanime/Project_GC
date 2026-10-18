using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Vampire
{
    // Runs before weapon input so a bomb confirmation never also redirects the next needle.
    [DefaultExecutionOrder(-100)]
    public sealed class BombTargeting : MonoBehaviour
    {
        public const float Radius=.82f*4/2;
        static BombTargeting active;
        static int consumedFrame=-1;
        public static bool BlocksPointerAim => (active!=null&&active.Aiming)||consumedFrame==Time.frameCount;
        public bool Aiming => slot!=null;
        public SpriteRenderer Preview => preview;
        InventorySlot slot;
        Inventory inventory;
        LevelManager level;
        SpriteRenderer preview,cursor;
        int armedFrame,touchId=-1;
        Vector2 target;
        bool oldCursorVisible,cursorCaptured;
        readonly List<RaycastResult> hits=new List<RaycastResult>();
        bool CanUse => level!=null&&level.PlayerCharacter!=null&&level.PlayerCharacter.IsAlive&&!level.IsLevelEnded&&!level.IsRunFlowPaused&&Time.timeScale>0&&
            (ApothecaryUI.Instance==null||ApothecaryUI.Instance.Page=="hud");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState(){active=null;consumedFrame=-1;}
        void Awake(){inventory=GetComponent<Inventory>();level=FindObjectOfType<LevelManager>();}
        public static BombTargeting For(InventorySlot slot)=>slot.GetComponentInParent<Inventory>()?.GetComponent<BombTargeting>();
        public bool BeginAim(InventorySlot source)
        {
            if(!CanUse||source.Count==0||!source.IsBomb)return false;
            if(Aiming){Cancel();return false;}
            active=this;slot=source;armedFrame=Time.frameCount;touchId=-1;
            target=level.PlayerCharacter.transform.position;
            if(Mouse.current!=null&&!GamePlatform.UsesTouchControls)ScreenTarget(Mouse.current.position.ReadValue(),out target);
            preview=MakeRenderer("Bomb blast preview",BombCircleArt.Sprite,Radius*2,30000);
            preview.color=new Color(1,0,0,.27f);
            var border=MakeRenderer("Dark red blast boundary",BombCircleArt.Border,Radius*2,30001);
            border.color=new Color(.62f,.015f,.025f,.95f);border.transform.SetParent(preview.transform,true);
            cursor=MakeRenderer("Bomb aiming cursor",source.IconImage.sprite,.4f,30002);
            oldCursorVisible=Cursor.visible;cursorCaptured=true;
            if(!GamePlatform.UsesTouchControls)Cursor.visible=false;
            UpdatePreview();return true;
        }
        SpriteRenderer MakeRenderer(string name,Sprite sprite,float size,int order)
        {
            var r=new GameObject(name).AddComponent<SpriteRenderer>();r.sprite=sprite;
            r.sortingLayerID=SortingLayer.layers.OrderBy(l=>l.value).Last().id;r.sortingOrder=order;
            r.transform.localScale=Vector3.one*(size/Mathf.Max(.01f,sprite.bounds.size.x));return r;
        }
        public void Cancel()
        {
            slot=null;touchId=-1;
            if(preview!=null){preview.enabled=false;Destroy(preview.gameObject);}
            if(cursor!=null){cursor.enabled=false;Destroy(cursor.gameObject);}
            if(cursorCaptured){Cursor.visible=oldCursorVisible;cursorCaptured=false;}
            if(active==this)active=null;
        }
        public bool ThrowAutomatic(InventorySlot source)
        {
            if(!CanUse||source.Count==0)return false;
            Cancel();return source.ThrowBomb(AutomaticTarget(level.EntityManager,level.PlayerCharacter,Camera.main));
        }
        public bool Confirm(Vector2 position)
        {
            if(!Aiming||!CanUse)return false;
            var source=slot;consumedFrame=Time.frameCount;Cancel();return source.ThrowBomb(position);
        }
        void Update()
        {
            if(!CanUse){if(Aiming)Cancel();return;}
            if(GameInput.GetKeyDown(KeyCode.Alpha1)||GameInput.GetKeyDown(KeyCode.Z))
            {
                var bomb=inventory.Slots.FirstOrDefault(s=>s.IsBomb);if(bomb!=null)ThrowAutomatic(bomb);return;
            }
            if(!Aiming)return;
            if(slot.Count==0||GameInput.GetKeyDown(KeyCode.Escape)||Mouse.current?.rightButton.wasPressedThisFrame==true){Cancel();return;}
            if(Time.frameCount<=armedFrame)return;
            bool hasTouch=false;
            if(Touchscreen.current!=null)foreach(var touch in Touchscreen.current.touches)
            {
                hasTouch|=touch.press.isPressed||touch.press.wasReleasedThisFrame;
                if(touch.press.wasPressedThisFrame&&touchId<0&&!OverUI(touch.position.ReadValue()))touchId=touch.touchId.ReadValue();
                if(touchId<0||touch.touchId.ReadValue()!=touchId)continue;
                if(ScreenTarget(touch.position.ReadValue(),out var point))target=point;
                UpdatePreview();
                if(touch.phase.ReadValue()==UnityEngine.InputSystem.TouchPhase.Canceled){touchId=-1;continue;}
                if(touch.press.wasReleasedThisFrame)
                {
                    touchId=-1;if(!OverUI(touch.position.ReadValue())){Confirm(target);return;}
                }
            }
            if(!hasTouch&&Mouse.current!=null&&!GamePlatform.UsesTouchControls)
            {
                var position=Mouse.current.position.ReadValue();
                if(ScreenTarget(position,out var point))target=point;
                UpdatePreview();
                if(Mouse.current.leftButton.wasPressedThisFrame&&!OverUI(position)&&Camera.main.pixelRect.Contains(position)){Confirm(target);return;}
            }
        }
        void UpdatePreview(){if(preview!=null)preview.transform.position=target;if(cursor!=null)cursor.transform.position=target;}
        bool ScreenTarget(Vector2 screen,out Vector2 point)
        {
            point=target;var cam=Camera.main;if(cam==null||!cam.pixelRect.Contains(screen))return false;
            var plane=new Plane(Vector3.forward,level.PlayerCharacter.transform.position);var ray=cam.ScreenPointToRay(screen);
            if(!plane.Raycast(ray,out var distance))return false;point=ray.GetPoint(distance);return true;
        }
        bool OverUI(Vector2 position)
        {
            if(EventSystem.current==null)return false;hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},hits);
            return hits.Any(h=>h.module is GraphicRaycaster);
        }
        void OnApplicationFocus(bool focus){if(!focus)Cancel();}
        void OnDisable()=>Cancel();
        void OnDestroy()=>Cancel();
        public static Vector2 AutomaticTarget(EntityManager entities,Character player,Camera camera)
        {
            Vector2 origin=player!=null?(Vector2)player.transform.position:Vector2.zero;
            if(entities==null||camera==null)return origin;
            var points=new List<Vector2>();
            foreach(var monster in entities.LivingMonsters)
            {
                if(!NeedleTargeting.IsValid(monster,~0))continue;
                var point=NeedleTargeting.Position(monster);var v=camera.WorldToViewportPoint(point);
                if(v.z>0&&v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1)points.Add(point);
            }
            return DensestCenter(points,origin,Radius);
        }
        // Spatial buckets bound the search to nearby enemies, even late in a crowded run.
        public static Vector2 DensestCenter(IReadOnlyList<Vector2> points,Vector2 fallback,float radius)
        {
            var buckets=new Dictionary<Vector2Int,List<Vector2>>();
            foreach(var p in points){var key=Cell(p,radius);if(!buckets.TryGetValue(key,out var list))buckets[key]=list=new List<Vector2>();list.Add(p);}
            Vector2 best=fallback;int bestCount=0;float bestDistance=float.PositiveInfinity,bestSpread=float.PositiveInfinity;
            foreach(var seed in points)
            for(int start=0;start<2;start++)
            {
                Vector2 center=seed;
                // A cluster can fit inside one blast while its edge enemies are farther than one radius apart.
                // Also probe their wider centroid rather than treating each edge as an isolated group.
                if(start==1)
                {
                    int wideCount=0;Vector2 wideSum=Vector2.zero;var key=Cell(seed,radius);
                    for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)if(buckets.TryGetValue(key+new Vector2Int(x,y),out var near))
                        foreach(var p in near)if((p-seed).sqrMagnitude<=4*radius*radius){wideSum+=p;wideCount++;}
                    if(wideCount>0)center=wideSum/wideCount;
                }
                for(int pass=0;pass<3;pass++)
                {
                    int count=0;float spread=0;Vector2 sum=Vector2.zero;var cell=Cell(center,radius);
                    for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)if(buckets.TryGetValue(cell+new Vector2Int(x,y),out var group))
                        foreach(var p in group)if((p-center).sqrMagnitude<=radius*radius){count++;sum+=p;spread+=(p-center).sqrMagnitude;}
                    float distance=(center-fallback).sqrMagnitude;
                    if(count>bestCount||(count==bestCount&&(spread<bestSpread-.0001f||(Mathf.Abs(spread-bestSpread)<.0001f&&distance<bestDistance))))
                    {bestCount=count;best=center;bestDistance=distance;bestSpread=spread;}
                    if(count==0)break;center=sum/count;
                }
            }
            return best;
        }
        static Vector2Int Cell(Vector2 p,float size)=>new Vector2Int(Mathf.FloorToInt(p.x/size),Mathf.FloorToInt(p.y/size));
    }
    public static class BombCircleArt
    {
        static Sprite sprite,border;
        public static Sprite Border => border!=null?border:(border=Create(true));
        public static Sprite Sprite
        {
            get
            {
                if(sprite!=null)return sprite;
                sprite=Create(false);return sprite;
            }
        }
        static Sprite Create(bool rim)
        {
                const int size=128;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,name=rim?"Bomb radius boundary":"Plain bomb radius circle"};
                var colors=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float inset=size*.5f-Vector2.Distance(new Vector2(x+.5f,y+.5f),Vector2.one*(size*.5f));
                    colors[y*size+x]=new Color(1,1,1,Mathf.Clamp01(inset)*(rim?1-Mathf.Clamp01(inset-2.5f):1));
                }
                texture.SetPixels(colors);texture.Apply();return UnityEngine.Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,size);
        }
    }
}
