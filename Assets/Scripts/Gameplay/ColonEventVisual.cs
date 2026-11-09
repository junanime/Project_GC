using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    /// <summary>Continuous, floor-clipped effects; all time comes from the event owner.</summary>
    public sealed class ColonEventVisual : MonoBehaviour
    {
        ColonFieldEvents owner;
        Transform root;
        sealed class Wisp { public Vector2 at; public float life, seed; public LineRenderer line; }
        readonly List<Wisp> wisps = new List<Wisp>();
        readonly List<Renderer> floor = new List<Renderer>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Vector2> lane = new List<Vector2>();
        float lastClock;
        static Material material;
        public static Material Material => material != null ? material : material = new Material(Shader.Find("Sprites/Default"));
        public void Configure(ColonFieldEvents value) { owner = value; Clear(); }
        public static LineRenderer Line(Transform parent, string name, float width, Color color, bool ground = true)
        {
            var r = new GameObject(name).AddComponent<LineRenderer>(); r.transform.SetParent(parent, false);
            r.sharedMaterial = Material; r.useWorldSpace = true; r.widthMultiplier = width; r.numCapVertices = 3; r.numCornerVertices = 3;
            r.startColor = r.endColor = color; r.sortingOrder = 12;
            if (ground) GroundVisualSorting.Apply(r, 12); return r;
        }
        public static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, Vector2 at, float height)
        {
            var r = new GameObject(name).AddComponent<SpriteRenderer>(); r.transform.SetParent(parent, false); r.sprite = sprite;
            r.transform.position = at; if (sprite != null) r.transform.localScale = Vector3.one * height / sprite.bounds.size.y;
            r.sortingOrder = 6; return r;
        }
        public void Rebuild()
        {
            Clear(); root = new GameObject("대장 전용 필드 이벤트 연출").transform; root.SetParent(transform, false); lastClock = owner.Clock;
            if (owner.Current == ColonEventKind.Expulsion)
            {
                for (int i = 0; i < 110; i++)
                { var w = new Wisp { seed = i * .618f, line = Line(root, "통로 기류", i % 5 == 0 ? .13f : .025f, Color.clear, false) };
                    w.line.sortingOrder = 80; wisps.Add(w); ResetWisp(w); }
            }
            if (owner.Current == ColonEventKind.Absorption) foreach (var p in owner.DryCenters) DryPatch(p);
            if (owner.Current == ColonEventKind.Residue)
            {
                Vector2 at = owner.Origin;
                for (int i = 0; i < 45; i++) { lane.Add(at); var dir = owner.Flow.Direction(at); at = ColonAttackWall.Stop(at, at + dir * .2f, .4f); }
                var rail = Line(root, "찌꺼기 이동 예고", .035f, new Color(.98f, .74f, .28f, .8f));
                rail.positionCount = lane.Count; for (int i = 0; i < lane.Count; i++) rail.SetPosition(i, lane[i]); floor.Add(rail);
                for (int i = 3; i < lane.Count - 1; i += 6)
                { var d = (lane[i + 1] - lane[i]).normalized; var n = new Vector2(-d.y, d.x); var arrow = Line(root, "이동 방향", .04f, new Color(1, .87f, .5f, .8f));
                    arrow.positionCount = 3; arrow.SetPositions(new[] { (Vector3)(lane[i] - d * .16f + n * .14f), (Vector3)lane[i], (Vector3)(lane[i] - d * .16f - n * .14f) }); floor.Add(arrow); }
            }
        }
        void ResetWisp(Wisp w)
        {
            var camera = Camera.main; Vector2 center = camera != null ? (Vector2)camera.transform.position : owner.Player.Position;
            float h = camera != null ? camera.orthographicSize : 4; float width = h * (camera != null ? camera.aspect : 1.8f);
            for (int k = 0; k < 30; k++)
            { w.at = center + new Vector2(Random.Range(-width - 1, width + 1), Random.Range(-h - 1, h + 1));
                if (owner.Map.FloorClearance(w.at) > .4f && owner.Flow.Direction(w.at).sqrMagnitude > .1f) break; }
            w.life = Random.Range(.4f, 1.8f);
        }
        void LateUpdate()
        {
            if (root == null || owner == null) return;
            root.gameObject.SetActive(!MiniStageRuntimeState.IsInsideMiniStage && owner.Map != null && owner.Map.FieldVisible);
            float dt = owner.Clock - lastClock; lastClock = owner.Clock;
            if (owner.Paused) return;
            float strength = owner.Strength;
            foreach (var w in wisps)
            {
                w.life -= dt; Vector2 dir = owner.Flow.Direction(w.at);
                Vector2 next = ColonAttackWall.Stop(w.at, w.at + dir * dt * (4.5f + strength * 5), .15f);
                var camera = Camera.main;
                if (w.life <= 0 || (next - w.at).sqrMagnitude < .000001f || camera != null && Vector2.Distance(next, camera.transform.position) > camera.orthographicSize * camera.aspect + 4) ResetWisp(w);
                else w.at = next;
                Vector2 p = w.at; w.line.positionCount = 8;
                float len = .35f + .5f * Mathf.Repeat(w.seed, 1);
                for (int k = 0; k < 8; k++)
                { w.line.SetPosition(k, p); var step = owner.Flow.Direction(p); p = ColonAttackWall.Stop(p, p - step * len / 7, .12f); }
                float a = strength * Mathf.Clamp01(w.life * 3) * (w.line.widthMultiplier > .1f ? .14f : .58f);
                w.line.startColor = new Color(.78f, 1, .92f, 0); w.line.endColor = new Color(1, 1, .88f, a);
            }
            foreach (var r in floor)
            {
                if (r is LineRenderer line)
                {
                    float alpha = owner.Current == ColonEventKind.Residue ? (owner.Age < ColonFieldEvents.WarningSeconds ? .55f + .3f * Mathf.Sin(owner.Age * 6) : .22f * strength) : .36f * strength;
                    var c = line.startColor; c.a = Mathf.Max(0, alpha); line.startColor = line.endColor = c;
                }
                else
                { var block = new MaterialPropertyBlock(); block.SetColor("_Color", new Color(1, 1, 1, strength * .78f)); r.SetPropertyBlock(block); }
            }
        }
        void DryPatch(Vector2 center)
        {
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var indices = new List<int>();
            float radius = owner.DryRadius; const float step = .06f;
            for (float y = -radius; y < radius; y += step) for (float x = -radius; x < radius; x += step)
            {
                var p = center + new Vector2(x, y); float d = new Vector2(x, y / .78f).magnitude;
                if (d > radius || owner.Map.FloorClearance(p) < .25f) continue;
                float grain = Mathf.Floor(Mathf.PerlinNoise(p.x * 1.8f, p.y * 1.8f) * 4) / 3;
                float edge = Mathf.Clamp01((radius - d) / .32f);
                Color c = Color.Lerp(new Color(.79f, .52f, .36f, edge), new Color(.91f, .67f, .47f, edge), grain);
                int n = vertices.Count;
                vertices.Add(p); vertices.Add(p + Vector2.right * step); vertices.Add(p + Vector2.one * step); vertices.Add(p + Vector2.up * step);
                for (int k = 0; k < 4; k++) colors.Add(c);
                indices.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds(); meshes.Add(mesh);
            var go = new GameObject("수분이 빠진 바닥"); go.transform.SetParent(root, false); go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = Material; GroundVisualSorting.Apply(r, 2); floor.Add(r);
            for (int k = 0; k < 7; k++)
            {
                float a = k * 2.399f; var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * .72f) * radius * .48f;
                var crack = Line(root, "작은 건조 균열", .022f, new Color(.36f, .22f, .18f, 0)); crack.positionCount = 4;
                for (int j = 0; j < 4; j++) crack.SetPosition(j, p + new Vector2((j - 1.5f) * .13f, Mathf.Sin(k + j * 2) * .08f)); floor.Add(crack);
            }
        }
        public void Clear()
        {
            if (root != null) Destroy(root.gameObject); root = null;
            foreach (var m in meshes) if (m != null) Destroy(m); meshes.Clear(); wisps.Clear(); floor.Clear(); lane.Clear();
        }
        void OnDestroy() { Clear(); }
    }
}
