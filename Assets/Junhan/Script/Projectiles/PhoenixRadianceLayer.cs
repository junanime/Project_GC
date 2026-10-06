using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // A single premultiplied-alpha draw interpolates two painted poses. No doubled
    // translucent silhouettes and no runtime texture copies or per-frame allocations.
    public sealed class PhoenixRadianceLayer
    {
        static Mesh quad;
        static Material material;
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        public readonly MeshRenderer Renderer;
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        readonly string sheet;
        static readonly int MainTex = Shader.PropertyToID("_MainTex"), Tint = Shader.PropertyToID("_Color"),
            Frames = Shader.PropertyToID("_Frames"), Motion = Shader.PropertyToID("_Motion"), Anchors = Shader.PropertyToID("_Anchors");
        public PhoenixRadianceLayer(Transform parent, string sheet)
        {
            this.sheet = sheet;
            if (quad == null)
            {
                quad = new Mesh { name = "Phoenix radiance shared quad" };
                quad.vertices = new[] { new Vector3(-.5f,-.5f), new Vector3(.5f,-.5f), new Vector3(-.5f,.5f), new Vector3(.5f,.5f) };
                quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
                quad.triangles = new[] {0,2,1,1,2,3}; quad.RecalculateBounds();
            }
            if (material == null) material = new Material(Resources.Load<Shader>("PhoenixRadiance/PhoenixRadiance")) { name = "Phoenix radiance shared" };
            if (!textures.TryGetValue(sheet, out var texture) || texture == null)
                textures[sheet] = texture = Resources.Load<Texture2D>("PhoenixRadiance/" + sheet);
            var go = new GameObject("Radiance " + sheet, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer;
            go.GetComponent<MeshFilter>().sharedMesh = quad;
            Renderer = go.GetComponent<MeshRenderer>(); Renderer.sharedMaterial = material;
            Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
            properties.SetTexture(MainTex, texture);
        }
        public void Hide() { Renderer.enabled = false; }
        public void Draw(Vector3 position, Vector2 size, float angle, float frame, float alpha,
            SpriteRenderer sorting, int order, float phase, float sway = 0, float reveal = 1, float density = 0, Color? tint = null)
        {
            Renderer.enabled = alpha > .001f;
            if (!Renderer.enabled) return;
            var t = Renderer.transform;
            float canvas = sheet == "CloneWings" ? 2f : 1f;
            size *= canvas;
            // Child inherits the carrier lifetime, but not its squash, pivot or weapon scale.
            t.position = position; t.rotation = Quaternion.Euler(0,0,angle);
            var parentScale = t.parent.lossyScale;
            t.localScale = new Vector3(size.x / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)),
                size.y / Mathf.Max(.0001f, Mathf.Abs(parentScale.y)), 1);
            Renderer.sortingLayerID = sorting != null ? sorting.sortingLayerID : SortingLayer.NameToID("Monster Full");
            Renderer.sortingOrder = (sorting != null ? sorting.sortingOrder : 0) + order;
            frame = Mathf.Clamp(frame,0,11); int a = (int)frame, b = Mathf.Min(a+1,11);
            properties.SetVector(Frames,new Vector4(a,b,frame-a,phase));
            var color = tint ?? Color.white; color.a *= alpha; properties.SetColor(Tint,color);
            properties.SetVector(Motion,new Vector4(sway,reveal,density,canvas));
            // The painted wing attachment point is lower during upstroke. Align it to
            // the clone's back, instead of letting the entire pair bounce between rows.
            properties.SetVector(Anchors, sheet == "CloneWings" ? new Vector4(0,WingAnchor(a),0,WingAnchor(b)) : Vector4.zero);
            Renderer.SetPropertyBlock(properties);
        }
        static float WingAnchor(int frame)
        {
            switch(frame) { case 0: case 1: case 2: case 3: return -.335f;
                case 4:return -.08f; case 5:case 6:case 7:return -.025f;
                case 8:case 9:return .145f; case 10:return -.065f; default:return -.115f; }
        }
        public static float LoopFrame(float time, float fps = 12) => Mathf.PingPong(time * fps, 11);
    }
}
