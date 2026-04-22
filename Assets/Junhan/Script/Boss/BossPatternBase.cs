using System.Collections;
using UnityEngine;

namespace Vampire
{
    public abstract class BossPatternBase : MonoBehaviour
    {
        [Header("Pattern Info")]
        [SerializeField] protected string patternName;
        [SerializeField] protected float cooldown = 3f;

        [Header("Distance Weights - Phase 1")]
        [SerializeField] protected int nearWeightPhase1 = 10;
        [SerializeField] protected int midWeightPhase1 = 10;
        [SerializeField] protected int farWeightPhase1 = 10;

        [Header("Distance Weights - Phase 2")]
        [SerializeField] protected int nearWeightPhase2 = 10;
        [SerializeField] protected int midWeightPhase2 = 10;
        [SerializeField] protected int farWeightPhase2 = 10;

        protected BossController bossController;
        protected float lastUseTime = -999f;

        public string PatternName => patternName;

        public virtual void Init(BossController controller)
        {
            bossController = controller;
        }

        public virtual bool CanUse()
        {
            return Time.time >= lastUseTime + cooldown;
        }

        public int GetWeight(float distanceToPlayer, int phase)
        {
            if (phase == 1)
            {
                if (distanceToPlayer <= bossController.NearDistanceThreshold)
                    return nearWeightPhase1;

                if (distanceToPlayer <= bossController.MidDistanceThreshold)
                    return midWeightPhase1;

                return farWeightPhase1;
            }
            else
            {
                if (distanceToPlayer <= bossController.NearDistanceThreshold)
                    return nearWeightPhase2;

                if (distanceToPlayer <= bossController.MidDistanceThreshold)
                    return midWeightPhase2;

                return farWeightPhase2;
            }
        }

        public IEnumerator Execute()
        {
            lastUseTime = Time.time;
            yield return ExecutePattern();
        }

        protected abstract IEnumerator ExecutePattern();
    }
}