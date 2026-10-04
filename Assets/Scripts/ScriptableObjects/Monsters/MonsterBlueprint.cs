using UnityEngine;

namespace Vampire
{
    public class MonsterBlueprint : ScriptableObject
    {
        [Header("Stats")]
        public new string name;  // 이름
        public float hp;
        public float atk;
        public float recovery;
        public float armor;
        public float atkspeed;
        public float movespeed;
        public float acceleration;

        [Header("Drops")]
        public LootTable<GemType> gemLootTable;
        public LootTable<CoinType> coinLootTable;

        [Header("Animation")]
        public Sprite[] walkSpriteSequence;
        public float walkFrameTime;

        [Header("Ground shadow / 바닥 그림자")]
        public bool useGroundShadowFootprint;
        [Tooltip("스프라이트 사각형 기준 착지 중심. 왼쪽 아래가 (0, 0)입니다.")]
        public Vector2 groundShadowCenterUV = new Vector2(.5f, .05f);
        [Tooltip("투명 여백/날개가 아닌 실제 몸통의 바닥 그림자 폭과 두께입니다.")]
        public Vector2 groundShadowSizeUV = new Vector2(.7f, .14f);
        [Tooltip("공중 몬스터의 기존 바닥 높이를 유지하고 가로 중심과 크기만 보정합니다.")]
        public bool preserveGroundShadowHeight;

        // =========================================================
        // Result Screen
        // =========================================================

        [Header("Result Screen")]

        [TextArea(2, 4)]
        public string description;

        [Tooltip("결과 화면에서 보여줄 몬스터 대표 이미지. 비워두면 walkSpriteSequence의 첫 번째 이미지를 사용할 수 있습니다.")]
        public Sprite resultSprite;
    }
}
