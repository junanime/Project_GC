using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public enum KingPose { Idle, Move, Barrage, Fan, Charge, Shoot, Dash, Hook, Paper, Flush, Tribute, Eat, Shield, Summon, Phase, Death, DashCharge }

    /// <summary>
    /// Approved A movement and V3 action poses share a fixed porcelain-seat anchor.
    /// The owning encounter drives Tick, so pause, mini rooms and death never leave
    /// independently advancing animations behind.
    /// </summary>
    public sealed class ConstipationKingVisual : MonoBehaviour
    {
        public SpriteRenderer Body { get; private set; }
        public int Facing { get; private set; } = 1;
        public KingPose Pose { get; private set; }
        public float ReferenceHeight { get; private set; }
        public float Opacity { get; set; } = 1;
        public bool Visible { get; set; } = true;
        public Vector2 Mouth => PixelSocket(Pose == KingPose.Eat ? 333 : 323, Pose == KingPose.Charge || Pose == KingPose.Summon ? 248 : 239);
        public Vector2 Outlet => PixelSocket(251.878f, 437.373f);
        public Vector2 StaffTip => Pose == KingPose.Hook ? PixelSocket(428, 246)
            : Pose == KingPose.Paper && phase == 2 && progress >= .32f
            ? PixelSocket(355, 454) : Pose == KingPose.Summon || Pose == KingPose.Shield || Pose == KingPose.Phase
                ? PixelSocket(153, 52) : PixelSocket(159, 110);
        // The exit sequence uses this exact rendered prop, rather than spawning a
        // second unrelated reward at the boss's feet.
        public Vector2 KeyDropPoint => keyScepter != null ? (Vector2)keyScepter.transform.position : StaffTip;
        public bool KeyScepterReleased { get; private set; }
        public Vector2 SeatGap => PixelSocket(258, 343);
        public float LeanDegrees => lean;
        public int CentralJetCount => waterRenderer != null && waterRenderer.enabled ? 1 : 0;
        public int DisplayedPoseIndex { get; private set; } = -1;
        public float AnimationTime => clock;

        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static Material fluidMaterial;
        Transform pivot;
        SpriteRenderer outlet, staffFoot, shadow, keyScepter, staffHand;
        Material staffBodyMaterial, staffHandMaterial;
        readonly SpriteRenderer[] booster = new SpriteRenderer[2];
        readonly SpriteRenderer[] drops = new SpriteRenderer[7];
        readonly SpriteRenderer[] foam = new SpriteRenderer[5];
        readonly LineRenderer[] ribbons = new LineRenderer[3];
        readonly LineRenderer[] ripples = new LineRenderer[3];
        readonly LineRenderer[] trail = new LineRenderer[8];
        readonly Vector2[] trailGround = new Vector2[8];
        readonly float[] trailAge = new float[8];
        Mesh waterMesh;
        MeshRenderer waterRenderer;
        const int WaterSteps = 36;
        readonly Vector3[] vertices = new Vector3[(WaterSteps + 1) * 2];
        readonly Color[] colors = new Color[(WaterSteps + 1) * 2];
        float unit, sceneUnit, clock, poseAge, progress, flash, lean, travel, trailTimer, hoverCompression;
        bool moving;
        int phase = 1, trailCursor;
        Vector2 facingDirection = Vector2.right;

        public static Sprite Get(string key)
        {
            if (sprites.TryGetValue(key, out var found) && found != null) return found;
            string sheet = key == "Outlet" ? "Body" : key == "StaffFoot6" ? "Pose6"
                : key.StartsWith("Tribute", StringComparison.Ordinal) ? "Tribute"
                : key.StartsWith("Booster", StringComparison.Ordinal) ? "Booster" : key;
            foreach (var sprite in Resources.LoadAll<Sprite>("ConstipationKing/" + sheet)) sprites[sprite.name] = sprite;
            sprites.TryGetValue(key, out found);
            return found;
        }

        public static ConstipationKingVisual Create(Transform parent, float size = 4.8f)
        {
            var go = new GameObject("변비왕 · approved A central water throne");
            go.transform.SetParent(parent, false);
            var visual = go.AddComponent<ConstipationKingVisual>();
            visual.Initialize(size);
            return visual;
        }

        void Initialize(float size)
        {
            ReferenceHeight = size;
            unit = size / 380f;
            sceneUnit = unit / .6f;
            pivot = new GameObject("Fixed pedestal lean pivot").transform;
            pivot.SetParent(transform, false);
            Body = Sprite(pivot, "Approved body · fixed scale", Get("Body"), 510);
            outlet = Sprite(pivot, "Single central A outlet", Get("Outlet"), 511);
            staffFoot = Sprite(pivot, "Authored staff foot below crop", Get("StaffFoot6"), 511);
            keyScepter = Sprite(pivot, "Royal toilet-paper key scepter", Resources.Load<Sprite>("ColonEnding/KeyScepter"), 512);
            var staffShader = Resources.Load<Shader>("ConstipationKing/SeparateScepter");
            if (keyScepter.sprite != null && staffShader != null)
            {
                staffBodyMaterial = new Material(staffShader);
                staffHandMaterial = new Material(staffShader);
                staffHandMaterial.SetFloat("_HandOnly", 1);
                Body.sharedMaterial = staffBodyMaterial;
                staffHand = Sprite(pivot, "Original gripping fingers above key shaft", Body.sprite, 513);
                staffHand.sharedMaterial = staffHandMaterial;
            }
            shadow = Sprite(transform, "Ground contact shadow", BombCircleArt.Sprite, 478);
            for (int i = 0; i < booster.Length; i++) booster[i] = Sprite(transform, "Hydrant water booster " + i, Get("Booster0"), 503);
            for (int i = 0; i < drops.Length; i++) drops[i] = Sprite(transform, "Water landing droplet " + i, BombCircleArt.Sprite, 508);
            for (int i = 0; i < foam.Length; i++) foam[i] = Sprite(transform, "Single landing crest " + i, BombCircleArt.Sprite, 508);
            for (int i = 0; i < ribbons.Length; i++) ribbons[i] = Line("Short flowing water highlight " + i, 507, 15);
            for (int i = 0; i < ripples.Length; i++) ripples[i] = Line("Landing ripple " + i, 499, 33);
            for (int i = 0; i < trail.Length; i++) { trail[i] = Line("Residual floor ripple " + i, 498, 33); trailAge[i] = 10; }
            var jet = new GameObject("Exactly one connected A water column");
            jet.transform.SetParent(transform, false);
            waterMesh = new Mesh { name = "Single central pressure-water silhouette" };
            waterMesh.MarkDynamic();
            jet.AddComponent<MeshFilter>().sharedMesh = waterMesh;
            waterRenderer = jet.AddComponent<MeshRenderer>();
            waterRenderer.sharedMaterial = Material;
            waterRenderer.sortingOrder = 505;
            var triangles = new int[WaterSteps * 6];
            for (int i = 0; i < WaterSteps; i++)
            {
                int a = i * 2, j = i * 6;
                triangles[j] = a; triangles[j + 1] = a + 1; triangles[j + 2] = a + 2;
                triangles[j + 3] = a + 1; triangles[j + 4] = a + 3; triangles[j + 5] = a + 2;
            }
            waterMesh.vertices = vertices;
            waterMesh.triangles = triangles;
            Sample();
        }

        static Material Material => fluidMaterial != null ? fluidMaterial : fluidMaterial = new Material(Shader.Find("Sprites/Default"));
        static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, int order)
        {
            var r = new GameObject(name).AddComponent<SpriteRenderer>();
            r.transform.SetParent(parent, false); r.sprite = sprite; r.sortingOrder = order; return r;
        }
        LineRenderer Line(string name, int order, int count)
        {
            var r = new GameObject(name).AddComponent<LineRenderer>(); r.transform.SetParent(transform, false);
            r.sharedMaterial = Material; r.useWorldSpace = false; r.positionCount = count;
            r.numCapVertices = 3; r.numCornerVertices = 2; r.sortingOrder = order; return r;
        }

        public void Face(Vector2 direction)
        {
            int previousFacing = Facing;
            if (direction.sqrMagnitude > .001f) facingDirection = direction.normalized;
            if (Mathf.Abs(direction.x) > .03f) Facing = direction.x < 0 ? -1 : 1;
            if (Facing != previousFacing) Sample();
        }
        public void SetMoving(bool value) { moving = value; }
        public void SetPhase(int value) { phase = value >= 2 ? 2 : 1; }
        public void SetPose(KingPose value)
        {
            if (Pose == value) return;
            Pose = value; poseAge = 0; progress = 0; Sample();
        }
        public void SetPoseProgress(float value) { progress = Mathf.Clamp01(value); Sample(); }
        public void Flash() { flash = .10f; }
        public void ReleaseKeyScepter()
        {
            KeyScepterReleased = true;
            if (keyScepter != null) keyScepter.enabled = false;
        }
        public void ResetPose() { SetPose(KingPose.Idle); moving = false; progress = 0; Opacity = 1; Visible = true; }
        public void Tick(float dt)
        {
            if (dt <= 0) return;
            clock += dt; poseAge += dt; flash = Mathf.Max(0, flash - dt);
            travel = Mathf.MoveTowards(travel, moving ? 1 : 0, dt * 3);
            float slam = Pose == KingPose.Paper && phase == 2
                ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.18f, .4f, progress)) : 0;
            hoverCompression = Mathf.MoveTowards(hoverCompression, slam, dt * 8);
            float desiredLean = Pose == KingPose.Death ? -Facing * 12 * progress : -Facing * 5 * travel;
            lean = Mathf.MoveTowards(lean, desiredLean, dt * 18);
            if (moving && Pose != KingPose.Death)
            {
                trailTimer += dt;
                if (trailTimer >= .15f)
                {
                    trailTimer = 0; int i = trailCursor++ % trail.Length;
                    trailGround[i] = (Vector2)transform.position + new Vector2(Facing * (11.878f * unit - 14 * sceneUnit * travel), 2 * sceneUnit);
                    trailAge[i] = 0;
                }
            }
            for (int i = 0; i < trailAge.Length; i++) trailAge[i] += dt;
            Sample();
        }

        Vector2 PixelSocket(float x, float y)
        {
            if (pivot == null) return transform.position;
            return pivot.TransformPoint(new Vector3((x - 250) * Facing * unit, (426 - y) * unit));
        }
        int ResolvePose()
        {
            switch (Pose)
            {
                case KingPose.Barrage: case KingPose.Fan: case KingPose.Shoot: return 1;
                case KingPose.Charge: case KingPose.Tribute: return 2;
                case KingPose.DashCharge: return 4;
                case KingPose.Hook: return 3;
                case KingPose.Paper: return phase == 2 ? (progress < .32f ? 5 : 6) : (progress < .65f ? 3 : -1);
                case KingPose.Flush: return progress < .22f ? 8 : phase == 2 && progress >= .45f ? 10 : 9;
                case KingPose.Eat: return 7;
                case KingPose.Shield: return poseAge < .75f ? 5 : -1;
                case KingPose.Summon: return 5;
                case KingPose.Phase: return progress < .3f ? 4 : progress < .8f ? 5 : -1;
                case KingPose.Death: return 4;
                default: return -1;
            }
        }
        void Sample()
        {
            if (Body == null) return;
            float alpha = Visible ? Mathf.Clamp01(Opacity) : 0;
            int pose = ResolvePose(); DisplayedPoseIndex = pose;
            Body.sprite = Get(pose < 0 ? "Body" : "Pose" + pose);
            float lift = (18 + 2.4f * Mathf.Sin(clock * Mathf.PI * 2 / 2.8f)) * sceneUnit;
            // The phase-two staff slam settles the throne toward the floor, as in
            // V3. The central outlet still sits above ground and keeps one short
            // jet; leaving/cancelling Paper smoothly restores normal A hovering.
            lift *= 1 - hoverCompression;
            if (Pose == KingPose.Death) lift *= 1 - progress;
            // Authored leaning rotates around the porcelain pedestal, keeping fluid attached.
            pivot.localPosition = new Vector3(10 * Facing * unit, 40 * unit + lift, 0);
            float gesture = Pose == KingPose.Eat ? Mathf.Sin(poseAge * 14) * .35f
                : Pose == KingPose.Summon ? Mathf.Sin(poseAge * 9) * .5f
                : Pose == KingPose.Flush ? -Facing * Mathf.Sin(progress * Mathf.PI) * 1.2f : 0;
            pivot.localRotation = Quaternion.Euler(0, 0, lean + gesture);
            Body.transform.localScale = Vector3.one * unit * 100;
            Body.transform.localPosition = new Vector3(-10 * Facing * unit, (pose < 0 ? -40 : -10) * unit, 0);
            Body.flipX = Facing < 0; Body.enabled = alpha > 0;
            Body.color = flash > 0 ? new Color(1, .74f, .72f, alpha) : new Color(1, 1, 1, alpha);
            outlet.enabled = pose >= 0 && alpha > 0;
            outlet.transform.localScale = Vector3.one * unit * 100;
            outlet.transform.localPosition = new Vector3((660f / 1254 * 480 - 250) * Facing * unit, (426 - 1143.5f / 1254 * 480) * unit, 0);
            outlet.flipX = Facing < 0; outlet.color = Body.color;
            staffFoot.enabled = pose == 6 && alpha > 0 && staffBodyMaterial == null;
            staffFoot.transform.localScale = Vector3.one * unit * 100;
            staffFoot.transform.localPosition = new Vector3(155 * Facing * unit, -54 * unit, 0);
            staffFoot.flipX = Facing < 0; staffFoot.color = Body.color;
            DrawKeyScepter(pose, alpha);
            ScaleEllipse(shadow, new Vector2(6 * sceneUnit, 0), 77 * sceneUnit, 14 * sceneUnit, new Color(.20f, .11f, .15f, .20f * alpha));
            DrawWater(alpha * (Pose == KingPose.Death ? 1 - progress : 1));
            DrawBooster(alpha);
        }

        void DrawKeyScepter(int pose, float alpha)
        {
            if (keyScepter == null || keyScepter.sprite == null || staffBodyMaterial == null) return;
            Vector2 top = new Vector2(157, 58), bottom = new Vector2(186, 339);
            Vector4 head = new Vector4(157, 115, 55, 65), hand = new Vector4(173, 238, 25, 28);
            Vector4 shaft = new Vector4(166, 164, 186, 332);
            if (pose == 5)
            {
                top = new Vector2(146, 24); bottom = new Vector2(115, 269);
                head = new Vector4(137, 80, 55, 66); hand = new Vector4(130, 166, 25, 27);
                shaft = new Vector4(138, 127, 116, 258);
            }
            else if (pose == 6)
            {
                top = new Vector2(380, 235); bottom = new Vector2(351, 460);
                head = new Vector4(384, 296, 51, 80); hand = new Vector4(345, 346, 31, 28);
                shaft = new Vector4(354, 330, 353, 451);
            }
            else if (pose == 8 || pose == 9)
            {
                top = new Vector2(159, 60); bottom = new Vector2(190, 321);
                head = new Vector4(161, 115, 48, 57); hand = new Vector4(178, 226, 23, 24);
                shaft = new Vector4(172, 165, 190, 310);
            }
            else if (pose == 10)
            {
                top = new Vector2(176, 88); bottom = new Vector2(149, 339);
                head = new Vector4(158, 143, 51, 63); hand = new Vector4(-999, -999, 1, 1);
                shaft = new Vector4(175, 185, 150, 328);
            }
            SetStaffMaterial(staffBodyMaterial, head, shaft, hand);
            SetStaffMaterial(staffHandMaterial, head, shaft, hand);
            Vector2 center = (top + bottom) * .5f;
            keyScepter.transform.localPosition = new Vector3((center.x - 250) * Facing * unit, (426 - center.y) * unit);
            Vector2 axis = new Vector2((top.x - bottom.x) * Facing, bottom.y - top.y);
            keyScepter.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg - 90);
            keyScepter.transform.localScale = Vector3.one * (axis.magnitude * unit / keyScepter.sprite.bounds.size.y);
            keyScepter.flipX = Facing < 0;
            keyScepter.color = Body.color;
            keyScepter.enabled = alpha > 0 && !KeyScepterReleased;
            staffHand.sprite = Body.sprite;
            staffHand.transform.localPosition = Body.transform.localPosition;
            staffHand.transform.localScale = Body.transform.localScale;
            staffHand.flipX = Body.flipX; staffHand.color = Body.color;
            staffHand.enabled = alpha > 0 && pose != 10;
        }

        static void SetStaffMaterial(Material material, Vector4 head, Vector4 shaft, Vector4 hand)
        {
            material.SetVector("_StaffHead", head);
            material.SetVector("_StaffShaft", shaft);
            material.SetVector("_StaffHand", hand);
        }

        Vector2 WaterCenter(float q, Vector2 top, Vector2 ground)
        {
            return Vector2.Lerp(top, ground, q) + Vector2.right * (Mathf.Sin(q * Mathf.PI) * Mathf.Sin(clock * 7.5f - q * 5.2f) * sceneUnit);
        }
        float WaterRadius(float q) => (Mathf.Lerp(12, 19, Mathf.Pow(q, 1.1f)) + Mathf.Sin(q * Mathf.PI) * Mathf.Sin(clock * 7.5f - q * 10) * .7f) * (1 + travel * .055f) * sceneUnit;

        void DrawWater(float alpha)
        {
            Vector2 top = transform.InverseTransformPoint(Outlet);
            Vector2 ground = new Vector2(Facing * (11.878f * unit - 14 * sceneUnit * travel), 2 * sceneUnit);
            waterRenderer.enabled = alpha > .001f;
            for (int k = 0; k <= WaterSteps; k++)
            {
                float q = k / (float)WaterSteps; var p = WaterCenter(q, top, ground); float r = WaterRadius(q);
                vertices[k * 2] = p + Vector2.left * r; vertices[k * 2 + 1] = p + Vector2.right * r;
                Color color = Color.Lerp(new Color(.71f, .93f, .94f), new Color(.40f, .73f, .82f), Mathf.Sin(q * Mathf.PI));
                color.a = alpha; colors[k * 2] = color;
                color = Color.Lerp(color, new Color(.83f, .96f, .93f, alpha), .2f); colors[k * 2 + 1] = color;
            }
            waterMesh.vertices = vertices; waterMesh.colors = colors; waterMesh.RecalculateBounds();
            for (int k = 0; k < ribbons.Length; k++)
            {
                float end = Mathf.Repeat(clock * 2 + k / 3f, 1), start = Mathf.Max(0, end - .2f);
                var line = ribbons[k]; line.enabled = alpha > 0;
                Color c = k % 2 == 0 ? new Color(.75f, .96f, .96f) : new Color(.91f, 1, .98f);
                c.a = alpha * (.45f + .3f * Mathf.Sin(end * Mathf.PI)); line.startColor = line.endColor = c;
                line.startWidth = line.endWidth = 2.3f * sceneUnit;
                for (int j = 0; j < line.positionCount; j++)
                {
                    float q = Mathf.Lerp(start, end, j / (float)(line.positionCount - 1));
                    var p = WaterCenter(q, top, ground); p.x += WaterRadius(q) * (Mathf.Sin(k * 2.39996f) * .55f + Mathf.Sin(q * 12 - clock * 6 + k) * .13f);
                    line.SetPosition(j, p);
                }
            }
            for (int k = 0; k < ripples.Length; k++)
            {
                float q = Mathf.Repeat(clock * 1.35f + k / 3f, 1);
                Ellipse(ripples[k], ground + Vector2.left * Facing * travel * q * 7 * sceneUnit,
                    (10.8f + q * 22.8f) * sceneUnit, (2.5f + q * 5.5f) * sceneUnit,
                    new Color(.84f, .97f, .96f, .44f * (1 - q) * alpha), 1.25f * sceneUnit);
            }
            for (int k = 0; k < foam.Length; k++)
            {
                float q = k / 4f, y = (Mathf.Sin(q * Mathf.PI) * 1.6f - 1.1f + Mathf.Sin(clock * 6 + k * 1.3f) * .55f) * sceneUnit;
                ScaleEllipse(foam[k], ground + new Vector2((q - .5f) * 19 * 1.85f * sceneUnit, -y),
                    (2.676f + Mathf.Sin(clock * 5 + k) * .55f) * sceneUnit, 1.76f * sceneUnit, new Color(.93f, 1, .96f, .78f * alpha));
            }
            for (int k = 0; k < drops.Length; k++)
            {
                float q = Mathf.Repeat(clock * 1.6f + k / 7f, 1), a = k * 2.399963f;
                Vector2 start = ground + new Vector2(Mathf.Cos(a) * 19 * .43f, Mathf.Sin(a) * 2) * sceneUnit;
                float vx = (Mathf.Cos(a) * 13.64f - Facing * travel * 6) * sceneUnit, peak = (7.34f + k % 3 * 1.4f) * sceneUnit;
                Vector2 p = start + new Vector2(vx * q, 4 * peak * q * (1 - q));
                float fade = Mathf.SmoothStep(0, 1, q / .08f) * (1 - Mathf.SmoothStep(0, 1, (q - .7f) / .3f));
                ScaleEllipse(drops[k], p, (1.1f + k % 3 * .34f) * sceneUnit, (1.25f + k % 2 * .34f) * sceneUnit,
                    new Color(.92f, 1, .98f, fade * .765f * alpha));
            }
            for (int i = 0; i < trail.Length; i++)
            {
                float q = Mathf.Clamp01(trailAge[i] / .85f);
                Vector2 at = transform.InverseTransformPoint(trailGround[i]);
                Ellipse(trail[i], at, (12 + q * 25) * sceneUnit, (3 + q * 5) * sceneUnit,
                    new Color(.84f, .98f, .97f, .28f * (1 - q) * alpha), 1.2f * sceneUnit);
            }
        }

        void DrawBooster(float alpha)
        {
            bool active = Pose == KingPose.Dash && moving;
            float frame = Mathf.Repeat(clock * 9, 4), blend = frame - Mathf.Floor(frame);
            for (int i = 0; i < booster.Length; i++)
            {
                var r = booster[i]; r.enabled = active && alpha > 0;
                if (!active) continue;
                r.sprite = Get("Booster" + ((Mathf.FloorToInt(frame) + i) % 4));
                r.transform.position = Outlet - facingDirection * .15f;
                r.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg);
                r.transform.localScale = Vector3.one * (.46f * sceneUnit * 100);
                r.color = new Color(1, 1, 1, alpha * (i == 0 ? 1 - blend : blend));
            }
        }
        static void ScaleEllipse(SpriteRenderer r, Vector2 at, float rx, float ry, Color color)
        {
            r.enabled = color.a > .001f; r.color = color; r.transform.localPosition = at;
            if (r.sprite != null) r.transform.localScale = new Vector3(rx * 2 / r.sprite.bounds.size.x, ry * 2 / r.sprite.bounds.size.y, 1);
        }
        static void Ellipse(LineRenderer r, Vector2 at, float rx, float ry, Color color, float width)
        {
            r.enabled = color.a > .001f; r.startColor = r.endColor = color; r.startWidth = r.endWidth = width;
            for (int i = 0; i < r.positionCount; i++)
            {
                float a = i * Mathf.PI * 2 / (r.positionCount - 1);
                r.SetPosition(i, at + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry));
            }
        }
        void OnDestroy()
        {
            if (staffBodyMaterial != null) { if (Application.isPlaying) Destroy(staffBodyMaterial); else DestroyImmediate(staffBodyMaterial); }
            if (staffHandMaterial != null) { if (Application.isPlaying) Destroy(staffHandMaterial); else DestroyImmediate(staffHandMaterial); }
            if (waterMesh == null) return;
            if (Application.isPlaying) Destroy(waterMesh); else DestroyImmediate(waterMesh);
        }
    }
}
