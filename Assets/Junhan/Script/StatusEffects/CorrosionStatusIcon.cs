using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 부식침 상태일 때 몬스터 머리 위에 표시되는 보라색 소용돌이 아이콘입니다.
    /// 별도 스프라이트 없이 LineRenderer만으로 소용돌이를 그립니다.
    /// </summary>
    public class CorrosionStatusIcon : MonoBehaviour
    {
        [Header("Spiral Visual / 소용돌이 표시")]
        [Tooltip("소용돌이를 구성하는 점 개수입니다. 높을수록 부드럽지만 약간 더 무겁습니다.")]
        [SerializeField] private int pointCount = 36;

        [Tooltip("소용돌이가 몇 바퀴 감기는지 정합니다.")]
        [SerializeField] private float spiralTurns = 2.25f;

        [Tooltip("소용돌이의 최대 반지름입니다.")]
        [SerializeField] private float radius = 0.22f;

        [Tooltip("소용돌이 선의 두께입니다.")]
        [SerializeField] private float lineWidth = 0.045f;

        [Tooltip("소용돌이 색상입니다.")]
        [SerializeField] private Color spiralColor = new Color(0.75f, 0.1f, 1f, 0.95f);

        [Header("Animation / 애니메이션")]
        [Tooltip("소용돌이가 회전하는 속도입니다.")]
        [SerializeField] private float rotationSpeed = 220f;

        [Tooltip("소용돌이가 살짝 커졌다 작아지는 속도입니다.")]
        [SerializeField] private float pulseSpeed = 6f;

        [Tooltip("소용돌이가 살짝 커졌다 작아지는 강도입니다.")]
        [SerializeField] private float pulseAmount = 0.08f;

        [Tooltip("머리 위에서 위아래로 흔들리는 속도입니다.")]
        [SerializeField] private float bobSpeed = 4f;

        [Tooltip("머리 위에서 위아래로 흔들리는 거리입니다.")]
        [SerializeField] private float bobAmount = 0.04f;

        [Header("Sorting / 정렬")]
        [Tooltip("소용돌이 표시가 몬스터보다 앞에 보이도록 적용할 Sorting Order입니다.")]
        [SerializeField] private int sortingOrder = 900;

        private LineRenderer lineRenderer;
        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;

        private void Awake()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalScale = transform.localScale;

            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            SetupLineRenderer();
            RebuildSpiral();
        }

        private void Update()
        {
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = baseLocalScale * pulse;

            Vector3 pos = baseLocalPosition;
            pos.y += Mathf.Sin(Time.time * bobSpeed) * bobAmount;
            transform.localPosition = pos;
        }

        private void SetupLineRenderer()
        {
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = false;
            lineRenderer.positionCount = Mathf.Max(8, pointCount);
            lineRenderer.widthMultiplier = Mathf.Max(0.01f, lineWidth);
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.sortingOrder = sortingOrder;

            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                lineRenderer.material = new Material(spriteShader);
            }

            lineRenderer.startColor = spiralColor;
            lineRenderer.endColor = spiralColor;
        }

        private void RebuildSpiral()
        {
            int count = Mathf.Max(8, pointCount);
            lineRenderer.positionCount = count;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float angle = t * spiralTurns * Mathf.PI * 2f;
                float currentRadius = Mathf.Lerp(0.02f, radius, t);

                Vector3 point = new Vector3(
                    Mathf.Cos(angle) * currentRadius,
                    Mathf.Sin(angle) * currentRadius,
                    0f
                );

                lineRenderer.SetPosition(i, point);
            }
        }
    }
}