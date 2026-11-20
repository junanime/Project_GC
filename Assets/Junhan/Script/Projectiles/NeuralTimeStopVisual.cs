using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Presentation only. The controller owns the freeze deadline and all targets.
    public sealed class NeuralTimeStopVisual : MonoBehaviour
    {
        Character owner;
        Canvas hud;
        CanvasGroup hudOpacity;
        NeuralHourglassGraphic hourglass;
        MeshRenderer haze;
        Mesh hazeMesh;
        const int MarkedFootprints = 12;
        readonly HashSet<int> markedBodies = new HashSet<int>();
        readonly ParticleSystem.Particle[] settledGrains = new ParticleSystem.Particle[MarkedFootprints * 3];
        ParticleSystem grains;
        Material grainMaterial;
        bool grainsReleased;
        Material hazeMaterial;
        float age, duration, finishAge, animationTime, startedAt, animationStartedAt;
        bool telegraph, running;
        public float Remaining01 { get; private set; }
        public float Phase => age;
        public bool Visible => running;
        public Canvas Hud => hud;

        public void Prepare(Character character)
        {
            owner = character;
            EnsureVisuals();
            age = animationTime = 0; telegraph = true; running = true; Remaining01 = 1;
            startedAt = animationStartedAt = Time.time;
            hud.gameObject.SetActive(true); hudOpacity.alpha = 0;
        }
        public void Begin(float seconds)
        {
            if (owner == null) owner = GetComponentInParent<Character>();
            EnsureVisuals();
            duration = Mathf.Max(.05f, seconds); age = 0; finishAge = 0;
            // A direct activation can skip Prepare. End the gust on this exact frame.
            animationTime = running && telegraph ? Time.time - animationStartedAt : PhoenixSummonVisual.RequiemDuration;
            startedAt = Time.time;
            telegraph = false; running = true; Remaining01 = 1;
            markedBodies.Clear();
            grainsReleased = false;
            grains.Clear();
            grains.Play();
            hud.gameObject.SetActive(true);
        }
        public void Clear()
        {
            running = false; Remaining01 = 0;
            if (haze != null) haze.enabled = false;
            if (grains != null) grains.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (hud != null) hud.gameObject.SetActive(false);
        }
        void EnsureVisuals()
        {
            if (hud != null) return;
            var root = new GameObject("Time stop remaining time", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            hud = root.GetComponent<Canvas>(); hud.renderMode = RenderMode.ScreenSpaceOverlay;
            hud.sortingOrder = 190; // Beneath the game's pause/settings/reward canvas (200).
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            hudOpacity = root.GetComponent<CanvasGroup>(); hudOpacity.blocksRaycasts = false; hudOpacity.interactable = false;
            var icon = new GameObject("Mint hourglass", typeof(RectTransform), typeof(CanvasRenderer), typeof(NeuralHourglassGraphic));
            icon.transform.SetParent(root.transform, false);
            hourglass = icon.GetComponent<NeuralHourglassGraphic>(); hourglass.raycastTarget = false;
            var rect = icon.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -100); rect.sizeDelta = new Vector2(88, 140);
            var labelObject = new GameObject("Time stop label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(icon.transform, false);
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            var config = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            if (config != null && config.font != null) label.font = config.font;
            label.text = "시간 정지"; label.fontSize = 16; label.color = new Color(.96f, 1f, .88f);
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            var lr = label.rectTransform; lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(1, .15f); lr.offsetMin = lr.offsetMax = Vector2.zero;

            var world = new GameObject("Time stop sand haze", typeof(MeshFilter), typeof(MeshRenderer));
            world.transform.SetParent(transform, false);
            hazeMesh = new Mesh { name = "Time stop screen rim" };
            // Four joined strips leave the play area completely clear, including on a wide display.
            var corners = new[] { new Vector2(-1,-1), new Vector2(1,-1), new Vector2(1,1), new Vector2(-1,1) };
            var vertices = new Vector3[8];
            var uv = new Vector2[8];
            var triangles = new int[24];
            for (int i = 0; i < 4; i++)
            {
                vertices[i] = corners[i];
                vertices[i+4] = Vector2.Scale(corners[i], new Vector2(.60f, .55f));
                uv[i] = corners[i] * .5f + Vector2.one * .5f;
                uv[i+4] = (Vector2)vertices[i+4] * .5f + Vector2.one * .5f;
                int j = (i+1)%4, t = i*6;
                triangles[t]=i; triangles[t+1]=j; triangles[t+2]=j+4;
                triangles[t+3]=i; triangles[t+4]=j+4; triangles[t+5]=i+4;
            }
            hazeMesh.vertices = vertices; hazeMesh.uv = uv; hazeMesh.triangles = triangles;
            hazeMesh.RecalculateBounds();
            world.GetComponent<MeshFilter>().sharedMesh = hazeMesh;
            haze = world.GetComponent<MeshRenderer>();
            hazeMaterial = new Material(Resources.Load<Shader>("AugmentVfx/TimeSand")); haze.sharedMaterial = hazeMaterial;
            haze.sortingLayerName = "Monster Full"; haze.sortingOrder = 32760;
            haze.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; haze.receiveShadows = false;
            AugmentVfxTag.Mark(world);

            var feet = new GameObject("Sand suspended at frozen feet");
            feet.transform.SetParent(transform, false);
            grains = feet.AddComponent<ParticleSystem>();
            grains.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = grains.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = settledGrains.Length;
            main.startSpeed = 0;
            var emission = grains.emission; emission.enabled = false;
            var shape = grains.shape; shape.enabled = false;
            var drawing = grains.GetComponent<ParticleSystemRenderer>();
            grainMaterial = new Material(Resources.Load<Shader>("AugmentVfx/OpacitySprite"));
            drawing.sharedMaterial = grainMaterial;
            drawing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GroundVisualSorting.Apply(drawing, 6);
        }
        public void MarkFrozenFeet(Monster monster, float remaining)
        {
            if (!running || telegraph || markedBodies.Count >= MarkedFootprints || monster.BodyRenderer == null ||
                !markedBodies.Add(monster.GetInstanceID())) return;
            var bounds = monster.BodyRenderer.bounds;
            for (int i = 0; i < 3; i++)
            {
                float side = i - 1f;
                var dot = new ParticleSystem.EmitParams {
                    position = new Vector3(bounds.center.x + side * Mathf.Min(.42f, bounds.extents.x + .09f),
                        bounds.min.y + (i % 2 == 0 ? .16f : .055f), bounds.center.z),
                    velocity = Vector3.zero, startLifetime = remaining + .2f,
                    startSize = i == 1 ? .085f : .11f, rotation = 45f,
                    startColor = i == 1 ? new Color32(206,144,62,235) : new Color32(255,226,139,250)
                };
                grains.Emit(dot, 1);
            }
        }

        void LateUpdate()
        {
            if (!running) return;
            if (owner == null || !owner.IsAlive || !owner.gameObject.activeInHierarchy) { Clear(); return; }
            // Same scaled clock as NeuralBlockController.activeUntilTime, including pause.
            age = Time.time - startedAt;
            if (telegraph) animationTime = Time.time - animationStartedAt;
            bool reduced = GamePreferences.Current.reducedMotion;
            float opacity = GamePreferences.Current.augmentVfxOpacity;
            float strength;
            if (telegraph)
            {
                hudOpacity.alpha = 0;
                strength = Mathf.SmoothStep(0, .84f, age / PhoenixSummonVisual.RequiemDuration);
            }
            else
            {
                Remaining01 = Mathf.Clamp01(1 - age / duration);
                finishAge = Mathf.Max(0, age - duration);
                if (finishAge >= .18f) { Clear(); return; }
                float fade = 1 - Mathf.SmoothStep(0, 1, finishAge / .18f);
                hudOpacity.alpha = fade * opacity;
                hourglass.SetState(Remaining01, reduced ? 0 : age, Remaining01 <= .2f, reduced);
                strength = Mathf.SmoothStep(.84f, 1, age / Mathf.Min(.12f, duration * .2f)) * fade;
            }
            grainMaterial.SetFloat("_AugmentOpacity", hudOpacity.alpha);
            if (!telegraph && finishAge > 0 && !grainsReleased)
            {
                grainsReleased = true;
                if (!reduced)
                {
                    int count = grains.GetParticles(settledGrains);
                    for (int i = 0; i < count; i++) settledGrains[i].velocity = Vector3.down * 1.1f;
                    grains.SetParticles(settledGrains, count);
                }
            }
            var cam = Camera.main;
            haze.enabled = cam != null && strength > 0 && opacity > 0;
            if (cam == null) return;
            float distance = Mathf.Max(cam.nearClipPlane + .2f, 1f);
            float halfHeight = cam.orthographic ? cam.orthographicSize : Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * distance;
            var t = haze.transform;
            t.position = cam.transform.position + cam.transform.forward * distance; t.rotation = cam.transform.rotation;
            var parent = t.parent.lossyScale;
            t.localScale = new Vector3(halfHeight * cam.aspect / Mathf.Max(.001f, Mathf.Abs(parent.x)), halfHeight / Mathf.Max(.001f, Mathf.Abs(parent.y)), 1);
            hazeMaterial.SetFloat("_Strength", strength);
            hazeMaterial.SetFloat("_Phase", reduced ? 0 : animationTime);
            hazeMaterial.SetFloat("_Release", !telegraph && !reduced ? Mathf.Clamp01(finishAge / .18f) : 0);
        }
        void OnDisable() { Clear(); }
        void OnDestroy()
        {
            if (grains != null) Destroy(grains.gameObject);
            if (grainMaterial != null) Destroy(grainMaterial);
            if (hazeMaterial != null) Destroy(hazeMaterial);
            if (hazeMesh != null) Destroy(hazeMesh);
            if (hud != null) Destroy(hud.gameObject);
            if (haze != null) Destroy(haze.gameObject);
        }
    }
}
