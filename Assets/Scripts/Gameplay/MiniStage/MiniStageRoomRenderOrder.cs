using UnityEngine;

namespace Vampire
{
    public class MiniStageRoomRenderOrder : MonoBehaviour
    {
        [Header("Sorting Layer")]
        [Tooltip("미니 스테이지 배경 오브젝트에 적용할 Sorting Layer 이름입니다. 대부분 Default를 사용하면 됩니다.")]
        [SerializeField] private string sortingLayerName = "Default";

        [Header("Scene Background")]
        [Tooltip("기존 무한 타일 배경의 Renderer입니다. 비워두면 씬에서 InfiniteBackground를 찾아 자동 적용합니다.")]
        [SerializeField] private Renderer infiniteBackgroundRenderer;

        [Tooltip("기존 무한 타일 배경의 Order in Layer입니다. 미니 스테이지 검정 배경보다 낮아야 합니다.")]
        [SerializeField] private int infiniteBackgroundOrder = -1000;

        [Header("Mini Stage Background")]
        [Tooltip("기존 무한 타일 맵을 가리기 위한 검정색 배경 Renderer입니다.")]
        [SerializeField] private Renderer blackBackgroundRenderer;

        [Tooltip("미니 스테이지 검정 배경의 Order in Layer입니다. 기존 무한 배경보다 높고, 미니 스테이지 이미지보다 낮아야 합니다.")]
        [SerializeField] private int blackBackgroundOrder = -900;

        [Tooltip("미니 스테이지 실제 백그라운드 이미지 Renderer입니다.")]
        [SerializeField] private Renderer miniStageBackgroundRenderer;

        [Tooltip("미니 스테이지 백그라운드 이미지의 Order in Layer입니다. 플레이어/몬스터/투사체보다 낮아야 합니다.")]
        [SerializeField] private int miniStageBackgroundOrder = -800;

        [Header("Optional Room Visuals")]
        [Tooltip("배경 위에 올릴 추가 장식 Renderer들입니다. 없어도 됩니다.")]
        [SerializeField] private Renderer[] additionalBackgroundRenderers;

        [Tooltip("추가 장식 Renderer들의 Order in Layer입니다.")]
        [SerializeField] private int additionalBackgroundOrder = -750;

        [Tooltip("벽, 테두리, 장애물 등 방 구조물 Renderer들입니다. 없어도 됩니다.")]
        [SerializeField] private Renderer[] wallAndPropRenderers;

        [Tooltip("벽, 테두리, 장애물 등의 Order in Layer입니다. 기본값은 플레이어보다 낮게 둡니다.")]
        [SerializeField] private int wallAndPropOrder = -100;

        [Header("Apply Options")]
        [Tooltip("Awake에서 자동으로 정렬값을 적용합니다.")]
        [SerializeField] private bool applyOnAwake = true;

        [Tooltip("OnValidate에서 에디터 중에도 정렬값을 적용합니다. 프리팹 작업 중 확인하기 좋습니다.")]
        [SerializeField] private bool applyInEditor = true;

        [Tooltip("정렬값 적용 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private void Awake()
        {
            if (applyOnAwake)
            {
                ApplyRenderOrder();
            }
        }

        private void OnValidate()
        {
            if (applyInEditor)
            {
                ApplyRenderOrder();
            }
        }

        [ContextMenu("Apply Render Order")]
        public void ApplyRenderOrder()
        {
            ResolveInfiniteBackgroundRenderer();

            ApplyRenderer(infiniteBackgroundRenderer, infiniteBackgroundOrder);
            ApplyRenderer(blackBackgroundRenderer, blackBackgroundOrder);
            ApplyRenderer(miniStageBackgroundRenderer, miniStageBackgroundOrder);
            ApplyRenderers(additionalBackgroundRenderers, additionalBackgroundOrder);
            ApplyRenderers(wallAndPropRenderers, wallAndPropOrder);

            if (debugLog)
            {
                Debug.Log(
                    $"[MiniStageRoomRenderOrder] 렌더 순서 적용 완료. " +
                    $"Infinite={infiniteBackgroundOrder}, Black={blackBackgroundOrder}, MiniBG={miniStageBackgroundOrder}",
                    this
                );
            }
        }

        private void ResolveInfiniteBackgroundRenderer()
        {
            if (infiniteBackgroundRenderer != null)
            {
                return;
            }

            InfiniteBackground infiniteBackground = FindObjectOfType<InfiniteBackground>();

            if (infiniteBackground == null)
            {
                return;
            }

            infiniteBackgroundRenderer = infiniteBackground.GetComponent<Renderer>();
        }

        private void ApplyRenderers(Renderer[] renderers, int order)
        {
            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                ApplyRenderer(renderers[i], order);
            }
        }

        private void ApplyRenderer(Renderer targetRenderer, int order)
        {
            if (targetRenderer == null)
            {
                return;
            }

            targetRenderer.sortingLayerName = sortingLayerName;
            targetRenderer.sortingOrder = order;
        }
    }
}