using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Native UI ribbons: soft rising coffee steam or horizontal peristalsis currents.
    public sealed class StageEventFlowGraphic : MaskableGraphic
    {
        public bool steam;
        private float age, direction;
        public void Animate(float time, float sign)
        { age = time; direction = sign; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int line = 0; line < 3; line++)
            {
                for (int i = 0; i < 20; i++)
                {
                    float t = i / 20f, next = (i + 1) / 20f;
                    Vector2 a = Point(line,t), b = Point(line,next);
                    Vector2 normal = new Vector2(-(b-a).y,(b-a).x).normalized * 1.8f;
                    Color tint = color; tint.a *= Mathf.Sin(t * Mathf.PI) * (.6f + .4f * Mathf.Sin(age * 2 + line) * Mathf.Sin(age * 2 + line));
                    int start = vh.currentVertCount;
                    vh.AddVert(a-normal,tint,Vector2.zero); vh.AddVert(a+normal,tint,Vector2.zero);
                    vh.AddVert(b+normal,tint,Vector2.zero); vh.AddVert(b-normal,tint,Vector2.zero);
                    vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
                }
            }
        }
        private Vector2 Point(int line, float t)
        {
            return steam ? new Vector2((line-1)*30 + Mathf.Sin(t*7-age*2+line)*7, -12+t*58)
                : new Vector2((-70+t*140)*direction, (line-1)*20 + Mathf.Sin(t*7-age*3)*6);
        }
    }
}
