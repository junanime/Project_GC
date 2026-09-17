using System.Collections;
using UnityEngine;

namespace Vampire
{
    // Presentation-only arrival: no boss AI or hitboxes exist until the descent finishes.
    public sealed class BossSummonPresentation : MonoBehaviour
    {
        public const float SignalStepSeconds = .18f;
        public const float ArrivalSeconds = 2f;
        private SpriteRenderer terminal;
        private Sprite standby, active;
        private readonly SpriteRenderer[] arcs = new SpriteRenderer[3];
        private GameObject arrival;

        public void Initialize(SpriteRenderer target)
        {
            terminal = target;
            standby = Resources.Load<Sprite>("BossSummonArt/Standby");
            active = Resources.Load<Sprite>("BossSummonArt/Active");
            if (terminal == null || standby == null || active == null) return;
            terminal.sprite = standby;
            terminal.color = Color.white;
            for (int i = 0; i < arcs.Length; i++)
            {
                var sr = new GameObject("TransmissionArc" + (i + 1)).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(terminal.transform, false);
                // Sprite canvas is 2 world units tall, with its pivot on the feet.
                sr.transform.localPosition = new Vector3(.025f, 1.70f, 0);
                sr.transform.localScale = Vector3.one * .60f;
                sr.sprite = Resources.Load<Sprite>("BossSummonArt/Signal" + (i + 1));
                sr.sortingLayerID = terminal.sortingLayerID;
                sr.sortingOrder = terminal.sortingOrder + 1;
                sr.enabled = false;
                arcs[i] = sr;
            }
        }

        public IEnumerator Transmit()
        {
            if (terminal != null && active != null) terminal.sprite = active;
            yield return new WaitForSeconds(.2f);
            for (int cycle = 0; cycle < 2; cycle++)
            {
                for (int step = 0; step < 3; step++)
                {
                    if (arcs[step] != null) arcs[step].enabled = true;
                    yield return new WaitForSeconds(SignalStepSeconds);
                }
                yield return new WaitForSeconds(.12f);
                HideArcs();
                yield return new WaitForSeconds(.12f);
            }
        }

        public static Vector3 ArrivalOffset(float progress, float height)
        {
            float t = Mathf.Clamp01(progress);
            float sway = Mathf.Sin(t * Mathf.PI * 4) * .65f * Mathf.Sin(t * Mathf.PI);
            return new Vector3(sway, height * (1 - Mathf.SmoothStep(0, 1, t)), 0);
        }

        public IEnumerator Descend(GameObject bossPrefab, Vector3 landing, float scaleMultiplier = 1f)
        {
            if (bossPrefab == null) yield break;
            arrival = new GameObject("BossArrivalVisual");
            // Keep cleanup tied to this scene object, but use the prefab's world dimensions.
            arrival.transform.SetParent(transform, false);
            arrival.transform.localScale = new Vector3(1 / transform.lossyScale.x, 1 / transform.lossyScale.y, 1);
            foreach (var source in bossPrefab.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!source.enabled || source.sprite == null || source.name.Contains("Shadow")) continue;
                bool visible = true;
                for (var p = source.transform; p != null && p != bossPrefab.transform; p = p.parent)
                    if (!p.gameObject.activeSelf) { visible = false; break; }
                if (!visible) continue;
                var copy = new GameObject(source.name).AddComponent<SpriteRenderer>();
                copy.transform.SetParent(arrival.transform, false);
                copy.transform.localPosition = (source.transform.position - bossPrefab.transform.position) * scaleMultiplier;
                copy.transform.localRotation = source.transform.rotation;
                copy.transform.localScale = source.transform.lossyScale * scaleMultiplier;
                copy.sprite = source.sprite; copy.color = source.color;
                copy.sharedMaterial = source.sharedMaterial;
                copy.flipX = source.flipX; copy.flipY = source.flipY;
                copy.sortingLayerID = source.sortingLayerID;
                copy.sortingOrder = source.sortingOrder;
            }
            float height = 7f;
            Camera camera = Camera.main;
            if (camera != null && camera.orthographic)
                height = Mathf.Max(height, camera.transform.position.y + camera.orthographicSize + 2 - landing.y);
            for (float elapsed = 0; elapsed < ArrivalSeconds && arrival != null; elapsed += Time.deltaTime)
            {
                float t = elapsed / ArrivalSeconds;
                arrival.transform.position = landing + ArrivalOffset(t, height);
                arrival.transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 4) * 7 * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            if (arrival != null) arrival.transform.SetPositionAndRotation(landing, Quaternion.identity);
        }

        // Called immediately after the real pooled boss is ready, avoiding a blank frame.
        public void FinishArrival() { ClearArrival(); }

        public void ResetStandby()
        {
            HideArcs(); ClearArrival();
            if (terminal != null && standby != null) terminal.sprite = standby;
        }
        private void HideArcs() { foreach (var sr in arcs) if (sr != null) sr.enabled = false; }
        private void ClearArrival()
        {
            if (arrival != null) { arrival.SetActive(false); Destroy(arrival); arrival = null; }
        }
        private void OnDisable() { ResetStandby(); }
    }
}
