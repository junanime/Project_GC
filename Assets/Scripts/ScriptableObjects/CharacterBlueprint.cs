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

        [Header("Ashi Visual Comparison")]
        [Tooltip("아시 눈썹 버전의 걷기 프레임입니다. 비어 있으면 기본 걷기 프레임을 사용합니다.")]
        public Sprite[] eyebrowWalkSpriteSequence;

        [Header("Ashi Syringe Attack Wing")]
        [Tooltip("몸체 애니메이션 위에서 재생할 뒤쪽 날개 공격 프레임입니다.")]
        public Sprite[] syringeRearWingAttackSpriteSequence;

        [Tooltip("몸체 애니메이션 위에서 재생할 앞쪽 날개 공격 프레임입니다.")]
        public Sprite[] syringeFrontWingAttackSpriteSequence;

        [Tooltip("날개 공격이 현재 발사 간격에서 차지하는 비율입니다.")]
        [Range(0.1f, 1f)] public float syringeAttackDurationRatio = 0.55f;

        [Tooltip("공격 속도가 매우 빠를 때도 인식 가능한 최소 날개 모션 시간입니다.")]
        public float syringeAttackMinDuration = 0.04f;

        [Tooltip("공격 속도가 느려도 날개가 오래 멈춰 있지 않도록 제한하는 최대 시간입니다.")]
        public float syringeAttackMaxDuration = 0.18f;

        [Tooltip("몸 중심에서 날개 뿌리까지의 가로 거리입니다.")]
        public float syringeAttackWingAnchorDistance = 0.14f;

        [Tooltip("날개 오버레이의 세로 위치 보정입니다.")]
        public float syringeAttackWingVerticalOffset = 0.015f;

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
