using UnityEngine;
namespace Vampire
{
    // A body-relative orbit. No body tint, steam or floor decal.
    public class CoffeeMonsterBuffRuntime : MonoBehaviour
    {
        private Character targetPlayer;
        private Rigidbody2D rb;
        private Monster monster;
        private SpriteRenderer source;
        private SpriteRenderer[] beans;
        private LineRenderer[] arcs;
        private GameObject orbitRoot;
        private Material orbitMaterial;
        private float expireTime, phase;
        private float extraMoveForce = 2.5f, maxAddedVelocity = 2f;
        private bool initialized;
        private static Sprite beanSprite;
        public void ApplyOrRefresh(Character targetPlayer, float duration, float extraMoveForce,
            float maxAddedVelocity, Color overlayColor)
        {
            this.targetPlayer = targetPlayer;
            this.extraMoveForce = Mathf.Max(0f, extraMoveForce);
            this.maxAddedVelocity = Mathf.Max(0.1f, maxAddedVelocity);
            expireTime = Time.time + Mathf.Max(.1f, duration);
            if (initialized) return;
            monster = GetComponent<Monster>(); rb = GetComponent<Rigidbody2D>();
            var animator = GetComponentInChildren<SpriteAnimator>(true);
            source = animator != null ? animator.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
            if (source == null) return;
            if (beanSprite == null) beanSprite = Resources.Load<Sprite>("CoffeeEvent/CoffeeBean");
            orbitRoot = new GameObject("Coffee bean orbit");
            orbitRoot.transform.SetParent(source.transform, false);
            orbitMaterial = new Material(Shader.Find("Sprites/Default"));
            beans = new SpriteRenderer[3]; arcs = new LineRenderer[2];
            for (int i=0;i<3;i++)
            {
                var obj = new GameObject("Orbit bean " + i);obj.transform.SetParent(orbitRoot.transform,false);
                beans[i] = obj.AddComponent<SpriteRenderer>();beans[i].sprite=beanSprite;
                beans[i].sharedMaterial=orbitMaterial;
            }
            for(int i=0;i<2;i++)
            {
                var obj=new GameObject(i==0?"Rear orbit":"Front orbit");obj.transform.SetParent(orbitRoot.transform,false);
                var line=obj.AddComponent<LineRenderer>();arcs[i]=line;line.sharedMaterial=orbitMaterial;
                line.useWorldSpace=true;line.positionCount=25;line.numCapVertices=2;
                line.startColor=line.endColor=new Color(.85f,.49f,.17f,.7f);
            }
            initialized=true;UpdateOrbit();
        }
        private bool Suspended => monster != null && (monster.IsFieldRuntimeSuspended ||
            (MiniStageRuntimeState.IsInsideMiniStage && !monster.IsMiniStageOwned));
        private void FixedUpdate()
        {
            if(!initialized)return;
            if(Time.time>=expireTime){RemoveBuffAndDestroy();return;}
            if(!Suspended)ApplyExtraMovementTowardPlayer();
        }
        private void LateUpdate()
        {
            if(!initialized||source==null)return;
            if(!Suspended)phase+=Time.deltaTime*2.6f;
            UpdateOrbit();
        }
        private void UpdateOrbit()
        {
            var bounds=source.bounds;Vector3 center=bounds.center;
            float rx=Mathf.Max(.2f,bounds.extents.x*1.35f),ry=Mathf.Max(.09f,bounds.size.y*.19f);
            for(int i=0;i<3;i++)
            {
                float a=phase+i*Mathf.PI*2/3,depth=Mathf.Sin(a);
                var bean=beans[i];bean.enabled=source.enabled;bean.transform.position=center+new Vector3(Mathf.Cos(a)*rx,depth*ry,0);
                bean.sortingLayerID=source.sortingLayerID;bean.sortingOrder=source.sortingOrder+(depth<0?2:-2);
                float size=Mathf.Max(.12f,bounds.size.x*.24f)*(1-depth*.12f);
                Vector3 ps=bean.transform.parent.lossyScale;
                bean.transform.localScale=new Vector3(size/Mathf.Max(.001f,Mathf.Abs(ps.x)),size/Mathf.Max(.001f,Mathf.Abs(ps.y)),1);
                bean.transform.rotation=Quaternion.Euler(0,0,-a*Mathf.Rad2Deg*.35f);
                var c=Color.white;c.a=source.color.a;bean.color=c;
            }
            for(int half=0;half<2;half++)
            {
                var line=arcs[half];line.enabled=source.enabled;
                line.sortingLayerID=source.sortingLayerID;line.sortingOrder=source.sortingOrder+(half==0?-1:1);
                line.startWidth=line.endWidth=Mathf.Max(.008f,bounds.size.x*.012f);
                for(int j=0;j<25;j++){float a=half*Mathf.PI+j*Mathf.PI/24;line.SetPosition(j,center+new Vector3(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry,0));}
            }
        }
        private void ApplyExtraMovementTowardPlayer()
        {
            if (rb == null || !rb.simulated || targetPlayer == null)
            {
                return;
            }

            Vector2 direction = ((Vector2)targetPlayer.transform.position - rb.position).normalized;

            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Vector2 addedVelocity = direction * extraMoveForce * Time.fixedDeltaTime;
            rb.velocity += addedVelocity;

            float projectedSpeed = Vector2.Dot(rb.velocity, direction);

            if (projectedSpeed > maxAddedVelocity)
            {
                Vector2 excess = direction * (projectedSpeed - maxAddedVelocity);
                rb.velocity -= excess;
            }
        }


        public void RemoveBuffAndDestroy(){initialized=false;Cleanup();Destroy(this);}
        private void Cleanup()
        {
            if(orbitRoot!=null){orbitRoot.SetActive(false);Destroy(orbitRoot);orbitRoot=null;}
            if(orbitMaterial!=null){Destroy(orbitMaterial);orbitMaterial=null;}
        }
        private void OnDisable(){if(initialized)RemoveBuffAndDestroy();}
        private void OnDestroy(){Cleanup();}
    }
}
