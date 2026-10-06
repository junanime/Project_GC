using UnityEngine;

namespace Vampire
{
    public static class CharacterHurtboxFit
    {
        public const float Scale = .9f;

        // Fit the projectile hurtbox once to the idle art, not to squash/stretch or dash poses.
        // Pickup triggers and the feet's solid collision centre remain unchanged.
        public static void Apply(Character owner, SpriteRenderer body, Collider2D feet)
        {
            if (owner == null || body == null || body.sprite == null) return;
            foreach (var box in owner.GetComponentsInChildren<BoxCollider2D>(true))
            {
                if (!box.isTrigger || box.name != "Full Hitbox") continue;
                Fit(box, body);
            }
            if (feet is CircleCollider2D circle) circle.radius *= Scale;
        }

        public static void Fit(BoxCollider2D box, SpriteRenderer body)
        {
            var vertices = body.sprite.vertices;
            if (vertices.Length == 0) return;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (var vertex in vertices)
            {
                var point = vertex;
                if (body.flipX) point.x = -point.x;
                if (body.flipY) point.y = -point.y;
                Vector2 local = box.transform.InverseTransformPoint(body.transform.TransformPoint(point));
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            box.offset = (min + max) * .5f;
            box.size = (max - min) * Scale;
        }
    }
}
