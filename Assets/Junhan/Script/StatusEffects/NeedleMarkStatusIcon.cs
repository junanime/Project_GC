using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 표식침 상태일 때 몬스터 머리 위에 표시되는 X자 표식입니다.
    /// 표식 소모 시 X 위로 사선 베기 연출을 만든 뒤 페이드아웃됩니다.
    /// </summary>
    public class NeedleMarkStatusIcon : MonoBehaviour
    {
        [Header("X Mark Visual / X 표식")]
        [Tooltip("X 표식의 크기입니다.")]
        [SerializeField] private float markSize = 0.26f;

        [Tooltip("X 표식 선 두께입니다.")]
        [SerializeField] private float markLineWidth = 0.055f;

        [Tooltip("X 표식 색상입니다.")]
        [SerializeField] private Color markColor = new Color(1f, 0.08f, 0.08f, 0.95f);

        [Header("Idle Animation / 대기 애니메이션")]
        [Tooltip("표식이 살짝 커졌다 작아지는 속도입니다.")]
        [SerializeField] private float idlePulseSpeed = 5f;

        [Tooltip("표식이 살짝 커졌다 작아지는 강도입니다.")]
        [SerializeField] private float idlePulseAmount = 0.07f;

        [Tooltip("표식이 살짝 흔들리는 각도입니다.")]
        [SerializeField] private float idleShakeAngle = 4f;

        [Tooltip("표식이 살짝 흔들리는 속도입니다.")]
        [SerializeField] private float idleShakeSpeed = 7f;

        [Header("Consume Animation / 소모 연출")]
        [Tooltip("표식이 소모되며 사라지는 데 걸리는 시간입니다.")]
        [SerializeField] private float consumeDuration = 0.22f;

        [Tooltip("표식 위를 스쳐 지나가는 베기 선 두께입니다.")]
        [SerializeField] private float slashLineWidth = 0.08f;

        [Tooltip("표식 위를 스쳐 지나가는 베기 선 색상입니다.")]
        [SerializeField] private Color slashColor = new Color(1f, 1f, 1f, 1f);

        [Tooltip("소모 시 표식이 얼마나 작아지는지 정합니다.")]
        [SerializeField] private float consumeScaleMultiplier = 0.45f;

        [Tooltip("소모 시 표식이 위로 살짝 떠오르는 거리입니다.")]
        [SerializeField] private float consumeRiseAmount = 0.12f;

        [Header("Sorting / 정렬")]
        [Tooltip("표식이 몬스터보다 앞에 보이도록 적용할 Sorting Order입니다.")]
        [SerializeField] private int sortingOrder = 910;

        private LineRenderer markLineA;
        private LineRenderer markLineB;
        private LineRenderer slashLine;

        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private bool isConsuming = false;

        private void Awake()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalScale = transform.localScale;

            markLineA = CreateLineRenderer("MarkLine_A", markColor, markLineWidth, sortingOrder);
            markLineB = CreateLineRenderer("MarkLine_B", markColor, markLineWidth, sortingOrder);
            slashLine = CreateLineRenderer("ConsumeSlash", slashColor, slashLineWidth, sortingOrder + 1);

            slashLine.enabled = false;

            RebuildXMark();
        }

        private void Update()
        {
            if (isConsuming)
            {
                return;
            }

            float pulse = 1f + Mathf.Sin(Time.time * idlePulseSpeed) * idlePulseAmount;
            transform.localScale = baseLocalScale * pulse;

            float angle = Mathf.Sin(Time.time * idleShakeSpeed) * idleShakeAngle;
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>
        /// 표식이 소모될 때 호출합니다.
        /// X 표식 위로 사선 베기 연출을 보여주고 바로 페이드아웃합니다.
        /// </summary>
        public void PlayConsumeAndDestroy()
        {
            if (isConsuming)
            {
                return;
            }

            StartCoroutine(ConsumeRoutine());
        }

        private IEnumerator ConsumeRoutine()
        {
            isConsuming = true;

            if (slashLine != null)
            {
                slashLine.enabled = true;
            }

            Vector3 startScale = transform.localScale;
            Vector3 endScale = baseLocalScale * consumeScaleMultiplier;

            Vector3 startPosition = transform.localPosition;
            Vector3 endPosition = baseLocalPosition + Vector3.up * consumeRiseAmount;

            float elapsed = 0f;

            while (elapsed < consumeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / consumeDuration);

                transform.localScale = Vector3.Lerp(startScale, endScale, t);
                transform.localPosition = Vector3.Lerp(startPosition, endPosition, t);
                transform.localRotation = Quaternion.identity;

                float alpha = Mathf.Lerp(1f, 0f, t);

                SetLineAlpha(markLineA, alpha);
                SetLineAlpha(markLineB, alpha);
                SetLineAlpha(slashLine, alpha);

                UpdateSlashLine(t);

                yield return null;
            }

            Destroy(gameObject);
        }

        private void RebuildXMark()
        {
            float half = Mathf.Max(0.05f, markSize);

            if (markLineA != null)
            {
                markLineA.positionCount = 2;
                markLineA.SetPosition(0, new Vector3(-half, half, 0f));
                markLineA.SetPosition(1, new Vector3(half, -half, 0f));
            }

            if (markLineB != null)
            {
                markLineB.positionCount = 2;
                markLineB.SetPosition(0, new Vector3(-half, -half, 0f));
                markLineB.SetPosition(1, new Vector3(half, half, 0f));
            }
        }

        private void UpdateSlashLine(float t)
        {
            if (slashLine == null)
            {
                return;
            }

            float half = markSize * 1.35f;
            float moveOffset = Mathf.Lerp(-0.28f, 0.28f, t);

            slashLine.positionCount = 2;
            slashLine.SetPosition(0, new Vector3(-half + moveOffset, half, 0f));
            slashLine.SetPosition(1, new Vector3(half + moveOffset, -half, 0f));
        }

        private LineRenderer CreateLineRenderer(string objectName, Color color, float width, int order)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(transform, false);
            lineObject.transform.localPosition = Vector3.zero;
            lineObject.transform.localRotation = Quaternion.identity;
            lineObject.transform.localScale = Vector3.one;

            LineRenderer lr = lineObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.positionCount = 2;
            lr.widthMultiplier = Mathf.Max(0.01f, width);
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.sortingOrder = order;

            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                lr.material = new Material(spriteShader);
            }

            lr.startColor = color;
            lr.endColor = color;

            return lr;
        }

        private void SetLineAlpha(LineRenderer lr, float alpha)
        {
            if (lr == null)
            {
                return;
            }

            Color start = lr.startColor;
            Color end = lr.endColor;

            start.a = alpha;
            end.a = alpha;

            lr.startColor = start;
            lr.endColor = end;
        }
    }
}