using UnityEngine;
namespace Vampire
{
    [CreateAssetMenu(menuName="Blueprints/Character skills")]
    public sealed class CharacterSkillDefinition : ScriptableObject
    {
        public enum SkillKind { Ashi, Shini, Hyuki }
        public SkillKind kind;
        public Sprite[] phoenixFrames, verticalFireTrail, icePrison;
        public Sprite[] burningIdle, burningWalk, burningDash, fireTrail, burnVfx;
        public string passiveName = "마라톤", activeName = "단거리 경주";
        [TextArea] public string passiveDescription = "대쉬 후 3초간 발사체 +2, 공격속도 ×1.2";
        [TextArea] public string activeDescription = "12초간 발사체 수·이동속도·발사체 속도 ×2 · 재사용 45초";
        public Sprite passiveIcon, activeIcon;
        public Sprite[] cutin, passiveWind, activeWind, projectileWind;
        [Min(.1f)] public float passiveDuration = 3, activeDuration = 12, cooldown = 45;
        [Range(2,3)] public float cutinDuration = 2;
    }
}
