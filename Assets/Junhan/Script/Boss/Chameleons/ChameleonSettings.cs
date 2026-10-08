using UnityEngine;
namespace Vampire
{
    public sealed class ChameleonSettings : ScriptableObject
    {
        public GameObject foamPrefab;
        public float bodyWidth=3.36f, spawnMin=420, spawnMax=450;
        public float projectileDamage=8, projectileSpeed=3.2f, teleportDamage=16;
        public float invisibleSeconds=3, outlineSeconds=1, coffeeSeconds=8;
        public static ChameleonSettings Current => Resources.Load<ChameleonSettings>("Chameleons/Settings");
    }
}
