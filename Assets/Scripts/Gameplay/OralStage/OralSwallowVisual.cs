using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Approved painted plates remain registered to the authored mouth. Gameplay
    // owns every vortex position/radius; the renderer never invents a second layout.
    [DisallowMultipleComponent, DefaultExecutionOrder(1250)]
    public sealed class OralSwallowVisual : MonoBehaviour
    {
        sealed class WhirlpoolView
        {
            public MeshRenderer Renderer;
            public MaterialPropertyBlock Properties = new MaterialPropertyBlock();
        }

        OralStageRuntime runtime;
        FiniteStomachMap map;
        Transform visualRoot;
        Material waterMaterial, whirlpoolMaterial, streamMaterial;
        Mesh waterMesh, unitMesh;
        MeshRenderer water;
        readonly Texture2D[] whirlpoolArt = new Texture2D[4];
        readonly List<WhirlpoolView> whirlpools = new List<WhirlpoolView>();
        readonly List<LineRenderer> streams = new List<LineRenderer>();
        readonly Vector3[] streamPoints = new Vector3[16];
        bool configured;

        static readonly int ClockId = Shader.PropertyToID("_Clock");
        static readonly int WaterId = Shader.PropertyToID("_Water");
        static readonly int SwallowId = Shader.PropertyToID("_Swallow");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int SecondTexId = Shader.PropertyToID("_SecondTex");
        static readonly int AngleId = Shader.PropertyToID("_Angle");
        static readonly int PhaseId = Shader.PropertyToID("_Phase");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        static readonly int PreviewId = Shader.PropertyToID("_Preview");

        public bool Ready => configured;
        public bool Visible => visualRoot != null && visualRoot.gameObject.activeSelf;
        public MeshRenderer WaterRenderer => water;
        public Transform VisualRoot => visualRoot;
        public int WhirlpoolVisualCount
        {
            get { int count = 0; foreach (var view in whirlpools) if (view.Renderer.enabled) count++; return count; }
        }

        public void Configure(OralStageRuntime owner)
        {
            if (configured || owner == null || owner.Map == null) return;
            runtime = owner;
            map = owner.Map;
            var waterShader = Resources.Load<Shader>("OralMap/Swallow/Water");
            var whirlpoolShader = Resources.Load<Shader>("OralMap/Swallow/Whirlpool");
            var wetArt = Resources.Load<Texture2D>("OralMap/Swallow/WaterFilled");
            var exitArt = Resources.Load<Texture2D>("OralMap/Swallow/WaterExit");
            if (waterShader == null || whirlpoolShader == null || wetArt == null || exitArt == null)
            {
                Debug.LogError("꿀꺽 이벤트의 승인된 물/소용돌이 리소스를 불러오지 못했습니다.", this);
                return;
            }

            visualRoot = new GameObject("꿀꺽 · 차오르는 물과 흐르는 소용돌이").transform;
            visualRoot.SetParent(transform, false);
            waterMaterial = new Material(waterShader) { name = "꿀꺽 · 승인 원화와 연속 수위" };
            waterMaterial.mainTexture = wetArt;
            waterMaterial.SetTexture("_ExitTex", exitArt);
            waterMesh = MakeQuad(map.WorldRect, "구강 원화와 정합된 수면");
            water = AddRenderer("협곡 → 혀 → 식도", waterMesh, waterMaterial, -850);
            unitMesh = MakeQuad(new Rect(-1, -1, 2, 2), "반경 1 소용돌이");
            whirlpoolMaterial = new Material(whirlpoolShader) { name = "꿀꺽 · 회전하는 물결과 역류 거품" };
            var center = OralMapGeometry.TongueCenter(map);
            var radii = OralMapGeometry.TongueRadii(map);
            whirlpoolMaterial.SetVector("_TongueCenter", new Vector4(center.x, center.y, 0, 0));
            whirlpoolMaterial.SetVector("_TongueRadii", new Vector4(radii.x, radii.y, 0, 0));
            whirlpoolMaterial.SetFloat("_SouthFloor", OralMapGeometry.SouthFloor(map));
            for (int i = 0; i < whirlpoolArt.Length; i++)
                whirlpoolArt[i] = Resources.Load<Texture2D>("OralMap/Swallow/Whirlpool" + (i + 1));

            streamMaterial = new Material(Resources.Load<Shader>("OralMap/Surface"));
            streamMaterial.SetFloat("_Mode", 1);
            streamMaterial.SetColor("_Color", Color.white);
            for (int i = 0; i < 32; i++)
            {
                var go = new GameObject("식도로 모이는 물살 " + i, typeof(LineRenderer));
                go.transform.SetParent(visualRoot, false);
                var line = go.GetComponent<LineRenderer>();
                line.sharedMaterial = streamMaterial;
                line.useWorldSpace = true;
                line.positionCount = streamPoints.Length;
                line.widthMultiplier = .025f + (i % 4) * .013f;
                line.numCapVertices = 3;
                line.numCornerVertices = 2;
                line.enabled = false;
                GroundVisualSorting.ApplyBackground(line, -849);
                streams.Add(line);
            }
            visualRoot.gameObject.SetActive(false);
            configured = true;
        }

        static Mesh MakeQuad(Rect rect, string name)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { new Vector3(rect.xMin, rect.yMin), new Vector3(rect.xMax, rect.yMin), new Vector3(rect.xMax, rect.yMax), new Vector3(rect.xMin, rect.yMax) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        MeshRenderer AddRenderer(string name, Mesh mesh, Material material, int order)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(visualRoot, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            GroundVisualSorting.ApplyBackground(renderer, order);
            return renderer;
        }

        void LateUpdate()
        {
            if (!configured) return;
            var finale = OralSwallowFinale.Instance;
            bool show = runtime != null && map != null && map.IsOral && finale != null && finale.Active &&
                !MiniStageRuntimeState.IsInsideMiniStage && !runtime.OnCeiling;
            visualRoot.gameObject.SetActive(show);
            if (!show) return;

            float age = finale.Age;
            water.enabled = finale.Water01 > 0;
            waterMaterial.SetFloat(ClockId, age);
            waterMaterial.SetFloat(WaterId, finale.Water01);
            waterMaterial.SetFloat(SwallowId, finale.Swallow01);
            UpdateWhirlpools(finale);
            UpdateExitStreams(finale);
        }

        void UpdateWhirlpools(OralSwallowFinale finale)
        {
            var states = finale.Whirlpools;
            int count = states != null ? states.Count : 0;
            while (whirlpools.Count < count)
            {
                int index = whirlpools.Count;
                var view = new WhirlpoolView();
                // Equal transparent sorting keys can swap when moving discs cross.
                // Keep all 47 discs above water (-850) and below the closed upper jaw (-800).
                // Each disc's layer stays stable for its entire lifetime.
                view.Renderer = AddRenderer("흡인 반경과 같은 소용돌이 " + index, unitMesh, whirlpoolMaterial, -848 + index);
                view.Properties.SetTexture(MainTexId, whirlpoolArt[index % 4]);
                view.Properties.SetTexture(SecondTexId, whirlpoolArt[(index + 1) % 4]);
                view.Properties.SetFloat(PhaseId, index * 2.39996f);
                whirlpools.Add(view);
            }

            float age = finale.Age;
            float warning = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(18, 19.4f, age));
            float appearance = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(20, 20.65f, age));
            float departure = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .43f, finale.Swallow01));
            for (int i = 0; i < whirlpools.Count; i++)
            {
                var view = whirlpools[i];
                bool show = i < count && age >= 18 && departure > 0;
                view.Renderer.enabled = show;
                if (!show) continue;
                var state = states[i];
                view.Renderer.transform.position = state.Position;
                // Visual outer radius and gameplay pull radius are identical.
                view.Renderer.transform.localScale = new Vector3(state.Radius, state.Radius, 1);
                view.Properties.SetFloat(ClockId, age);
                view.Properties.SetFloat(AngleId, state.Angle);
                view.Properties.SetFloat(OpacityId, warning * departure);
                view.Properties.SetFloat(PreviewId, 1 - appearance);
                view.Renderer.SetPropertyBlock(view.Properties);
            }
        }

        void UpdateExitStreams(OralSwallowFinale finale)
        {
            float progress = finale.Swallow01;
            bool active = finale.IsSwallowing && progress > 0;
            for (int i = 0; i < streams.Count; i++)
            {
                var line = streams[i];
                line.enabled = active;
                if (!active) continue;
                float phase = Mathf.Repeat(finale.Age * (1.0f + .045f * (i % 4)) + i * .381f, 1);
                float startY = 525 + (i * 39) % 145;
                float bowlWidth = 345 * Mathf.Sqrt(1 - Mathf.Pow((startY - 690) / 280, 2));
                float startX = 700 + (((i * 73) % 690) / 345f - 1) * bowlWidth;
                // Streams merge through the same southern painted chute instead
                // of scrolling the entire map or stretching its tissue/tooth art.
                for (int j = 0; j < streamPoints.Length; j++)
                {
                    float p = Mathf.Clamp01(phase - .09f + j * .009f);
                    float x = Mathf.Lerp(startX, 700, p * p) + Mathf.Sin(p * 6 + i) * 12 * (1 - p);
                    float y = Mathf.Lerp(startY, 1105, p);
                    streamPoints[j] = OralMapGeometry.PixelToWorld(map, new Vector2(x, y));
                }
                line.SetPositions(streamPoints);
                float alpha = Mathf.SmoothStep(0, 1, progress * 4) * Mathf.Sin(phase * Mathf.PI) * (.15f + .045f * (i % 4));
                line.startColor = new Color(.79f, 1, .98f, 0);
                line.endColor = new Color(.88f, 1, .98f, alpha);
            }
        }

        void OnDisable()
        {
            if (visualRoot != null) visualRoot.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (visualRoot != null) Destroy(visualRoot.gameObject);
            if (waterMaterial != null) Destroy(waterMaterial);
            if (whirlpoolMaterial != null) Destroy(whirlpoolMaterial);
            if (streamMaterial != null) Destroy(streamMaterial);
            if (waterMesh != null) Destroy(waterMesh);
            if (unitMesh != null) Destroy(unitMesh);
        }
    }
}
