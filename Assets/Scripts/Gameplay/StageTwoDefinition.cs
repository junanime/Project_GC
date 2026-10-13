using System;
using UnityEngine;

namespace Vampire
{
    public sealed class StageTwoDefinition : ScriptableObject
    {
        public GameObject snailPrefab;
        public Sprite body;
        public Sprite[] shells;
        public string[] names;
        public Sprite crater;
        public Texture2D cream;
        public ChestBlueprint rewardChest;

        public bool IsReady => snailPrefab != null && body != null && shells != null && shells.Length == 9 &&
            Array.TrueForAll(shells, s => s != null) && names != null && names.Length == 9 &&
            crater != null && cream != null && rewardChest != null;
    }
}
