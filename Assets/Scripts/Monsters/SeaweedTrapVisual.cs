using UnityEngine;

namespace Vampire
{
    [DefaultExecutionOrder(600)]
    public sealed class SeaweedTrapVisual : MonoBehaviour
    {
        private SpriteRenderer rear, front, original, player;
        private SpriteRenderer[] layers;
        private TrapMonsterBlueprint blueprint;
        private float elapsed;
        private bool capturing;
        public SpriteRenderer Rear => rear;
        public SpriteRenderer Front => front;
        public int FrameIndex => !capturing ? 0 : Mathf.Min(
            1 + Mathf.FloorToInt(elapsed / Mathf.Max(0.01f, blueprint.animationFrameTime)),
            blueprint.captureRearSprites.Length - 1);

        public bool Configure(TrapMonsterBlueprint data, SpriteRenderer existing)
        {
            if (existing == null || data == null || data.captureRearSprites == null || data.captureRearSprites.Length == 0 ||
                data.captureFrontSprites == null || data.captureFrontSprites.Length != data.captureRearSprites.Length)
            {
                Hide();
                if (existing != null) existing.enabled = true;
                return false;
            }
            blueprint = data;
            original = existing;
            if (rear == null) rear = CreateLayer("Seaweed Rear");
            if (front == null) front = CreateLayer("Seaweed Front");
            layers = new[] { rear, front };
            ResetDormant();
            return true;
        }

        private SpriteRenderer CreateLayer(string layerName)
        {
            var child = new GameObject(layerName);
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = original.sharedMaterial;
            return renderer;
        }

        public void Capture(SpriteRenderer capturedPlayer)
        {
            player = capturedPlayer;
            capturing = player != null;
            elapsed = 0f;
            Refresh();
        }

        public void ResetDormant()
        {
            capturing = false;
            player = null;
            elapsed = 0f;
            if (original != null) original.enabled = false;
            if (rear != null) { rear.enabled = true; front.enabled = true; Refresh(); }
        }

        public void Hide()
        {
            capturing = false;
            player = null;
            if (rear != null) { rear.enabled = false; front.enabled = false; }
        }

        private void LateUpdate()
        {
            if (rear == null || !rear.enabled) return;
            if (capturing && player == null) { Hide(); return; }
            Refresh();
            if (capturing) elapsed += Time.deltaTime;
        }

        private void Refresh()
        {
            int index = FrameIndex;
            rear.sprite = blueprint.captureRearSprites[index];
            front.sprite = blueprint.captureFrontSprites[index];
            if (capturing)
            {
                foreach (var renderer in layers)
                {
                    renderer.transform.position = player.transform.position;
                    renderer.transform.rotation = player.transform.rotation;
                    Vector3 parentScale = transform.lossyScale;
                    Vector3 scale = player.transform.lossyScale;
                    renderer.transform.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, 1f);
                    renderer.sortingLayerID = player.sortingLayerID;
                    renderer.flipX = player.flipX;
                }
                rear.sortingOrder = player.sortingOrder - 1;
                front.sortingOrder = player.sortingOrder + 1;
            }
            else
            {
                foreach (var renderer in layers)
                {
                    renderer.transform.localPosition = Vector3.up * (188f / 675f);
                    renderer.transform.localRotation = Quaternion.identity;
                    renderer.transform.localScale = Vector3.one;
                    renderer.flipX = false;
                    GroundVisualSorting.Apply(renderer, 1);
                }
            }
        }
        private void OnDisable() => Hide();
    }
}
