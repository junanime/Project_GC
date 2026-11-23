using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // A bounded pool sits under the oral stage so teardown and ceiling travel own every fragment.
    public sealed class OralFoodBreaks
    {
        const int Limit = 20;
        sealed class Burst
        {
            public SpriteRenderer Left, Right;
            public bool Gummy;
            public Vector2 Center, Size, Scale;
            public float Age, Direction;
        }
        readonly List<Burst> bursts = new List<Burst>(Limit);
        readonly Transform parent;
        int next;

        public OralFoodBreaks(Transform parent) { this.parent = parent; }

        public void Show(OralFoodSkin skin, bool gummy, SpriteRenderer body, float direction)
        {
            if (next == bursts.Count)
                bursts.Add(new Burst {
                    Left = OralEventArt.Sprite(parent, "음식 파편", null),
                    Right = OralEventArt.Sprite(parent, "사탕 반쪽", null)
                });
            var burst = bursts[next]; next = (next + 1) % Limit;
            burst.Gummy = gummy; burst.Age = 0; burst.Direction = direction;
            var bounds = body.bounds;
            burst.Center = gummy ? new Vector2(bounds.center.x, bounds.min.y) : (Vector2)bounds.center;
            burst.Size = bounds.size; burst.Scale = body.transform.lossyScale;
            Set(burst.Left, gummy ? skin.Body : skin.Left, body);
            Set(burst.Right, gummy ? null : skin.Right, body);
            Pose(burst);
        }

        static void Set(SpriteRenderer r, Sprite sprite, SpriteRenderer source)
        {
            r.sprite = sprite; r.enabled = sprite != null; r.color = Color.white;
            r.sortingLayerID = source.sortingLayerID; r.sortingOrder = source.sortingOrder + 1;
            r.flipX = source.flipX;
        }

        public void Tick(float dt)
        {
            foreach (var burst in bursts)
            {
                if (!burst.Left.enabled) continue;
                burst.Age += dt;
                if (burst.Age < .27f) Pose(burst);
                else { burst.Left.enabled = burst.Right.enabled = false; }
            }
        }

        static void Pose(Burst b)
        {
            float t = Mathf.Clamp01(b.Age / .27f);
            float rise = Mathf.Sin(t * Mathf.PI) * b.Size.y;
            var baseScale = new Vector3(b.Scale.x, b.Scale.y, 1);
            if (b.Gummy)
            {
                float stretch = Mathf.Sin(t * Mathf.PI) * .28f;
                b.Left.transform.position = b.Center + new Vector2(b.Direction * b.Size.x * .84f * t, rise * .5f);
                b.Left.transform.rotation = Quaternion.Euler(0, 0, -b.Direction * 26 * t);
                b.Left.transform.localScale = Vector3.Scale(baseScale, new Vector3(1 - stretch, .83f + stretch, 1));
            }
            else
            {
                float spread = b.Size.x * (.25f + t * .68f);
                float sign = b.Left.flipX ? -1 : 1;
                for (int i = 0; i < 2; i++)
                {
                    var r = i == 0 ? b.Left : b.Right;
                    float side = (i == 0 ? -1 : 1) * sign;
                    r.transform.position = b.Center + new Vector2(side * spread + b.Direction * t * b.Size.x * .36f, rise * .36f - t*t*b.Size.y*.34f);
                    r.transform.rotation = Quaternion.Euler(0, 0, -side * (68 + 13 * i) * t);
                    r.transform.localScale = baseScale;
                }
            }
            b.Left.color = b.Right.color = new Color(1,1,1,1 - Mathf.InverseLerp(.45f,1,t));
        }
    }
}
