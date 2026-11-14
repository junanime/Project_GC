using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Native geometry keeps chain links sharp at both gameplay and overview scale.
    // Each strand is one mesh, instead of three draw calls per gold link.
    public sealed class ColonGateSealVisual : MonoBehaviour
    {
        public Vector2 Center => seal != null ? (Vector2)seal.transform.position : (Vector2)transform.position;
        public Vector2 Keyhole => Center + new Vector2(.028f, -.15f) * Height;
        public float Height { get; private set; }
        public Vector2[] Anchors { get; private set; }
        public Bounds LockBounds => seal != null ? seal.bounds : default;
        public bool Fractured { get; private set; }
        SpriteRenderer seal, stone, shackle;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Transform> fragmentRoots = new List<Transform>();
        readonly List<MeshRenderer> fragmentRenderers = new List<MeshRenderer>();
        readonly List<Vector2> fragmentStarts = new List<Vector2>();
        readonly Transform[] strandRoots = new Transform[4];
        readonly MeshRenderer[] strandRenderers = new MeshRenderer[4];
        MaterialPropertyBlock block;
        Material goldMaterial, stoneMaterial, shackleMaterial;
        Vector3 stoneScale, shackleStart;
        bool separated;

        public static ColonGateSealVisual Create(StomachOpening opening, FiniteStomachMap map)
        {
            var go = new GameObject("벽에 고정된 금 체인 · 봉인석 자물쇠");
            go.transform.SetParent(opening.transform, false);
            go.transform.position = ColonOpeningGeometry.SealCenter(map, false);
            var result = go.AddComponent<ColonGateSealVisual>();
            result.Build(opening, map);
            return result;
        }
        void Build(StomachOpening opening, FiniteStomachMap map)
        {
            block = new MaterialPropertyBlock();
            Height = Mathf.Max(3.15f, opening.Radii.y * .72f);
            var art = Resources.Load<Sprite>("ColonEnding/SealLock");
            seal = Sprite("굳은 봉인석 자물쇠", art, 490);
            if (art == null) { Debug.LogError("Missing ColonEnding/SealLock", this); return; }
            stoneScale = Vector3.one * Height / art.bounds.size.y;
            seal.transform.localScale = stoneScale;
            Anchors = new Vector2[4];
            goldMaterial = new Material(Shader.Find("Sprites/Default"));
            var anchorBuilder = new Builder();
            for (int i = 0; i < 4; i++)
            {
                Anchors[i] = ColonOpeningGeometry.ChainAnchor(map, i, false);
                Vector2 anchor = Anchors[i] - (Vector2)transform.position;
                float side = i % 2 == 0 ? -1 : 1;
                Vector2 join = new Vector2(side * Height * .11f, (i < 2 ? .17f : -.12f) * Height);
                var root = new GameObject("금 체인 " + (i + 1)).transform;
                root.SetParent(transform, false); root.localPosition = anchor;
                strandRoots[i] = root;
                var builder = new Builder();
                Vector2 end = join - anchor;
                int links = Mathf.Max(3, Mathf.CeilToInt(end.magnitude / .255f));
                for (int j = 0; j <= links; j++)
                {
                    float t = j / (float)links;
                    Vector2 center = end * t + Vector2.down * Mathf.Sin(t * Mathf.PI) * .11f;
                    Vector2 tangent = (end + Vector2.down * Mathf.Cos(t * Mathf.PI) * .11f * Mathf.PI).normalized;
                    float minor = j % 2 == 0 ? .105f : .062f;
                    builder.Ring(center, tangent, .19f, minor, .074f, new Color(.31f, .14f, .025f), false);
                    builder.Ring(center, tangent, .19f, minor, .045f, Color.white, true);
                }
                strandRenderers[i] = MeshObject(root, builder, goldMaterial, 488, "interlocking gold links");
                anchorBuilder.Ring(anchor + Vector2.down * .03f, Vector2.right, .26f, .18f, .085f, new Color(.30f, .13f, .035f, .55f), false);
                anchorBuilder.Ring(anchor, Vector2.right, .23f, .15f, .065f, Color.white, true);
                anchorBuilder.Disc(anchor + new Vector2(-.05f, .045f), .055f, new Color(1, .94f, .63f));
            }
            MeshObject(transform, anchorBuilder, goldMaterial, 489, "벽 고정 금 고리");
        }
        SpriteRenderer Sprite(string label, Sprite art, int order)
        {
            var sr = new GameObject(label).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false); sr.sprite = art;
            sr.sortingLayerName = "Monster Full"; sr.sortingOrder = order; return sr;
        }
        Material LayerMaterial(Sprite source, int part)
        {
            var shader = Resources.Load<Shader>("ColonEnding/LockLayer");
            if (shader == null) throw new InvalidOperationException("Missing ColonEnding/LockLayer shader");
            var material = new Material(shader); material.mainTexture = source.texture;
            var r = source.rect;
            material.SetVector("_SpriteRect", new Vector4(r.x / source.texture.width, r.y / source.texture.height, r.width / source.texture.width, r.height / source.texture.height));
            material.SetFloat("_Part", part); return material;
        }
        public void LiftShackle(float progress)
        {
            if (seal == null || seal.sprite == null) return;
            if (!separated)
            {
                separated = true;
                stoneMaterial = LayerMaterial(seal.sprite, 1); shackleMaterial = LayerMaterial(seal.sprite, 2);
                stone = Sprite("봉인석 몸체", seal.sprite, 490); stone.sharedMaterial = stoneMaterial; stone.transform.localScale = stoneScale;
                shackle = Sprite("풀려 올라오는 금 걸쇠", seal.sprite, 491); shackle.sharedMaterial = shackleMaterial; shackle.transform.localScale = stoneScale;
                shackleStart = shackle.transform.localPosition;
                seal.enabled = false;
            }
            float p = Mathf.SmoothStep(0, 1, progress);
            shackle.transform.localPosition = shackleStart + new Vector3(-.05f * Height * p, .12f * Height * p);
            shackle.transform.localRotation = Quaternion.Euler(0, 0, -10 * p);
        }
        public void Break(float progress)
        {
            if (!Fractured)
            {
                Fractured = true; LiftShackle(1); BuildFragments();
                if (stone != null) stone.enabled = false;
            }
            float p = Mathf.Clamp01(progress);
            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, 1, p));
            for (int i = 0; i < fragmentRoots.Count; i++)
            {
                Vector2 start = fragmentStarts[i], direction = new Vector2(start.x < 0 ? -1 : 1, 0);
                float stagger = .8f + .14f * (i % 3);
                Vector2 flight = direction * p * p * stagger + Vector2.up * (Mathf.Sin(p * Mathf.PI) * .55f - p * p);
                fragmentRoots[i].localPosition = start + flight;
                fragmentRoots[i].localRotation = Quaternion.Euler(0, 0, (i % 2 == 0 ? -1 : 1) * p * (28 + i * 11));
                Tint(fragmentRenderers[i], new Color(1, 1, 1, fade));
                if (p >= 1) fragmentRenderers[i].enabled = false;
            }
            if (shackle != null)
            {
                shackle.transform.localPosition = shackleStart + new Vector3(-.05f * Height - p * .55f, .12f * Height + Mathf.Sin(p * Mathf.PI) * .55f - p * p * .95f);
                shackle.transform.localRotation = Quaternion.Euler(0, 0, -10 - p * 65);
                shackle.color = new Color(1, 1, 1, fade);
                if (p >= 1) shackle.enabled = false;
            }
            for (int i = 0; i < strandRoots.Length; i++)
            {
                if (strandRoots[i] == null) continue;
                Vector2 anchor = Anchors[i] - (Vector2)transform.position;
                float direction = i % 2 == 0 ? 1 : -1;
                strandRoots[i].localPosition = anchor + Vector2.up * (Mathf.Sin(p * Mathf.PI) * .08f - p * p * .85f);
                strandRoots[i].localRotation = Quaternion.Euler(0, 0, direction * Mathf.Sin(p * Mathf.PI) * 18 * (1 - p));
                float chainFade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .93f, p));
                Tint(strandRenderers[i], new Color(1, 1, 1, chainFade));
                if (p >= 1) strandRenderers[i].enabled = false;
            }
        }
        void Tint(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            block.Clear(); block.SetColor("_Color", color); renderer.SetPropertyBlock(block);
        }
        void BuildFragments()
        {
            if (seal == null || seal.sprite == null) return;
            // Convex Voronoi pieces share the exact source UV. There is no rectangular
            // cutout or recolored replacement when the stone breaks apart.
            var seeds = new[] { new Vector2(.18f,.18f), new Vector2(.50f,.15f), new Vector2(.80f,.21f),
                new Vector2(.23f,.46f), new Vector2(.58f,.42f), new Vector2(.78f,.58f) };
            Sprite source = seal.sprite; Vector2 size = source.bounds.size;
            var rect = source.rect;
            for (int index = 0; index < seeds.Length; index++)
            {
                var polygon = new List<Vector2> { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                for (int j = 0; j < seeds.Length; j++)
                    if (j != index) polygon = Clip(polygon, (seeds[index] + seeds[j]) * .5f, seeds[j] - seeds[index]);
                if (polygon.Count < 3) continue;
                Vector2 center = Vector2.zero; foreach (var v in polygon) center += v; center /= polygon.Count;
                var vertices = new Vector3[polygon.Count]; var uv = new Vector2[polygon.Count]; var colors = new Color[polygon.Count];
                var triangles = new int[(polygon.Count - 2) * 3];
                for (int j = 0; j < polygon.Count; j++)
                {
                    vertices[j] = Vector2.Scale(polygon[j] - center, size) * stoneScale.x;
                    uv[j] = new Vector2((rect.x + polygon[j].x * rect.width) / source.texture.width, (rect.y + polygon[j].y * rect.height) / source.texture.height);
                    colors[j] = Color.white;
                    if (j >= 2) { int at = (j - 2) * 3; triangles[at] = 0; triangles[at + 1] = j - 1; triangles[at + 2] = j; }
                }
                var mesh = new Mesh { name = "Seal stone fracture " + index, vertices = vertices, uv = uv, colors = colors, triangles = triangles };
                mesh.RecalculateBounds(); meshes.Add(mesh);
                var piece = new GameObject("부서지는 봉인석 조각 " + index); piece.transform.SetParent(transform, false);
                Vector2 start = Vector2.Scale(center - Vector2.one * .5f, size) * stoneScale.x;
                piece.transform.localPosition = start; piece.AddComponent<MeshFilter>().sharedMesh = mesh;
                var render = piece.AddComponent<MeshRenderer>(); render.sharedMaterial = stoneMaterial;
                render.sortingLayerName = "Monster Full"; render.sortingOrder = 492 + index;
                fragmentRoots.Add(piece.transform); fragmentRenderers.Add(render); fragmentStarts.Add(start);
            }
        }
        static List<Vector2> Clip(List<Vector2> source, Vector2 midpoint, Vector2 normal)
        {
            var output = new List<Vector2>();
            for (int i = 0; i < source.Count; i++)
            {
                Vector2 a = source[i], b = source[(i + 1) % source.Count];
                float da = Vector2.Dot(a - midpoint, normal), db = Vector2.Dot(b - midpoint, normal);
                bool keepA = da <= 0, keepB = db <= 0;
                if (keepA) output.Add(a);
                if (keepA != keepB) output.Add(Vector2.Lerp(a, b, da / (da - db)));
            }
            return output;
        }
        MeshRenderer MeshObject(Transform parent, Builder builder, Material material, int order, string label)
        {
            var go = new GameObject(label); go.transform.SetParent(parent, false);
            var mesh = builder.Build(label); meshes.Add(mesh); go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingLayerName = "Monster Full"; renderer.sortingOrder = order; return renderer;
        }
        void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            if (goldMaterial != null) Destroy(goldMaterial);
            if (stoneMaterial != null) Destroy(stoneMaterial);
            if (shackleMaterial != null) Destroy(shackleMaterial);
        }
        sealed class Builder
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Color> c = new List<Color>();
            readonly List<int> triangles = new List<int>();
            public void Ring(Vector2 center, Vector2 axis, float major, float minor, float thickness, Color color, bool metal)
            {
                Vector2 normal = new Vector2(-axis.y, axis.x);
                const int steps = 20; int start = v.Count;
                for (int i = 0; i <= steps; i++)
                {
                    float a = i * Mathf.PI * 2 / steps;
                    Vector2 rim = axis * Mathf.Cos(a) * major + normal * Mathf.Sin(a) * minor;
                    Vector2 outward = (axis * Mathf.Cos(a) / major + normal * Mathf.Sin(a) / minor).normalized;
                    float light = Mathf.Clamp01(.5f + Vector2.Dot(outward, new Vector2(-.4f,.916f)) * .5f);
                    Color shade = metal ? Color.Lerp(new Color(.68f,.33f,.035f), new Color(1,.95f,.54f), Mathf.Pow(light,.65f)) : color;
                    v.Add(center + rim + outward * thickness * .5f); c.Add(shade);
                    v.Add(center + rim - outward * thickness * .5f); c.Add(metal ? Color.Lerp(shade, new Color(1,.76f,.16f), .30f) : shade);
                    if (i > 0)
                    {
                        int p = start + i * 2;
                        triangles.Add(p - 2); triangles.Add(p); triangles.Add(p - 1);
                        triangles.Add(p); triangles.Add(p + 1); triangles.Add(p - 1);
                    }
                }
            }
            public void Disc(Vector2 center, float radius, Color color)
            {
                int start = v.Count; v.Add(center); c.Add(color);
                for (int i = 0; i <= 16; i++)
                {
                    float angle = i * Mathf.PI * 2 / 16;
                    v.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius); c.Add(color);
                    if (i > 0) { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
                }
            }
            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name }; mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(triangles,0);
                mesh.SetUVs(0, new List<Vector2>(new Vector2[v.Count])); mesh.RecalculateBounds(); return mesh;
            }
        }
    }

    public sealed class ColonKeyGroundVisual : MonoBehaviour
    {
        SpriteRenderer shadow;
        LineRenderer socket, burst;
        readonly SpriteRenderer[] dust = new SpriteRenderer[10];
        Material material;
        float impactAge = 10;
        float extraction;
        public static ColonKeyGroundVisual Create(Transform owner)
        {
            var go = new GameObject("열쇠 접지 그림자와 착지 먼지"); go.transform.SetParent(owner, false);
            var result = go.AddComponent<ColonKeyGroundVisual>(); result.Initialize(); return result;
        }
        void Initialize()
        {
            material = new Material(Shader.Find("Sprites/Default"));
            shadow = Sprite("열쇠 바닥 그림자", 479); shadow.color = new Color(.19f,.08f,.10f,.24f);
            socket = Line("바닥에 박힌 자리", 521, .025f, 40); socket.loop = true;
            burst = Line("착지 충격 고리", 521, .035f, 40); burst.loop = true;
            for (int i=0;i<dust.Length;i++) { dust[i]=Sprite("착지 먼지 "+i,522); dust[i].enabled=false; }
            socket.enabled=burst.enabled=false;
        }
        SpriteRenderer Sprite(string name,int order)
        {
            var sr=new GameObject(name).AddComponent<SpriteRenderer>();sr.transform.SetParent(transform,false);
            sr.sprite=BombCircleArt.Sprite;sr.sortingLayerName="Monster Full";sr.sortingOrder=order;return sr;
        }
        LineRenderer Line(string name,int order,float width,int points)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(transform,false);
            line.sharedMaterial=material;line.useWorldSpace=false;line.positionCount=points;line.widthMultiplier=width;
            line.sortingLayerName="Monster Full";line.sortingOrder=order;return line;
        }
        public void SetAir(Vector2 ground,float height)
        {
            transform.position=ground;
            float proximity=1-Mathf.Clamp01(height/5f);
            shadow.transform.localScale=new Vector3(.42f+.3f*proximity,.13f+.06f*proximity,1);
            shadow.color=new Color(.18f,.07f,.09f,.08f+.17f*proximity);
        }
        public void Land(Vector2 point)
        {
            transform.position=point;impactAge=0;extraction=0;socket.enabled=true;
            for(int i=0;i<40;i++){float a=i*Mathf.PI*2/40;socket.SetPosition(i,new Vector3(Mathf.Cos(a)*.23f,Mathf.Sin(a)*.055f));}
            socket.startColor=socket.endColor=new Color(.31f,.12f,.12f,.65f);
        }
        public void Extract(float progress)
        {
            extraction=Mathf.Clamp01(progress);
            shadow.color=new Color(.18f,.07f,.09f,.23f*(1-extraction));
            socket.startColor=socket.endColor=new Color(.31f,.12f,.12f,.65f*(1-extraction));
        }
        public void Tick(float dt)
        {
            impactAge+=dt;
            float p=Mathf.Clamp01(impactAge/.65f),a=1-p;
            burst.enabled=p<1;
            burst.startColor=burst.endColor=new Color(.92f,.61f,.34f,a*.55f);
            for(int i=0;i<40;i++){float angle=i*Mathf.PI*2/40;burst.SetPosition(i,new Vector3(Mathf.Cos(angle)*(.18f+p*.8f),Mathf.Sin(angle)*(.07f+p*.24f)));}
            for(int i=0;i<dust.Length;i++)
            {
                dust[i].enabled=p<1;
                if(p>=1)continue;
                float angle=i*2.39996f;
                float radius=(.4f+.09f*(i%4))*p;
                dust[i].transform.localPosition=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius*.3f+Mathf.Sin(p*Mathf.PI)*(.16f+.04f*(i%3)));
                dust[i].transform.localScale=Vector3.one*(.055f+.025f*(i%3))*(1+p*.8f);
                dust[i].color=new Color(.77f,.40f,.27f,a*.65f);
            }
        }
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
