using UnityEngine;

namespace Vampire
{
    [CreateAssetMenu(menuName = "Blueprints/Prescription and blood clot balance")]
    public sealed class RemakeBalance : ScriptableObject
    {
        public enum QuestTag { Combat, Exploration, Skill }
        public bool randomTagEachRun = true;
        public QuestTag designatedTag;
        [Min(1)] public float overchargeRadius = 2.5f;
        [Min(1)] public int overchargeKills = 25;
        [Min(1)] public float overchargeStaySeconds = 25;
        [Min(.3f)] public float foodExplosionDelay = 1.25f;
        [Min(1)] public float foodExplosionRadiusMultiplier = 1.35f;
        [Range(0,1)] public float foodExplosionDamageMultiplier = .6f;
        [Min(2)] public int fastBomberEvery = 4;
        [Min(1)] public float fastBomberSpeed = 1.2f;
        [Min(.05f)] public float sniperTeleportDelay = .2f;
        [Min(1)] public float sniperTeleportCooldown = 6f;
        [Min(1)] public float sniperSafeDistance = 3;
        public ChestBlueprint levelUpChest;
        static RemakeBalance fallback;
        public static RemakeBalance Current
        {
            get
            {
                var asset = Resources.Load<RemakeBalance>("RemakeBalance");
                if (asset != null) return asset;
                if (fallback == null) { fallback = CreateInstance<RemakeBalance>(); fallback.hideFlags = HideFlags.HideAndDontSave; }
                return fallback;
            }
        }
    }
}
