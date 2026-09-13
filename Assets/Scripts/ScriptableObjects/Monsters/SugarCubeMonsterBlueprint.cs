using UnityEngine;

namespace Vampire
{
    public enum SugarCubeSplitStage
    {
        NormalBody,
        NormalSplit1,
        EliteBody,
        EliteSplit1,
        EliteSplit2
    }

    [CreateAssetMenu(
        fileName = "Sugar Cube Monster",
        menuName = "Blueprints/Monsters/Sugar Cube Monster",
        order = 12)]
    public class SugarCubeMonsterBlueprint : MeleeMonsterBlueprint
    {
        [Header("Sugar Cube / 설탕 큐브")]
        [Tooltip("이 설탕 큐브가 본체인지, 분열체인지 구분하기 위한 표시입니다.")]
        public SugarCubeSplitStage splitStage = SugarCubeSplitStage.NormalBody;

        [Tooltip("이 몬스터가 죽었을 때 생성할 다음 단계 분열체 블루프린트입니다. 비워두면 분열하지 않습니다.")]
        public SugarCubeMonsterBlueprint splitChildBlueprint;

        [Tooltip("사망 시 생성할 분열체 수입니다. 기본은 마인크래프트 슬라임처럼 2마리입니다.")]
        public int splitChildCount = 2;

        [Tooltip("프리팹 원본 크기에 곱해지는 시각 크기 배율입니다. 본체 1, 분열체 0.5처럼 사용합니다.")]
        public float visualScaleMultiplier = 1f;

        [Header("Ground shadow / 바닥 그림자")]
        [Tooltip("투명 여백을 제외한 착지 바닥 중심입니다. 이미지 UV 기준이며 점프 중에는 고정됩니다.")]
        public Vector2 shadowGroundCenterUV = new Vector2(0.5f, 0.04f);

        [Range(0.01f, 1f)]
        [Tooltip("이미지 전체 폭 대비 몸체 바닥 폭입니다. PPU와 단계 크기 배율은 자동 반영됩니다.")]
        public float shadowGroundWidthUV = 0.83f;

        [Tooltip("분열체가 사망 위치에서 얼마나 떨어져 생성될지 결정합니다.")]
        public float splitSpawnRadius = 0.35f;

        [Tooltip("분열체가 생성 직후 바깥으로 살짝 튀어나가는 힘입니다.")]
        public float splitScatterKnockback = 1.5f;

        [Tooltip("true면 플레이어가 처치했을 때만 분열합니다. 이벤트/강제 삭제 때 분열 방지용입니다.")]
        public bool splitOnlyWhenKilledByPlayer = true;

        [Tooltip("true면 부모에게 적용된 HP Buff를 자식에게 넘기지 않습니다. 설탕큐브처럼 단계별 HP가 고정일 때 true 추천.")]
        public bool childIgnoresParentHpBuff = true;

        [Header("Sugar Cube Animation / 설탕 큐브 애니메이션")]
        [Tooltip("설탕 큐브 전용 걷기 배열을 사용합니다. 단일 4장, 트리오 24장, 엘리트 32장.")]
        public bool useSugarCubeWalkAnimationOverride = true;

        [Tooltip("단계에 맞는 프레임을 순서대로 넣으세요. 모든 칸에 유효한 스프라이트가 필요합니다.")]
        public Sprite[] sugarCubeWalkSpriteSequence = new Sprite[4];

        [Tooltip("설탕 큐브 전용 걷기 애니메이션 프레임 간격입니다. 값이 작을수록 빠르게 움직입니다.")]
        public float sugarCubeWalkFrameTime = 0.12f;

        [Header("Loot / 보상")]
        [Tooltip("이 단계의 설탕 큐브가 죽었을 때 경험치/코인을 드랍할지 여부입니다.")]
        public bool dropLootOnDeath = true;

        [Header("Debug")]
        [Tooltip("체크하면 설탕 큐브 스폰/분열 로그를 Console에 출력합니다.")]
        public bool debugLog = false;

        public bool CanSplit()
        {
            return splitChildBlueprint != null && splitChildCount > 0;
        }

        public Sprite[] GetEffectiveWalkSpriteSequence()
        {
            if (useSugarCubeWalkAnimationOverride && HasValidSugarCubeWalkSpriteSequence())
            {
                return sugarCubeWalkSpriteSequence;
            }

            return walkSpriteSequence;
        }

        public float GetEffectiveWalkFrameTime()
        {
            if (useSugarCubeWalkAnimationOverride && HasValidSugarCubeWalkSpriteSequence())
            {
                return Mathf.Max(0.01f, sugarCubeWalkFrameTime);
            }

            return Mathf.Max(0.01f, walkFrameTime);
        }

        private bool HasValidSugarCubeWalkSpriteSequence()
        {
            if (sugarCubeWalkSpriteSequence == null || sugarCubeWalkSpriteSequence.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < sugarCubeWalkSpriteSequence.Length; i++)
            {
                if (sugarCubeWalkSpriteSequence[i] == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
