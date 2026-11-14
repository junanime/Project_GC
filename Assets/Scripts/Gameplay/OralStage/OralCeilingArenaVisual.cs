using UnityEngine;
namespace Vampire
{
    // A separate surface and camera composition keep the small ceiling room readable.
    [DisallowMultipleComponent,DefaultExecutionOrder(21000)]
    public sealed class OralCeilingArenaVisual:MonoBehaviour
    {
        OralStageRuntime runtime;OralFieldEvents events;Transform visual;
        SpriteRenderer floor,dark;Sprite floorSprite,darkSprite;Material floorMaterial;
        Camera view;bool cameraOwned;float originalSize;Vector3 originalLocal,followOffset;
        public float Blend {get;private set;}
        public bool Visible=>visual!=null&&visual.gameObject.activeSelf;
        public void Configure(OralStageRuntime owner)
        {
            if(runtime!=null)return;runtime=owner;events=owner.GetComponent<OralFieldEvents>();
            visual=new GameObject("C02 · 작은 입천장").transform;visual.SetParent(transform,false);
            var texture=Resources.Load<Texture2D>("OralMap/PalateCompact");
            if(texture==null){visual.gameObject.SetActive(false);return;}
            floorSprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Vector2.one*.5f,texture.width,0,SpriteMeshType.FullRect);
            darkSprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),Vector2.one*.5f,Texture2D.whiteTexture.width);
            dark=Make("입천장 외곽 암부",darkSprite,-610);
            dark.transform.localScale=Vector3.one*250;
            floor=Make("밝은 입천장 · 얕은 가로결",floorSprite,-609);
            floorMaterial=new Material(Resources.Load<Shader>("OralMap/PalateArena"));floor.sharedMaterial=floorMaterial;
            // The painted inner lip lies at approximately 13.5..86.5% of the atlas.
            floor.transform.localScale=new Vector3(OralCeilingArenaGeometry.HalfExtents.x*2/.73f,OralCeilingArenaGeometry.HalfExtents.y*2/.73f,1);
            visual.position=OralCeilingArenaGeometry.Center(owner.Map);visual.gameObject.SetActive(false);
        }
        SpriteRenderer Make(string label,Sprite sprite,int order)
        {
            var go=new GameObject(label);go.transform.SetParent(visual,false);
            var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;GroundVisualSorting.ApplyBackground(r,order);return r;
        }
        void LateUpdate()
        {
            if(runtime==null||runtime.Level==null||runtime.Map==null||visual==null||floor==null)return;
            bool active=runtime.Map.IsOral&&events!=null&&events.OnCeiling&&!MiniStageRuntimeState.IsInsideMiniStage;
            Blend=active?(events.CeilingPhase==2?1:events.CeilingTravel01):0;
            visual.gameObject.SetActive(active&&Blend>0);
            if(active)
            {
                if(!cameraOwned)TakeCamera();
                dark.color=new Color(.11f,.017f,.065f,Blend);floor.color=new Color(1,1,1,Blend);
                if(view!=null)
                {
                    var focus=(Vector3)OralCeilingArenaGeometry.Center(runtime.Map);focus.z=view.transform.position.z;
                    var follow=NormalCameraPosition();
                    view.transform.position=Vector3.Lerp(follow,focus,Blend);
                    float fit=Mathf.Max(8.9f,8.9f/Mathf.Max(.1f,view.aspect));
                    view.orthographicSize=Mathf.Lerp(originalSize,fit,Blend);
                }
            }
            else ReleaseCamera();
        }
        void TakeCamera()
        {
            view=Camera.main;if(view==null)return;cameraOwned=true;originalSize=view.orthographicSize;
            originalLocal=view.transform.localPosition;
            followOffset=runtime.Level.PlayerCharacter!=null?view.transform.position-runtime.Level.PlayerCharacter.transform.position:Vector3.zero;
        }
        Vector3 NormalCameraPosition()
        {
            if(view.transform.parent!=null)return view.transform.parent.TransformPoint(originalLocal);
            return runtime.Level.PlayerCharacter!=null?runtime.Level.PlayerCharacter.transform.position+followOffset:view.transform.position;
        }
        void ReleaseCamera()
        {
            if(!cameraOwned)return;cameraOwned=false;
            if(view!=null){view.orthographicSize=originalSize;view.transform.position=NormalCameraPosition();}view=null;
        }
        void OnDisable(){ReleaseCamera();if(visual!=null)visual.gameObject.SetActive(false);}
        void OnDestroy(){ReleaseCamera();if(visual!=null)Destroy(visual.gameObject);if(floorSprite!=null)Destroy(floorSprite);if(darkSprite!=null)Destroy(darkSprite);if(floorMaterial!=null)Destroy(floorMaterial);}
    }
}
