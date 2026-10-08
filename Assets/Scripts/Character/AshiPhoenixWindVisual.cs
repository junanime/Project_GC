using UnityEngine;

namespace Vampire
{
    // Two halves per ribbon place the wind behind and in front of Ashi, forming a real wrap.
    [DefaultExecutionOrder(520)]
    public sealed class AshiPhoenixWindVisual : MonoBehaviour
    {
        const int Bands = 5, Segments = 40;
        Character owner;
        CharacterSkillRuntime skill;
        SpriteRenderer body;
        readonly LineRenderer[] ribbons = new LineRenderer[Bands * 2];
        readonly Vector3[] points = new Vector3[Segments + 1];
        Material material;

        public void Bind(Character character, CharacterSkillRuntime runtime)
        {
            owner = character; skill = runtime;
            body = GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();
            material = new Material(Shader.Find("Sprites/Default"));
            for (int band = 0; band < Bands; band++)
                for (int half = 0; half < 2; half++)
                {
                    var line = new GameObject("Wind wrap " + band + (half == 0 ? " front" : " back")).AddComponent<LineRenderer>();
                    line.transform.SetParent(transform, false);
                    line.sharedMaterial = material;
                    line.positionCount = Segments + 1;
                    line.useWorldSpace = true;
                    line.numCapVertices = 4;
                    line.enabled = false;
                    ribbons[band * 2 + half] = line;
                }
        }

        void LateUpdate()
        {
            bool show = owner != null && owner.IsAlive && skill != null && skill.IsCutin;
            float t = show ? Mathf.Clamp01(skill.CutinElapsed / Mathf.Max(.01f, skill.Definition.cutinDuration)) : 1;
            float envelope = Mathf.SmoothStep(0, 1, (t - .12f) / .22f) * (1 - Mathf.SmoothStep(0, 1, (t - .76f) / .24f));
            for (int band = 0; band < Bands; band++)
                for (int half = 0; half < 2; half++)
                {
                    var line = ribbons[band * 2 + half];
                    if (line == null) continue;
                    line.enabled = show && envelope > .001f;
                    if (!line.enabled) continue;
                    line.sortingLayerID = body != null ? body.sortingLayerID : 0;
                    line.sortingOrder = (body != null ? body.sortingOrder : 0) + (half == 0 ? 5 : -2);
                    float y = -.25f + band * .32f;
                    float radius = (.5f + band * .17f) * Mathf.Lerp(.65f, 1.12f, envelope);
                    for (int point = 0; point <= Segments; point++)
                    {
                        float a = (half + point / (float)Segments) * Mathf.PI;
                        float spiral = Mathf.Sin(a + t * Mathf.PI * 10 + band * .8f);
                        points[point] = transform.position + new Vector3(Mathf.Cos(a) * radius, y - Mathf.Sin(a) * .24f + spiral * .10f, 0);
                    }
                    line.SetPositions(points);
                    line.startWidth = line.endWidth = (.042f + .011f * band) * envelope;
                    line.startColor = new Color(.58f, 1f, .69f, envelope * .1f);
                    line.endColor = new Color(.84f, 1f, .87f, envelope * .8f);
                }
        }

        void OnDisable() { foreach (var line in ribbons) if (line != null) line.enabled = false; }
        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
