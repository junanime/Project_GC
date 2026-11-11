using UnityEngine;

namespace Vampire
{
    /// <summary>Separate fruit and metal coil; the base and its shadow stay on the floor.</summary>
    public sealed class OralLemonSpringVisual : MonoBehaviour
    {
        public const float CompressionSeconds = .09f;
        const float ExpansionSeconds = .13f, SettleSeconds = .46f;
        public float Extension { get; private set; } = 1f;
        public bool PulseActive => pulseAge < CompressionSeconds + ExpansionSeconds + SettleSeconds;
        public int PulseCount { get; private set; }
        public SpriteRenderer FruitRenderer { get; private set; }
        public SpriteRenderer SpringRenderer { get; private set; }
        public SpriteRenderer ShadowRenderer { get; private set; }
        Vector2 position;
        float width, springHeight, springWidth, pulseAge = 10f;

        public void Configure(Sprite fruit, Sprite spring, Vector2 ground, float fruitWidth)
        {
            position = ground; width = fruitWidth; springHeight = width * .40f; springWidth = width * .48f;
            transform.position = ground;
            ShadowRenderer = OralEventArt.Sprite(transform, "스프링 지면 그림자", OralEventArt.Disc, 16);
            GroundVisualSorting.Apply(ShadowRenderer, 16);
            ShadowRenderer.transform.position = ground;
            ShadowRenderer.transform.localScale = new Vector3(width * .64f, width * .20f, 1);
            ShadowRenderer.color = new Color(.16f, .035f, .045f, .30f);
            SpringRenderer = OralEventArt.Sprite(transform, "은색 금속 스프링", spring, 19);
            GroundVisualSorting.Apply(SpringRenderer, 19);
            FruitRenderer = OralEventArt.Sprite(transform, "통레몬", fruit, 20);
            GroundVisualSorting.Apply(FruitRenderer, 20);
            pulseAge = 10; PulseCount = 0; Extension = 1; Pose();
        }

        public void Pulse()
        {
            // Simultaneous visitors share one compression instead of repeatedly
            // resetting the coil before it can release.
            if (PulseActive && pulseAge < CompressionSeconds + ExpansionSeconds) return;
            pulseAge = 0; PulseCount++; Extension = 1; Pose();
        }

        public void Sample(float dt)
        {
            if (!PulseActive) return;
            pulseAge += Mathf.Max(0, dt);
            if (pulseAge < CompressionSeconds)
                Extension = Mathf.Lerp(1f, .64f, Mathf.SmoothStep(0, 1, pulseAge / CompressionSeconds));
            else if (pulseAge < CompressionSeconds + ExpansionSeconds)
                Extension = Mathf.Lerp(.64f, 1.26f, Mathf.SmoothStep(0, 1, (pulseAge - CompressionSeconds) / ExpansionSeconds));
            else
            {
                float t = Mathf.Clamp01((pulseAge - CompressionSeconds - ExpansionSeconds) / SettleSeconds);
                Extension = 1 + .26f * Mathf.Pow(1 - t, 2) * Mathf.Cos(t * Mathf.PI * 3);
            }
            if (!PulseActive) Extension = 1;
            Pose();
        }

        void Pose()
        {
            if (SpringRenderer != null && SpringRenderer.sprite != null)
            {
                var size = SpringRenderer.sprite.bounds.size;
                SpringRenderer.transform.position = position + Vector2.up * (width * .035f);
                SpringRenderer.transform.localScale = new Vector3(springWidth / Mathf.Max(.001f, size.x), springHeight * Extension / Mathf.Max(.001f, size.y), 1);
            }
            if (FruitRenderer != null && FruitRenderer.sprite != null)
            {
                float compression = Mathf.Max(0, 1 - Extension);
                float scale = width / Mathf.Max(.001f, FruitRenderer.sprite.bounds.size.x);
                FruitRenderer.transform.localScale = new Vector3(scale * (1 + compression * .12f), scale * (1 - compression * .13f), 1);
                FruitRenderer.transform.position = position + Vector2.up * (springHeight * Extension - width * .035f);
                float sway = PulseActive ? Mathf.Sin(pulseAge * 24) * Mathf.Max(0, 1 - pulseAge / .68f) * 2.2f : 0;
                FruitRenderer.transform.rotation = Quaternion.Euler(0, 0, sway);
            }
        }
    }
}
