using UnityEngine;

namespace Vampire
{
    [CreateAssetMenu(fileName = "Trap Monster", menuName = "Blueprints/Monsters/Trap Monster", order = 2)]
    public class TrapMonsterBlueprint : MonsterBlueprint
    {
        [Header("Trap State")]
        [Tooltip("활성화 상태일 때 함정 몬스터의 체력입니다. 현재 방향키 미니게임 방식에서는 해제 조건으로 쓰지 않고, 기존 호환용으로 유지합니다.")]
        public float activeHealth = 30f;

        [Tooltip("플레이어에게 한 번에 주는 틱 데미지입니다.")]
        public float tickDamage = 5f;

        [Tooltip("틱 데미지를 주는 간격입니다.")]
        public float tickInterval = 0.5f;

        [Tooltip("활성화되면 플레이어를 이 시간 동안 묶습니다. 0 이하이면 방향키 미니게임을 풀 때까지 무제한으로 묶습니다.")]
        public float bindDuration = 4f;

        [Tooltip("활성화 직후 자동으로 플레이어를 묶습니다.")]
        public bool bindPlayerOnActivate = true;

        [Header("Trap Arrow Mini Game")]
        [Tooltip("체력으로 파괴해서 해제하지 않고, 방향키 미니게임으로 해제할지 여부입니다.")]
        public bool useArrowEscapeMiniGame = true;

        [Tooltip("랜덤으로 보여줄 방향키 개수입니다. 현재 요청 기준 기본값은 4입니다.")]
        public int arrowMiniGameSequenceLength = 4;

        [Tooltip("함정 몬스터 기준 UI가 위로 뜨는 높이입니다.")]
        public float arrowMiniGameUiYOffset = 1.35f;

        [Tooltip("월드 스페이스 UI 크기입니다. 너무 크거나 작으면 이 값을 조절하세요.")]
        public float arrowMiniGameUiScale = 0.01f;

        [Tooltip("방향키 UI의 Canvas Sorting Order입니다. 다른 UI나 스프라이트에 가리면 높이세요.")]
        public int arrowMiniGameSortingOrder = 6000;

        [Tooltip("틀린 방향키를 입력했을 때 진행도를 처음부터 되돌릴지 여부입니다.")]
        public bool resetArrowProgressOnWrongInput = false;

        [Tooltip("틀린 방향키를 입력했을 때 플레이어에게 주는 추가 피해입니다. 0이면 피해 없음입니다.")]
        public float wrongArrowInputPenaltyDamage = 0f;

        [Tooltip("틀린 입력 시 빨간색으로 깜빡이는 시간입니다.")]
        public float wrongArrowFlashDuration = 0.15f;

        [Header("Trap Spawn - Test")]
        [Tooltip("테스트용: 체크하면 스폰 시 플레이어 근처로 위치를 보정합니다.")]
        public bool spawnNearPlayerForTest = true;

        [Tooltip("플레이어와의 최소 거리입니다.")]
        public float spawnMinDistanceFromPlayer = 2f;

        [Tooltip("플레이어와의 최대 거리입니다.")]
        public float spawnMaxDistanceFromPlayer = 4f;

        [Header("Trap Visual")]
        [Tooltip("휴면 상태 스프라이트들입니다. 1장이면 정지 이미지, 여러 장이면 루프 애니메이션처럼 재생됩니다.")]
        public Sprite[] dormantSprites;

        [Tooltip("활성화 상태 스프라이트들입니다.")]
        public Sprite[] activeSprites;

        [Tooltip("사망 상태 스프라이트들입니다.")]
        public Sprite[] deathSprites;

        [Tooltip("스프라이트 프레임 간격입니다.")]
        public float animationFrameTime = 0.15f;

        [Tooltip("사망 애니메이션 종료 후 오브젝트 제거까지 대기 시간입니다.")]
        public float deathDespawnDelay = 0.15f;
    }
}