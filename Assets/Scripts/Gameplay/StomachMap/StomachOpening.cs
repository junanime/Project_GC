using UnityEngine;

namespace Vampire
{
    // The membrane is part of the terrain; this small, reachable trigger owns interaction.
    // Opening it reveals a passage only. It never changes the navigable boundary.
    public sealed class StomachOpening : InteractableEventObject
    {
        public const float SizeRatio = 2f / 3f;
        public static readonly Vector4 Inlet = new Vector4(.844f, .870f, .045f * SizeRatio, .070f * SizeRatio);
        public static readonly Vector4 Outlet = new Vector4(.195f, .300f, .055f * SizeRatio, .075f * SizeRatio);
        public bool Upper { get; private set; }
        public float Openness { get; private set; }
        public bool IsOpen { get; private set; }
        public Vector2 Center => map.UVToWorld(new Vector2(Shape.x, Shape.y));
        public Vector2 Radii => Vector2.Scale(new Vector2(Shape.z, Shape.w), map.WorldRect.size);
        public Vector2 Inward => Upper ? Vector2.down : Vector2.up;
        public Vector2 FrontLip => Center + Inward * Radii.y;
        Vector4 Shape => Upper ? Inlet : Outlet;
        FiniteStomachMap map;
        bool moving;

        public static void Install(FiniteStomachMap owner)
        {
            owner.TerrainMaterial.SetVector("_Inlet", Inlet);
            owner.TerrainMaterial.SetVector("_Outlet", Outlet);
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject(i == 0 ? "식도 입구 · 다섯 조각 덮개" : "십이지장 출구 · 다섯 조각 덮개");
                go.transform.SetParent(owner.transform, false);
                go.AddComponent<CircleCollider2D>().radius = 1.5f;
                var opening = go.AddComponent<StomachOpening>();
                opening.map = owner; opening.Upper = i == 0;
                go.transform.position = opening.FrontLip + opening.Inward * .65f;
            }
        }
        protected override void Awake() { base.Awake(); ConfigureReusableInteraction(); }
        protected override bool InteractionAvailable => map != null && map.StageOne && map.FieldVisible && !moving && !IsOpen;
        protected override bool KeepVisibleAfterInteraction => true;
        protected override bool ExecuteInteraction(Character player)
        {
            if (!InteractionAvailable) return false;
            IsOpen = true; moving = true;
            return true;
        }
        protected override void Update()
        {
            base.Update();
            if (!moving || levelManager.IsRunFlowPaused || MiniStageRuntimeState.IsInsideMiniStage) return;
            Openness = Mathf.MoveTowards(Openness, 1, Time.deltaTime / 1.2f);
            map.TerrainMaterial.SetFloat(Upper ? "_InletOpen" : "_OutletOpen", Mathf.SmoothStep(0, 1, Openness));
            if (Openness >= 1) moving = false;
        }

        // Solid ellipse extruded toward the OUTSIDE of the stomach: the player can reach
        // only the inlet's lower lip / outlet's upper lip, never circle around behind it.
        public static float FrontDistance(FiniteStomachMap map, Vector2 p, bool upper, out Vector2 inward)
        {
            var shape = upper ? Inlet : Outlet;
            var center = map.UVToWorld(new Vector2(shape.x, shape.y));
            var radii = Vector2.Scale(new Vector2(shape.z, shape.w), map.WorldRect.size);
            var local = p - center;
            local.y = upper ? Mathf.Min(local.y, 0) : Mathf.Max(local.y, 0);
            var q = new Vector2(local.x / radii.x, local.y / radii.y);
            inward = new Vector2(q.x / radii.x, q.y / radii.y).normalized;
            if (inward.sqrMagnitude < .001f) inward = upper ? Vector2.down : Vector2.up;
            return (q.magnitude - 1) * Mathf.Min(radii.x, radii.y);
        }
    }
}
