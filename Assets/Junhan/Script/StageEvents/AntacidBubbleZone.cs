using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 제산 거품 폭주 이벤트에서 생성되는 안전 거품 하나입니다.
    /// 플레이어가 이 반경 안에 있으면 이벤트 지속 피해를 받지 않습니다.
    /// 몬스터 피해까지 막는 완전 무적 구역은 아니며, 1차 구현은 '이벤트 피해 회피 구역'입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class AntacidBubbleZone : MonoBehaviour
    {
        [Header("Runtime")]
        [Tooltip("현재 거품 반경입니다.")]
        [SerializeField] private float radius = 1.8f;

        [Tooltip("현재 거품이 유지되는 시간입니다.")]
        [SerializeField] private float lifetime = 5f;

        [Tooltip("거품이 사라질 때 투명해지는 시간입니다.")]
        [SerializeField] private float fadeOutDuration = 0.35f;

        private float age;
        private SpriteRenderer spriteRenderer;
        private CircleCollider2D circleCollider;
        private PolygonCollider2D groundCollider;
        // UV footprint excludes floating bubbles in the upper part of the image.
        [SerializeField] private Vector2 groundCenterUV = new Vector2(.5f, .40f);
        [SerializeField] private Vector2 groundRadiiUV = new Vector2(.46f, .28f);
        private bool debugLog;
        [Header("Bubble lift and pop")]
        [SerializeField] private Sprite[] bubblePopFrames;
        private Color baseColor;
        private BubbleVisual[] bubbles;
        private sealed class BubbleVisual
        {
            public SpriteRenderer renderer;
            public Vector3 origin;
            public float launch, duration, size, sway;
        }

        public float Radius => radius;

        public void Init(float radius, float lifetime, float fadeOutDuration, Color bubbleColor, bool debugLog)
        {
            this.radius = Mathf.Max(0.1f, radius);
            this.lifetime = Mathf.Max(0.1f, lifetime);
            this.fadeOutDuration = Mathf.Max(0.01f, fadeOutDuration);
            this.debugLog = debugLog;

            age = 0f;
            baseColor = bubbleColor;
            GroundVisualSorting.ApplyHierarchy(gameObject);

            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            circleCollider = GetComponent<CircleCollider2D>();

            if (circleCollider == null)
            {
                circleCollider = gameObject.AddComponent<CircleCollider2D>();
            }

            circleCollider.isTrigger = true;

            if (spriteRenderer != null && spriteRenderer.transform != transform)
            {
                spriteRenderer.transform.localScale = Vector3.one * this.radius * 2f;
                circleCollider.radius = this.radius;
            }
            else
            {
                transform.localScale = Vector3.one * this.radius * 2f;
                circleCollider.radius = 0.5f;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = bubbleColor;
            }
            ConfigureGroundFootprint();
            CreateBubbles();

            if (debugLog)
            {
                Debug.Log($"[제산 거품] 생성 | Radius={this.radius} | Lifetime={this.lifetime}", this);
            }
        }

        private void Update()
        {
            if (MiniStageRuntimeState.IsInsideMiniStage)
            {
                return;
            }

            age += Time.deltaTime;

            if (spriteRenderer != null)
            {
                float remaining = lifetime - age;
                // Depletion starts with the first lift; keep the safe area readable until expiry.
                Color color = baseColor;
                color.a *= Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(age / lifetime))
                    * Mathf.Clamp01(remaining / Mathf.Min(fadeOutDuration, lifetime));
                spriteRenderer.color = color;
            }
            AnimateBubbles();

            if (age >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        public bool ContainsPoint(Vector2 point)
        {
            if (MiniStageRuntimeState.IsInsideMiniStage || age >= lifetime)
            {
                return false;
            }

            if(spriteRenderer==null||spriteRenderer.sprite==null)return false;
            Bounds b=spriteRenderer.sprite.bounds;
            Vector3 p=spriteRenderer.transform.InverseTransformPoint(point);
            Vector2 uv=new Vector2((p.x-b.min.x)/b.size.x,(p.y-b.min.y)/b.size.y);
            Vector2 d=uv-groundCenterUV;
            return d.x*d.x/(groundRadiiUV.x*groundRadiiUV.x)+d.y*d.y/(groundRadiiUV.y*groundRadiiUV.y)<=1f;
        }

        private void ConfigureGroundFootprint()
        {
            if(spriteRenderer==null||spriteRenderer.sprite==null)return;
            if(circleCollider!=null)circleCollider.enabled=false;
            groundCollider=GetComponent<PolygonCollider2D>();
            if(groundCollider==null)groundCollider=gameObject.AddComponent<PolygonCollider2D>();
            groundCollider.isTrigger=true;
            var points=new Vector2[48];Bounds b=spriteRenderer.sprite.bounds;
            for(int i=0;i<points.Length;i++)
            {
                float a=i*Mathf.PI*2/points.Length;
                Vector2 uv=groundCenterUV+new Vector2(Mathf.Cos(a)*groundRadiiUV.x,Mathf.Sin(a)*groundRadiiUV.y);
                Vector3 local=new Vector3(b.min.x+uv.x*b.size.x,b.min.y+uv.y*b.size.y,0);
                points[i]=transform.InverseTransformPoint(spriteRenderer.transform.TransformPoint(local));
            }
            groundCollider.SetPath(0,points);
        }

        private void CreateBubbles()
        {
            if (bubbles != null)
                foreach (var bubble in bubbles)
                    if (bubble.renderer != null) Destroy(bubble.renderer.gameObject);
            bubbles = null;
            if (spriteRenderer == null || bubblePopFrames == null || bubblePopFrames.Length == 0) return;
            bubbles = new BubbleVisual[14];
            for (int i = 0; i < bubbles.Length; i++)
            {
                var obj = new GameObject("Rising soap bubble " + i);
                obj.transform.SetParent(transform, false);
                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = bubblePopFrames[0];
                sr.sharedMaterial = spriteRenderer.sharedMaterial;
                GroundVisualSorting.Apply(sr, 1);
                float angle = i * 2.399963f;
                float distance = Mathf.Sqrt((i + 0.5f) / bubbles.Length) * radius * 0.72f;
                // Store offsets in world units so the fallback root scale cannot enlarge the bubbles.
                Vector3 origin = new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance * 0.65f, 0f);
                float duration = Mathf.Min(1.2f, lifetime * 0.28f);
                bubbles[i] = new BubbleVisual { renderer = sr, origin = origin,
                    launch = i / (float)(bubbles.Length - 1) * (lifetime - duration),
                    duration = duration, size = radius * (0.22f + (i % 3) * 0.055f), sway = angle };
            }
            AnimateBubbles();
        }

        private void AnimateBubbles()
        {
            if (bubbles == null) return;
            foreach (var bubble in bubbles)
            {
                float t = Mathf.Clamp01((age - bubble.launch) / bubble.duration);
                bubble.renderer.enabled = t < 1f;
                if (t >= 1f) continue;
                bool launched = age >= bubble.launch;
                float lift = Mathf.Min(t / 0.78f, 1f);
                bubble.renderer.transform.position = transform.position + bubble.origin +
                    new Vector3(launched ? Mathf.Sin(t * 5f + bubble.sway) * radius * 0.08f * lift : 0f,
                        lift * radius * 0.75f, 0f);
                int frame = t < 0.78f ? 0 : Mathf.Min(bubblePopFrames.Length - 1,
                    1 + Mathf.FloorToInt((t - 0.78f) / 0.22f * (bubblePopFrames.Length - 1)));
                bubble.renderer.sprite = bubblePopFrames[frame];
                float size = bubble.size * (1f + lift * 0.18f);
                Vector3 parentScale = transform.lossyScale;
                bubble.renderer.transform.localScale = new Vector3(size / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                    size / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)), 1f);
                Color color = Color.white;
                color.a = baseColor.a * (t < 0.85f ? 1f : Mathf.Clamp01((1f - t) / 0.15f));
                bubble.renderer.color = color;
            }
        }
    }
}
