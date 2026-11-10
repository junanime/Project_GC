using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Lift rendered anatomy, never the world foot or its navigation position.
    // Suspension removes physical/contact attacks while the player passes underneath.
    [DefaultExecutionOrder(19000)]
    public sealed class OralHiccupLift : MonoBehaviour
    {
        sealed class Part { public SpriteRenderer Source, Copy; public bool Hidden; }
        readonly List<Part> parts=new List<Part>();
        Monster monster;OralEventAdditions owner;Transform visualRoot;SpriteRenderer footprint;
        bool suspendedBefore,active;float height;
        public float Height=>height;
        public SpriteRenderer Footprint=>footprint;
        public bool Active=>active;
        public void Begin(OralEventAdditions events,Monster target)
        {
            Restore();owner=events;monster=target;suspendedBefore=target.IsFieldRuntimeSuspended;active=true;
            visualRoot=new GameObject("딸꾹질 · 공중 몬스터").transform;visualRoot.SetParent(events.transform,false);
            foreach(var r in target.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var p=new Part{Source=r,Hidden=r.forceRenderingOff};parts.Add(p);
                if(!r.name.ToLowerInvariant().Contains("shadow"))p.Copy=OralEventArt.Sprite(visualRoot,r.name+" · 공중",r.sprite);
                r.forceRenderingOff=true;
            }
            footprint=OralEventArt.Sprite(visualRoot,"착지 위치 · 바닥 그림자",OralEventArt.Disc);
            GroundVisualSorting.Apply(footprint,8);monster.SetFieldRuntimeSuspended(true);
        }
        public void Sample(float worldHeight){height=worldHeight;Refresh();}
        void LateUpdate(){Refresh();}
        void Refresh()
        {
            if(!active)return;
            if(monster==null||!monster.gameObject.activeInHierarchy||monster.HP<=0||owner==null){Restore();return;}
            bool visible=!MiniStageRuntimeState.IsInsideMiniStage&&!StageEntryLoading.IsLoading;
            visualRoot.gameObject.SetActive(visible);
            if(!visible)return;
            monster.SetFieldRuntimeSuspended(true);
            foreach(var p in parts)
            {
                if(p.Source==null)continue;p.Source.forceRenderingOff=true;
                if(p.Copy==null)continue;
                p.Copy.enabled=p.Source.enabled&&p.Source.gameObject.activeInHierarchy&&!p.Hidden;
                p.Copy.sprite=p.Source.sprite;p.Copy.sharedMaterial=p.Source.sharedMaterial;p.Copy.color=p.Source.color;
                p.Copy.flipX=p.Source.flipX;p.Copy.flipY=p.Source.flipY;
                p.Copy.sortingLayerID=p.Source.sortingLayerID;p.Copy.sortingOrder=p.Source.sortingOrder+35;
                p.Copy.transform.position=p.Source.transform.position+Vector3.up*height;
                p.Copy.transform.rotation=p.Source.transform.rotation;p.Copy.transform.localScale=p.Source.transform.lossyScale;
            }
            footprint.transform.position=monster.Position;
            float width=Mathf.Max(.25f,monster.BodyRenderer!=null?monster.BodyRenderer.bounds.size.x:.6f);
            float factor=Mathf.Lerp(1,.65f,Mathf.Clamp01(height/Mathf.Max(.1f,owner.LiftHeight)));
            footprint.transform.localScale=new Vector3(width*factor,width*.32f*factor,1);footprint.color=new Color(.16f,.03f,.1f,.3f);
        }
        public void Restore()
        {
            if(!active)return;active=false;height=0;
            foreach(var p in parts)if(p.Source!=null)p.Source.forceRenderingOff=p.Hidden;parts.Clear();
            if(monster!=null&&!suspendedBefore&&!MiniStageRuntimeState.IsInsideMiniStage&&!(owner!=null&&owner.Level!=null&&(owner.Level.GetComponent<StageProgression>()?.Travelling??false)))monster.SetFieldRuntimeSuspended(false);
            if(visualRoot!=null){visualRoot.gameObject.SetActive(false);Destroy(visualRoot.gameObject);}visualRoot=null;footprint=null;
        }
        void OnDisable(){Restore();}
        void OnDestroy(){Restore();}
    }
}
