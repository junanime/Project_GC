using UnityEngine;

namespace Vampire
{
    /// <summary>A stable landing footprint, independent of animation padding or body recoil.</summary>
    public sealed class MonsterGroundShadow : MonoBehaviour
    {
        private SpriteRenderer body, projection;
        private Vector3 originalPosition, originalScale;
        private int originalLayer, originalOrder;
        private bool captured;
        private readonly Vector3[] anchors = new Vector3[4];
        private bool lastFlipX, lastFlipY;

        public void Configure(SpriteRenderer bodyRenderer, SpriteRenderer shadowRenderer,
            Sprite reference, MonsterBlueprint profile)
        {
            body = bodyRenderer;
            projection = shadowRenderer;
            if (!captured)
            {
                originalPosition = projection.transform.localPosition;
                originalScale = projection.transform.localScale;
                originalLayer = projection.sortingLayerID;
                originalOrder = projection.sortingOrder;
                captured = true;
            }
            Vector2 uv = profile.groundShadowCenterUV;
            Vector2 point = new Vector2((uv.x * reference.rect.width - reference.pivot.x) / reference.pixelsPerUnit,
                (uv.y * reference.rect.height - reference.pivot.y) / reference.pixelsPerUnit);
            float groundY = projection.bounds.center.y;
            for (int i = 0; i < anchors.Length; i++)
            {
                Vector3 world = body.transform.TransformPoint(new Vector3((i & 1) != 0 ? -point.x : point.x,
                    (i & 2) != 0 ? -point.y : point.y, 0));
                if (profile.preserveGroundShadowHeight) world.y = groundY;
                anchors[i] = transform.InverseTransformPoint(world);
            }
            Vector2 size = new Vector2(reference.rect.width * profile.groundShadowSizeUV.x,
                reference.rect.height * profile.groundShadowSizeUV.y) / reference.pixelsPerUnit;
            Vector3 bodyScale = body.transform.lossyScale;
            Vector3 parentScale = projection.transform.parent != null ? projection.transform.parent.lossyScale : Vector3.one;
            Vector3 spriteSize = projection.sprite.bounds.size;
            projection.transform.localScale = new Vector3(
                size.x * Mathf.Abs(bodyScale.x) / Mathf.Max(.0001f, spriteSize.x * Mathf.Abs(parentScale.x)),
                size.y * Mathf.Abs(bodyScale.y) / Mathf.Max(.0001f, spriteSize.y * Mathf.Abs(parentScale.y)),
                originalScale.z);
            projection.sortingLayerID = body.sortingLayerID;
            projection.sortingOrder = body.sortingOrder - 1;
            enabled = true;
            RefreshFacing();
        }

        public void Restore()
        {
            if (captured && projection != null)
            {
                projection.transform.localPosition = originalPosition;
                projection.transform.localScale = originalScale;
                projection.sortingLayerID = originalLayer;
                projection.sortingOrder = originalOrder;
            }
            enabled = false;
        }

        private void LateUpdate()
        {
            if (body != null && (body.flipX != lastFlipX || body.flipY != lastFlipY)) RefreshFacing();
        }

        public void RefreshFacing()
        {
            if (body == null || projection == null) return;
            lastFlipX = body.flipX; lastFlipY = body.flipY;
            int index = (lastFlipX ? 1 : 0) | (lastFlipY ? 2 : 0);
            Vector3 target = transform.TransformPoint(anchors[index]);
            // Sprite pivots need not be central. Align the visible ellipse and retain its depth.
            target.z = projection.bounds.center.z;
            projection.transform.position += target - projection.bounds.center;
        }
    }
}
