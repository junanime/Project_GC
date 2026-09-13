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