using UnityEngine;

namespace Vampire
{
    // Seven loose feathers stay at the muzzle while the real projectile leaves.
    // No movement, collider, damage or flight-speed changes are made here.
    public sealed class PhoenixHeavyReleaseVisual : MonoBehaviour
    {
        readonly PhoenixRadianceLayer[] feathers = new PhoenixRadianceLayer[7];
        SpriteRenderer sorting;
        Vector3 origin, forward, side;
        float age, length, heading;
        public const float Duration = .38f;
        public float Age => age;
        public static PhoenixHeavyReleaseVisual Play(Vector3 position, Vector2 direction, float charge, SpriteRenderer sort)
        {
            var v = new GameObject("Charged needle feather release").AddComponent<PhoenixHeavyReleaseVisual>();
            v.origin = position; v.forward = direction.sqrMagnitude > .0001f ? (Vector3)direction.normalized : Vector3.right;
            v.side = new Vector3(-v.forward.y,v.forward.x); v.heading = Mathf.Atan2(v.forward.y,v.forward.x)*Mathf.Rad2Deg;
            v.sorting = sort; v.length = Mathf.Lerp(.55f,1.35f,Mathf.Clamp01(charge));
            for(int i=0;i<v.feathers.Length;i++) v.feathers[i] = new PhoenixRadianceLayer(v.transform,"FeatherKit");
            v.Render(); return v;
        }
        void Update()
        {
            age += Time.deltaTime;
            if(age >= Duration) { Destroy(gameObject); return; }
            Render();
        }
        void Render()
        {
            float t = Mathf.Clamp01(age / Duration);
            bool reduced = GamePreferences.Current.reducedMotion;
            float spread = reduced ? .35f : Mathf.SmoothStep(0,1,t);
            float opacity = 1-Mathf.SmoothStep(0,1,t);
            for(int i=0;i<feathers.Length;i++)
            {
                float across=(i-3)/3f;
                Vector3 p=origin-forward*length*(.18f+spread*.38f)+side*length*across*(.12f+spread*.62f);
                float tilt=heading-90+across*(25+spread*35);
                var size=new Vector2(length*.55f,length*(.7f-.15f*Mathf.Abs(across)));
                feathers[i].Draw(p,size,tilt,1+i,opacity,sorting,8,reduced?0:age,.009f);
            }
        }
    }
}
