using UnityEngine;
namespace Vampire
{
    // Own only our drag multiplier, leaving other slow effects and buffs intact.
    [DefaultExecutionOrder(900)]
    public sealed class IceChillStatus : MonoBehaviour
    {
        public const int FreezeStacks=4;
        public int Stacks {get;private set;}
        public float SpeedMultiplier {get;private set;}=1;
        Component target; Rigidbody2D rb; float lastDrag,baseDrag;bool applied;
        public static float Multiplier(int stacks,int upgrades=0) => stacks<=0?1:Mathf.Max(.1f,1-(.1f+.1f*Mathf.Min(stacks,3))-.05f*upgrades*stacks);
        void Awake(){target=GetComponent<IDamageable>();rb=GetComponent<Rigidbody2D>();}
        public void Hit(int upgrades, Character source=null)
        {
            if(target==null||Ver4HitEffects.Health(target)<=0)return;
            var frozen=GetComponent<NeuralBlockedMonsterStatus>();
            if(frozen!=null&&frozen.IceFrozen)return;
            Stacks++;SpeedMultiplier=Multiplier(Stacks,upgrades);
            // Roll even on the fourth hit. A failed roll grows Hyuki's chance while the
            // independent four-stack guarantee still freezes; only a successful roll resets it.
            float roll=Random.value;
            bool instant=source!=null && source.IsAlive && source.Skills!=null
                ? source.Skills.RollInstantFreeze(roll) : roll<IceSkillRules.InstantFreezeChance;
            if(Stacks>=FreezeStacks || instant){Clear();IceSkillRules.Freeze(target);return;}
            if(GetComponent<IceChillVisual>()==null)gameObject.AddComponent<IceChillVisual>();
            ApplyDrag();
            if(target is BossPartDamageTestPart)
            {
                var boss=target.GetComponentInParent<BossController>();
                if(boss==null){var root=target.GetComponentInParent<BossPartDamageTestRootController>();if(root!=null)boss=root.GetComponentInChildren<BossController>();}
                if(boss!=null)boss.ApplyVer4IceChill(IceSkillRules.FreezeDuration,SpeedMultiplier);
            }
        }
        void FixedUpdate()
        {
            if(target==null||Ver4HitEffects.Health(target)<=0){Clear();return;}
            if(target is Monster m&&m.IsFieldRuntimeSuspended)return;
            if(Stacks>0)ApplyDrag();
        }
        void ApplyDrag()
        {
            if(rb==null||target is AcidLeechMonster||target is ExplodingMonster||target is TreasureRunnerMonster||target is NutritionThiefBacteriaMonster)return;
            if(!applied||!Mathf.Approximately(rb.drag,lastDrag))baseDrag=rb.drag;
            // Terminal movement is acceleration / drag: apply the speed ratio once, not cumulatively.
            lastDrag=baseDrag/SpeedMultiplier;rb.drag=lastDrag;applied=true;
        }
        public static float UnmodifiedDrag(Rigidbody2D body)
        {
            var s=body!=null?body.GetComponent<IceChillStatus>():null;
            return s!=null&&s.applied&&Mathf.Approximately(body.drag,s.lastDrag)?s.baseDrag:body!=null?body.drag:0;
        }
        public static void SetBaseDrag(Rigidbody2D body,float value)
        {
            if(body==null)return;
            body.drag=value;var s=body.GetComponent<IceChillStatus>();
            if(s!=null){s.applied=false;if(s.Stacks>0)s.ApplyDrag();}
        }
        public void Clear()
        {
            if(rb!=null&&applied&&Mathf.Approximately(rb.drag,lastDrag))rb.drag=baseDrag;
            applied=false;Stacks=0;SpeedMultiplier=1;
        }
        void OnDisable(){Clear();}
        void OnDestroy(){Clear();}
    }
}
