using UnityEngine;

namespace Vampire
{
    [CreateAssetMenu(
        fileName = "Character",
        menuName = "Blueprints/Character",
        order = 1
    )]
    public class CharacterBlueprint : ScriptableObject
    {
        [Header("Basic Info")]
        public new string name;

        [TextArea(2, 4)]
        public string description;

        [Header("Ownership")]
        public bool owned = false;
        public int cost = 999;

        [Header("Stats")]
        public float hp;
        public float recovery;
        public int armor;
        public float movespeed;
        public float luck;
        public float acceleration;

        [Header("Sprites")]
        public Sprite[] walkSpriteSequence;
        public float walkFrameTime = 0.15f;

        [Header("Result Animation")]
        public Sprite[] resultIdleSpriteSequence;
        public float resultIdleFrameTime = 0.2f;

        public Sprite[] resultActionASpriteSequence;
        public Sprite[] resultActionBSpriteSequence;
        public float resultActionFrameTime = 0.12f;

        [Header("Abilities")]
        public GameObject[] startingAbilities;


        public float LevelToExpIncrease(int level)
        {
            if (level < 10)
                return 10;

            if (level < 20)
                return 13;

            if (level < 30)
                return 16;

            return 20;
        }
    }
}