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
        [Tooltip("캐릭터 선택 카드 전용 정면 프로필. 인게임 애니메이션과 분리합니다.")]
        public Sprite profileSprite;
        public Sprite[] walkSpriteSequence;
        public float walkFrameTime = 0.08f;

        [Tooltip("움직이지 않을 때 재생할 호흡 애니메이션입니다.")]
        public Sprite[] idleSpriteSequence;
        public float idleFrameTime = 0.125f;

        [Tooltip("대쉬 중 재생할 다리 회전 애니메이션입니다.")]
        public Sprite[] dashSpriteSequence;
        public float dashFrameTime = 0.0275f;

        [Tooltip("대쉬 전체 시간에 프레임을 한 번씩 배분합니다. 능력치에는 영향이 없습니다.")]
        public bool fitDashAnimationToDuration;

        [Tooltip("대쉬 원본이 오른쪽 이동 기준이면 체크. 혁이 원본은 왼쪽을 보며 오른쪽으로 문워크합니다.")]
        public bool dashArtMovesRight = true;

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
