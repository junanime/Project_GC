using UnityEngine;
namespace Vampire
{
    [CreateAssetMenu(menuName="Boss/Roll cake settings")]
    public sealed class SnailBossSettings : ScriptableObject
    {
        public float maxHealth=6000, bodyWidth=4.2f, walkSpeed=.7f;
        public float phaseThreshold=.3f, transitionDuration=1.6f;
        public float basicCooldown=4f, patternGap=2f, projectileSpeed=3.2f;
        public int radialCount=18;
        public float vanillaBurstGap=.8f, chocolateBurstGap=.6f;
        public float dashSpeed=10f, dashDistance=9f, dashWarning=.8f, dashGap=1f, dashPuddleDuration=4f;
        [Range(0,1)] public float vanillaSlow=.25f, chocolateSlow=.3f;
        public float bombFlight=.85f, strawberryRadius=1.25f, kissesRadius=.7f;
        public float kissesPuddleDuration=3f, kissesSlowTail=2f;
        public float plateDirectionDifference=15f, plateLaneAngle=12f, melonSpread=9f;
        public float homingDuration=3f, mineLifetime=18f, groggyDuration=3f, groggyMultiplier=1.5f, trapDamageFraction=.025f;
        public float summonHealth=60, summonSpeed=1.5f, vanillaAbsorbSpeed=1.2f, chocolateAbsorbSpeed=1.5f;
        public float contactDamage=12, bulletDamage=8, bombDamage=14;
    }
    public enum SnailAction { Idle, Walk, Puff, DashPuff, Spit, Roll, Groggy, Absorb, Transition, Dead }
    public enum SnailPattern { Basic, Bomb, Fan, Homing, Summon, Dash, Absorb }
    public static class SnailBossRules
    {
        public static int BurstCount(bool chocolate)=>chocolate?3:2;
        public static int BombCount(bool chocolate)=>chocolate?5:2;
        public static float BombGap(bool chocolate)=>chocolate?.2f:2f;
        public static int AbsorbCount(bool chocolate)=>chocolate?12:6;
        public static float AbsorbFraction(bool chocolate)=>chocolate?.02f:.03f;
        public static float SpawnFraction(int removed)=>1f-.7f*.25f*Mathf.Clamp(removed,0,4);
        public static float PlateLaneDegrees(int lane,float angle)=>(lane-1)*angle;
        public static int Ingredient(SnailPattern pattern)=>pattern==SnailPattern.Basic?0:pattern==SnailPattern.Bomb?1:pattern==SnailPattern.Fan?2:pattern==SnailPattern.Homing?3:-1;
    }
}
