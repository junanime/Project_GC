using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Copies the actual visible renderers, including all original cores, without moving hit colliders.
    public sealed class BossChargeVisual : MonoBehaviour
    {
        private readonly List<SpriteRenderer> sources = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> copies = new List<SpriteRenderer>();
        private BossSpriteSequence thrust;
        private Transform owner;
        private Vector2 direction;
        private float started, stopping = -1;
        private bool charging;
        private bool restored;
        public static BossChargeVisual Begin(Transform root, Vector2 heading)
        {
            var go = new GameObject("ChargeVisual"); go.transform.SetParent(root, false);
            var v = go.AddComponent<BossChargeVisual>(); v.owner = root; v.direction = heading.normalized; v.started = Time.time;
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!sr.enabled || sr.forceRenderingOff || sr.sprite == null || sr.name.Contains("Shadow") || sr.GetComponent<BossSpriteSequence>() != null) continue;
                var copy = new GameObject(sr.name + "_Charge").AddComponent<SpriteRenderer>(); copy.transform.SetParent(go.transform);
                v.sources.Add(sr); v.copies.Add(copy); sr.forceRenderingOff = true;
            }
            var flame = new GameObject("ChargeThrust"); flame.transform.SetParent(go.transform, false); flame.AddComponent<SpriteRenderer>();
            v.thrust = flame.AddComponent<BossSpriteSequence>(); v.thrust.Play("ThrustIgnite", 2.4f, 10, false);
            return v;
        }
        public void StartThrust()
        {
            charging = true; started = Time.time;
            thrust.Play("ThrustRun", 2.4f, 16, true);
        }
        public void Finish()
        {
            if (stopping >= 0) return;
            stopping = Time.time; thrust.Play("ThrustEnd", 2.4f, 12, false);
        }
        private void LateUpdate()
        {
            if (restored) return;
            if (owner == null) { Destroy(gameObject); return; }
            float amount = charging ? 1 : Mathf.Clamp01((Time.time - started) * 3) * .45f;
            if (stopping >= 0) amount *= 1 - Mathf.Clamp01((Time.time - stopping) / .25f);
            var tilt = Quaternion.Euler(0, 0, -12 * direction.x * amount);
            int order = int.MaxValue, layer = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                var src = sources[i]; var copy = copies[i];
                if (src == null) { copy.enabled = false; continue; }
                copy.sprite = src.sprite; copy.color = src.color; copy.sharedMaterial = src.sharedMaterial;
                copy.enabled = src.enabled && src.gameObject.activeInHierarchy;
                copy.flipX = src.flipX; copy.flipY = src.flipY; copy.sortingLayerID = src.sortingLayerID; copy.sortingOrder = src.sortingOrder;
                copy.transform.position = owner.position + tilt * (src.transform.position - owner.position);
                copy.transform.rotation = tilt * src.transform.rotation;
                // Parent has the same world scale as owner; reproduce source world scale.
                Vector3 scale = transform.lossyScale;
                copy.transform.localScale = new Vector3(src.transform.lossyScale.x / scale.x, src.transform.lossyScale.y / scale.y, 1);
                src.forceRenderingOff = true; order = Mathf.Min(order, src.sortingOrder); layer = src.sortingLayerID;
            }
            thrust.Renderer.sortingOrder = order == int.MaxValue ? 9 : order - 1; thrust.Renderer.sortingLayerID = layer;
            thrust.transform.position = owner.position - (Vector3)direction * 1.1f;
            thrust.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            if (stopping >= 0 && Time.time - stopping >= .3f) Destroy(gameObject);
        }
        public void Restore()
        {
            if (restored) return;
            restored = true;
            foreach (var sr in sources) if (sr != null) sr.forceRenderingOff = false;
            foreach (var sr in copies) if (sr != null) sr.enabled = false;
            if (thrust != null) thrust.gameObject.SetActive(false);
        }
        private void OnDisable() { Restore(); }
        private void OnDestroy() { Restore(); }
    }
}
