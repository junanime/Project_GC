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
            startedAt = Time.time;
            telegraph = false; running = true; Remaining01 = 1;
            hud.gameObject.SetActive(true);
        }
        public void Clear()
        {
            running = false; Remaining01 = 0;
            if (haze != null) haze.enabled = false;
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
            hazeMesh = new Mesh { name = "Time stop camera quad" };
            hazeMesh.vertices = new[] { new Vector3(-1,-1), new Vector3(1,-1), new Vector3(-1,1), new Vector3(1,1) };
            hazeMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            hazeMesh.triangles = new[] { 0,2,1,1,2,3 }; hazeMesh.RecalculateBounds();
            world.GetComponent<MeshFilter>().sharedMesh = hazeMesh;
            haze = world.GetComponent<MeshRenderer>();
            hazeMaterial = new Material(Resources.Load<Shader>("AugmentVfx/TimeSand")); haze.sharedMaterial = hazeMaterial;
            haze.sortingLayerName = "Monster Full"; haze.sortingOrder = 32760;
            haze.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; haze.receiveShadows = false;
            AugmentVfxTag.Mark(world);
        }
        void LateUpdate()
        {
            if (!running) return;
            if (owner == null || !owner.IsAlive || !owner.gameObject.activeInHierarchy) { Clear(); return; }
            // Same scaled clock as NeuralBlockController.activeUntilTime, including pause.
            age = Time.time - startedAt;
            animationTime = Time.time - animationStartedAt;
            bool reduced = GamePreferences.Current.reducedMotion;
            float opacity = GamePreferences.Current.augmentVfxOpacity;
            float strength;
            if (telegraph)
            {
                hudOpacity.alpha = 0;
                strength = Mathf.SmoothStep(0, .3f, age / .6f);
            }
            else
            {
                Remaining01 = Mathf.Clamp01(1 - age / duration);
                finishAge = Mathf.Max(0, age - duration);
                if (finishAge >= .18f) { Clear(); return; }
                float fade = 1 - Mathf.SmoothStep(0, 1, finishAge / .18f);
                hudOpacity.alpha = fade * opacity;
                hourglass.SetState(Remaining01, reduced ? 0 : age, Remaining01 <= .2f, reduced);
                strength = Mathf.SmoothStep(.3f, 1, age / Mathf.Min(.18f, duration * .2f)) * fade;
            }
            var cam = Camera.main;
            haze.enabled = cam != null && strength > 0;
            if (cam == null) return;
            float distance = Mathf.Max(cam.nearClipPlane + .2f, 1f);
            float halfHeight = cam.orthographic ? cam.orthographicSize : Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * distance;
            var t = haze.transform;
            t.position = cam.transform.position + cam.transform.forward * distance; t.rotation = cam.transform.rotation;
            var parent = t.parent.lossyScale;
            t.localScale = new Vector3(halfHeight * cam.aspect / Mathf.Max(.001f, Mathf.Abs(parent.x)), halfHeight / Mathf.Max(.001f, Mathf.Abs(parent.y)), 1);
            hazeMaterial.SetFloat("_Strength", strength);
            hazeMaterial.SetFloat("_Phase", reduced ? 0 : animationTime);
            hazeMaterial.SetFloat("_Aspect", cam.aspect);
            hazeMaterial.SetFloat("_Calm", reduced ? 1 : 0);
        }
        void OnDisable() { Clear(); }
        void OnDestroy()
        {
            if (hazeMaterial != null) Destroy(hazeMaterial);
            if (hazeMesh != null) Destroy(hazeMesh);
            if (hud != null) Destroy(hud.gameObject);
            if (haze != null) Destroy(haze.gameObject);
        }
    }
}
