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
        public float walkFrameTime = 0.08f;

        [Tooltip("움직이지 않을 때 재생할 호흡 애니메이션입니다.")]
        public Sprite[] idleSpriteSequence;
        public float idleFrameTime = 0.125f;

        [Tooltip("대쉬 중 재생할 다리 회전 애니메이션입니다.")]
        public Sprite[] dashSpriteSequence;
        public float dashFrameTime = 0.0275f;

        [Tooltip("포획 직후 부리가 열리는 표정. 마지막 프레임을 구속 해제까지 유지합니다.")]
        public Sprite[] capturedSpriteSequence;
        public float capturedFrameTime = 0.09f;

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
