using UnityEngine;

namespace Vampire
{
    /// <summary>Shared, movement-driven miniature rig for field, summon and absorb actors.</summary>
    public sealed class SnailMiniVisual : MonoBehaviour
    {
        public const float ShellWidth = .72f;
        public const float BodyWidth = 1.02f;
        public const float CycleDistance = .72f;
        public SpriteRenderer Shell { get; private set; }
        public SpriteRenderer Body { get; private set; }
        public bool Chocolate { get; private set; }
        public int Ingredient { get; private set; }
        public float WalkPhase { get; private set; }
        public bool FacingRight { get; private set; }
        Transform facingRoot;
        Transform actor;
        Monster fieldActor;
        Vector3 previousPosition;
        float bodyScale;
        bool configured;

        public static string ShellKey(bool chocolate, int ingredient)
        {
            return "Minis/" + SnailBossArt.Projectile(chocolate, Mathf.Clamp(ingredient, 0, 3));
        }

        public void Configure(Transform movementRoot, bool chocolate, int ingredient)
        {
            actor = movementRoot;
            fieldActor = actor.GetComponent<Monster>();
            Chocolate = chocolate;
            Ingredient = Mathf.Clamp(ingredient, 0, 3);
            if (facingRoot == null)
            {
                facingRoot = new GameObject("Mini snail facing").transform;
                facingRoot.SetParent(transform, false);
                Shell = new GameObject("Single ingredient cake").AddComponent<SpriteRenderer>();
                Shell.transform.SetParent(facingRoot, false);
                Shell.sortingOrder = 505;
                Body = SnailBossArt.Make(facingRoot, "Body1", BodyWidth, 506);
            }
            Shell.sprite = SnailBossArt.Get(ShellKey(chocolate, Ingredient));
            // Each shell import is calibrated by its opaque bounds, with a bottom-center pivot.
            // Do not scale by the padded PNG canvas or deform the baked ingredient/topping layer.
            Shell.transform.localScale = Vector3.one;
            SnailBossArt.Set(Body, chocolate ? "Body2" : "Body1", BodyWidth);
            bodyScale = Body.transform.localScale.x;
            WalkPhase = 0;
            FacingRight = false;
            previousPosition = actor.position;
            configured = true;
            ApplyPose(0, false);
        }

        void LateUpdate()
        {
            if (!configured || actor == null) return;
            Vector3 current = actor.position;
            Vector2 movement = current - previousPosition;
            previousPosition = current;
            if (SnailBossRuntime.Paused || (fieldActor != null && fieldActor.IsFieldRuntimeSuspended))
                return;
            Advance(movement, Time.deltaTime);
        }

        public void Advance(Vector2 movement, float deltaTime)
        {
            if (!configured || deltaTime <= 0) return;
            float distance = movement.magnitude;
            // Repositioning/scene transfers must not spin the walk cycle or change facing.
            if (distance > 1.5f) return;
            if (Mathf.Abs(movement.x) > .0005f) FacingRight = movement.x > 0;
            bool moving = distance > .0001f;
            if (moving) WalkPhase = Mathf.Repeat(WalkPhase + distance / CycleDistance, 1);
            ApplyPose(WalkPhase, moving);
        }

        public void ApplyPose(float phase, bool walking)
        {
            if (!configured) return;
            float wave = walking ? Mathf.Sin(phase * Mathf.PI * 2) : 0;
            facingRoot.localScale = new Vector3(FacingRight ? -1 : 1, 1, 1);
            Body.transform.localScale = new Vector3(bodyScale * (1 + wave * .055f),
                bodyScale * (1 - wave * .025f), 1);
            // Preserve grounded flesh baseline including the chocolate source's 20 px margin.
            float baseline = Chocolate ? 20f * BodyWidth / 512f : 0;
            Body.transform.localPosition = new Vector3(-wave * .012f, -.025f - baseline, 0);
            Shell.transform.localPosition = new Vector3(.10f, .04f + Mathf.Abs(wave) * .004f, 0);
            Shell.transform.localRotation = Quaternion.Euler(0, 0, wave * 1.4f);
        }
    }
}
