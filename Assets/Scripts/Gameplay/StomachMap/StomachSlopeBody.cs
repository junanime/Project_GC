using UnityEngine;

namespace Vampire
{
    // Runs after the actor's movement code, before physics. Does not change saved movement stats.
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class StomachSlopeBody : MonoBehaviour
    {
        Rigidbody2D body;
        Character player;
        Monster monster;
        SnailBossRuntime snail;
        DigestiveEnzymeMonster enzyme;
        LevelManager level;
        Collider2D footprint;
        float slideSpeed;
        public float Radius
        {
            get
            {
                if (footprint == null) return .2f;
                var extents = footprint.bounds.extents;
                return Mathf.Max(.12f, Mathf.Max(extents.x, extents.y));
            }
        }
        public static StomachSlopeBody Ensure(GameObject owner) => owner.GetComponent<StomachSlopeBody>() ?? owner.AddComponent<StomachSlopeBody>();
        void Awake()
        {
            body=GetComponent<Rigidbody2D>();player=GetComponent<Character>();monster=GetComponent<Monster>();
            footprint=GetComponent<Collider2D>();
            snail=GetComponent<SnailBossRuntime>();enzyme=GetComponent<DigestiveEnzymeMonster>();
            var map = FiniteStomachMap.Instance;
            level = map != null && map.Owner != null ? map.Owner : FindObjectOfType<LevelManager>();
        }
        void OnEnable(){slideSpeed=0;}
        bool CanApply(FiniteStomachMap map)
        {
            return Time.timeScale>0 && map!=null && body!=null && body.simulated && map.AppliesAt(body.position) &&
                (level==null || (!level.IsRunFlowPaused || map.IsOral&&OralStageRuntime.Instance!=null&&OralStageRuntime.Instance.OnCeiling) && !level.IsLevelEnded) &&
                (player==null || player.IsAlive && !player.IsPortalTravelling && !player.IsPortalPoseHeld && !player.IsTrapBound && !player.IsDashing) &&
                (monster==null || !monster.IsMiniStageOwned && !monster.IsFieldRuntimeSuspended && monster.HP>0) &&
                (snail==null || !snail.Dead) && (enzyme==null || enzyme.IsAlive);
        }
        public float MovementMultiplier(Vector2 direction)
        {
            var map=FiniteStomachMap.Instance;
            if(map==null || map.IsOral || !map.AppliesAt(body.position))return 1;
            float d=map.NavigationDistance(body.position,out var inward);
            if(Vector2.Dot(direction,inward)>=0)return 1;
            float progress=Mathf.Clamp01((map.WallWidth-d)/Mathf.Max(.1f,map.Definition.climbableWidth-Radius));
            return FiniteStomachMap.UphillMultiplier(progress);
        }
        void FixedUpdate()
        {
            var map=FiniteStomachMap.Instance;
            if(!CanApply(map)){slideSpeed=0;return;}
            Vector2 position = body.position;
            float radius = Radius;
            if(map.IsOral){Constrain(map, position, radius, map.NavigationClearance(position));return;}
            float distance=map.NavigationDistance(position,out var inward);
            // Normalize against this actor's actual stopping point, including its footprint.
            // Otherwise a large monster would stop before ever reaching the intended 90% reduction.
            float progress=Mathf.Clamp01((map.WallWidth-distance)/Mathf.Max(.1f,map.Definition.climbableWidth-radius));
            if(progress<=0){slideSpeed=0;return;}
            Vector2 outward=-inward;
            Vector2 intent=player!=null?player.EffectiveMoveDirection:body.velocity;
            if(monster is MeleeMonster && level!=null)
                intent=(Vector2)level.PlayerCharacter.transform.position-body.position;
            bool climbing=Vector2.Dot(intent.normalized,outward)>.05f;
            float normalSpeed=Vector2.Dot(body.velocity,outward);
            float damping=1+body.drag*Time.fixedDeltaTime;
            if(normalSpeed>0 && snail==null)
            {
                float factor=FiniteStomachMap.UphillMultiplier(progress);
                // Compensate Unity's existing drag exactly: equilibrium uphill speed is factor * flat speed.
                float resistance=damping/(1+body.drag*Time.fixedDeltaTime/factor);
                // These controllers assign desired velocity each tick instead of accelerating.
                if(body.drag<.0001f || monster is AcidToadMonster || monster is NutritionThiefBacteriaMonster ||
                    monster is TreasureRunnerMonster || enzyme!=null || monster is ExplodingMonster explosion && explosion.HasLockedTrajectory)
                    resistance=factor;
                body.velocity-=outward*normalSpeed*(1-resistance);
                if(player!=null && player.EffectiveMoveDirection.sqrMagnitude<.001f)
                    body.velocity=Vector2.MoveTowards(body.velocity,Vector2.zero,8*Time.fixedDeltaTime);
            }
            if(!climbing)
            {
                slideSpeed=Mathf.MoveTowards(slideSpeed,Mathf.Lerp(.12f,1.2f,Mathf.SmoothStep(0,1,progress)),2.4f*Time.fixedDeltaTime);
                float downhill=Vector2.Dot(body.velocity,inward);
                body.velocity+=inward*Mathf.Max(0,slideSpeed*damping-downhill);
            }
            else slideSpeed=0;
            Constrain(map, position, radius, distance);
        }
        void LateUpdate()
        {
            var map=FiniteStomachMap.Instance;
            if(!CanApply(map))return;
            Vector2 position = body.position;
            Constrain(map, position, Radius, map.NavigationClearance(position));
        }
        void Constrain(FiniteStomachMap map, Vector2 position, float radius, float distance)
        {
            float limit=map.ClimbLimit+radius;
            if(distance>=limit+.05f)return;
            Vector2 p=map.IsOral && player!=null && OralStageRuntime.Instance!=null?OralStageRuntime.Instance.ConstrainPlayer(position,radius):map.ClampToField(position,limit);
            if((p-position).sqrMagnitude<.000001f)return;
            body.position=p;transform.position=new Vector3(p.x,p.y,transform.position.z);
            map.NavigationDistance(p,out var inward);
            body.velocity-=inward*Mathf.Min(0,Vector2.Dot(body.velocity,inward));
            if(monster!=null && level!=null)level.EntityManager.Grid.UpdateClient(monster);
        }
    }
}
