using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // A field-enemy mechanic, deliberately independent of the player's VFX opacity setting.
    public sealed class MonsterNeedleBarrier : MonoBehaviour
    {
        public const float StartTime = 630f, SpawnChance = .10f;
        public const int RequiredHits = 3;
        const float PopDelay = .09f, PopDuration = .38f;
        public int Hits { get; private set; }
        public bool IsProtected => armed && Hits < RequiredHits && isActiveAndEnabled && owner != null && owner.HP > 0;
        public Collider2D Surface => surface;
        public int AttachedNeedles => needles.Count;
        public Transform VisualRoot => view;
        public Vector2 Radii => radii;
        bool armed;
        Monster owner;
        SpriteRenderer body;
        Transform view;
        PolygonCollider2D surface;
        MeshRenderer renderer;
        Mesh mesh;
        static Material material;
        Vector2 radii;
        float popAge, hitAge, hitAngle;
        readonly List<Vector3> vertices = new List<Vector3>(1800);
        readonly List<Color> colors = new List<Color>(1800);
        readonly List<int> triangles = new List<int>(2700);
        readonly List<Needle> needles = new List<Needle>(3);
        sealed class Needle { public SpriteRenderer sprite; public Vector2 anchor, travel; public Quaternion rotation; public Vector3 scale; public float tip; }

        public static bool Blocks(Component target)
        {
            if (target == null) return false;
            var barrier = target.GetComponentInParent<MonsterNeedleBarrier>();
            return barrier != null && barrier.IsProtected;
        }
        public static bool Eligible(Monster monster)
        {
            return monster != null && !monster.IsMiniStageOwned && !(monster is BossMonster) &&
                !(monster is MiniBossMonster) && !(monster is BloodClotMonster) &&
                !(monster.Blueprint is BossMonsterBlueprint) && !(monster.Blueprint is EliteMonsterBlueprint) &&
                !RunScore.IsSpecial(monster);
        }
        public static bool ShouldSpawn(Monster monster, float elapsed, float sample)
            => elapsed >= StartTime && Eligible(monster) && sample < SpawnChance;
        public static void ConfigureSpawn(Monster monster, float elapsed)
        {
            var old = monster.GetComponent<MonsterNeedleBarrier>();
            if (old != null) old.ResetBarrier();
            if (elapsed < StartTime || !Eligible(monster)) return;
            if (Random.value < SpawnChance) (old ?? monster.gameObject.AddComponent<MonsterNeedleBarrier>()).Arm();
        }
        public void Arm()
        {
            ResetBarrier();
            owner = GetComponent<Monster>(); body = owner != null ? owner.BodyRenderer : null;
            if (body == null || body.sprite == null) return;
            if (view == null)
            {
                view = new GameObject("Thin needle barrier").transform;
                view.SetParent(transform, false);
                // The collider belongs to the same monster rigidbody and hit layer.
                view.gameObject.layer = owner.BodyHitbox != null ? owner.BodyHitbox.gameObject.layer : gameObject.layer;
                surface = view.gameObject.AddComponent<PolygonCollider2D>(); surface.isTrigger = true;
                var outline = new Vector2[32];
                for (int i = 0; i < outline.Length; i++) outline[i] = new Vector2(Mathf.Cos(i*Mathf.PI/16), Mathf.Sin(i*Mathf.PI/16));
                surface.SetPath(0, outline);
                mesh = new Mesh { name = "Thin barrier membrane" }; mesh.MarkDynamic();
                view.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = view.gameObject.AddComponent<MeshRenderer>();
                if (material == null) material = new Material(Shader.Find("Sprites/Default")) { name = "Needle barrier shared" };
                renderer.sharedMaterial = material;
            }
            radii = FitRadii(); armed = true; enabled = true; view.gameObject.SetActive(true); surface.enabled = true;
            hitAge = 10; UpdatePose(); Draw();
        }
        Vector2 FitRadii()
        {
            // Circumscribe the visible sprite, including hats/spikes, not only its feet hitbox.
            var size = PromptSpriteGeometry.WorldBounds(body).size;
            return new Vector2(Mathf.Max(.18f, size.x * .64f + .025f), Mathf.Max(.18f, size.y * .64f + .025f));
        }
        public void ResetBarrier()
        {
            armed = false; Hits = 0; popAge = 0;
            if (surface != null) surface.enabled = false;
            foreach (var needle in needles) if (needle.sprite != null) { needle.sprite.gameObject.SetActive(false); Destroy(needle.sprite.gameObject); }
            needles.Clear();
            if (view != null) view.gameObject.SetActive(false);
        }
        void OnDisable() { ResetBarrier(); }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }

        // Called before any needle damage, item proc, status, pierce, reflection or return logic.
        public bool AbsorbNeedle(SpriteRenderer source, Vector2 incoming, Vector2 position)
        {
            if (!IsProtected || owner.IsFieldRuntimeSuspended) return false;
            UpdatePose();
            Vector2 travel = incoming.sqrMagnitude > .0001f ? incoming.normalized : ((Vector2)view.position - position).normalized;
            if (travel == Vector2.zero) travel = Vector2.right;
            Vector2 local = position - (Vector2)view.position;
            Vector2 q = new Vector2(local.x / radii.x, local.y / radii.y);
            Vector2 d = new Vector2(travel.x / radii.x, travel.y / radii.y);
            float a = d.sqrMagnitude, b = Vector2.Dot(q,d), c = q.sqrMagnitude-1;
            float discriminant = b*b-a*c;
            Vector2 anchor = discriminant >= 0 ? q + d*((-b-Mathf.Sqrt(discriminant))/a) : -d.normalized;
            anchor.Normalize();
            // Consecutive shots from the same direction stay separately readable on the membrane.
            Vector2 entry = anchor;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                bool crowded = false;
                foreach (var needle in needles) if (Vector2.Distance(anchor,needle.anchor)<.30f) { crowded=true; break; }
                if (!crowded) break;
                // Search both sides of the incoming ray; a single positive rotation can
                // accidentally move the second pin directly onto the first one.
                int step = attempt / 2 + 1;
                anchor = Rotate(entry, (attempt % 2 == 0 ? 1 : -1) * step * .36f);
            }
            hitAngle = Mathf.Atan2(anchor.y,anchor.x); hitAge = 0;
            AddNeedle(source,anchor,travel);
            Hits++;
            if (Hits >= RequiredHits) { surface.enabled = false; popAge = 0; }
            UpdatePose(); Draw();
            return true;
        }
        void AddNeedle(SpriteRenderer source, Vector2 anchor, Vector2 travel)
        {
            if (source == null || source.sprite == null) return;
            var sprite = new GameObject("Needle pinned to membrane").AddComponent<SpriteRenderer>();
            // Keep needle art outside the ellipse's nonuniform scale, so diagonals never shear.
            sprite.transform.SetParent(transform, false); sprite.sprite = source.sprite;
            sprite.flipX = source.flipX; sprite.flipY = source.flipY;
            sprite.color = Color.white;
            var needle = new Needle { sprite=sprite, anchor=anchor, travel=travel, rotation=source.transform.rotation, scale=source.transform.lossyScale };
            // Keep the visible tip on the membrane, with the shaft outside the monster.
            foreach (var v in VisibleBodyGeometry.Outline(source.sprite))
            {
                Vector3 point = new Vector3(source.flipX ? -v.x : v.x,source.flipY ? -v.y : v.y);
                needle.tip = Mathf.Max(needle.tip, Vector2.Dot(needle.rotation * Vector3.Scale(point,needle.scale),travel));
            }
            needles.Add(needle);
        }
        void LateUpdate()
        {
            if (!armed) return;
            if (owner == null || owner.HP <= 0 || body == null) { ResetBarrier(); return; }
            float delta = owner.IsFieldRuntimeSuspended ? 0 : Time.deltaTime;
            hitAge += delta;
            if (Hits >= RequiredHits)
            {
                popAge += delta;
                if (popAge >= PopDelay+PopDuration) { ResetBarrier(); return; }
            }
            UpdatePose(); Draw();
        }
        void UpdatePose()
        {
            if (view == null || body == null) return;
            var fit = FitRadii(); radii = Vector2.Max(radii,fit);
            view.SetPositionAndRotation(PromptSpriteGeometry.WorldBounds(body).center, Quaternion.identity);
            var scale = transform.lossyScale;
            view.localScale = new Vector3(radii.x / SafeScale(scale.x), radii.y / SafeScale(scale.y), 1 / SafeScale(scale.z));
            renderer.sortingLayerID = body.sortingLayerID; renderer.sortingOrder = body.sortingOrder+6;
            renderer.enabled = body.enabled;
            float burst = Mathf.Clamp01((popAge-PopDelay)/PopDuration);
            foreach (var needle in needles)
            {
                var t = needle.sprite.transform;
                Vector2 anchor = (Vector2)view.position + Vector2.Scale(needle.anchor,radii)*(1+burst*.22f);
                t.position = new Vector3(anchor.x,anchor.y,view.position.z) - (Vector3)needle.travel * Mathf.Max(0,needle.tip-.012f);
                t.rotation = needle.rotation;
                t.localScale = new Vector3(needle.scale.x/SafeScale(scale.x),needle.scale.y/SafeScale(scale.y),needle.scale.z/SafeScale(scale.z));
                needle.sprite.sortingLayerID = renderer.sortingLayerID; needle.sprite.sortingOrder = renderer.sortingOrder+1;
                needle.sprite.enabled = body.enabled;
                needle.sprite.color = new Color(1,1,1,1-burst);
            }
        }
        static float SafeScale(float value) => Mathf.Abs(value)<.0001f ? 1 : value;
        static Vector2 Rotate(Vector2 p,float radians) => new Vector2(p.x*Mathf.Cos(radians)-p.y*Mathf.Sin(radians),p.x*Mathf.Sin(radians)+p.y*Mathf.Cos(radians));
        Vector3 Point(float angle,float radius)
        {
            // Tiny rounded movement; collider stays at the same stable surface radius.
            float dent = hitAge < .24f ? Mathf.Cos(Mathf.DeltaAngle(angle*Mathf.Rad2Deg,hitAngle*Mathf.Rad2Deg)*Mathf.Deg2Rad*3) * .018f * (1-hitAge/.24f) : 0;
            return new Vector3(Mathf.Cos(angle)*(radius+dent),Mathf.Sin(angle)*(radius+dent),0);
        }
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
        {
            int n=vertices.Count; vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
            for(int i=0;i<4;i++)colors.Add(color);
            triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
        }
        void Arc(float from,float to,float radius,float width,Color color)
        {
            int steps=Mathf.Max(2,Mathf.CeilToInt((to-from)*16));
            for(int i=0;i<steps;i++)
            {
                float a=Mathf.Lerp(from,to,i/(float)steps),b=Mathf.Lerp(from,to,(i+1f)/steps);
                Quad(Point(a,radius-width),Point(a,radius),Point(b,radius),Point(b,radius-width),color);
            }
        }
        void Draw()
        {
            if (mesh==null) return;
            vertices.Clear(); colors.Clear(); triangles.Clear();
            float burst=Mathf.Clamp01((popAge-PopDelay)/PopDuration);
            if(burst<=0)
            {
                // Clear middle and a gentle blue edge gradient preserve the monster's original colors.
                for(int i=0;i<64;i++)
                {
                    float a=i*Mathf.PI/32,b=(i+1)*Mathf.PI/32;
                    Quad(Vector3.zero,Point(a,.90f),Point(b,.90f),Vector3.zero,new Color(.32f,.65f,1,.10f));
                }
                Arc(0,Mathf.PI*2,.97f,.10f,new Color(.28f,.65f,1,.16f));
                Arc(0,Mathf.PI*2,1,.035f,new Color(.24f,.64f,1,.94f));
                Arc(0,Mathf.PI*2,.965f,.018f,new Color(.66f,.91f,1,.85f));
                Arc(1.95f,2.55f,.86f,.045f,new Color(.88f,.98f,1,.85f));
                Arc(5.1f,5.65f,.87f,.025f,new Color(.61f,.88f,1,.7f));
                // Persistent small ripples at every pinned needle communicate damage, not a timed debuff.
                foreach(var needle in needles)
                {
                    float angle=Mathf.Atan2(needle.anchor.y,needle.anchor.x);
                    Arc(angle-.15f,angle+.15f,.89f,.025f,new Color(.78f,.95f,1,.9f));
                    Arc(angle-.23f,angle+.23f,.82f,.018f,new Color(.41f,.79f,1,.65f));
                }
                if(hitAge<.18f) Arc(hitAngle-.3f,hitAngle+.3f,1.02f+hitAge*.3f,.028f,new Color(.86f,.98f,1,1-hitAge/.18f));
            }
            else
            {
                float alpha=1-burst;
                for(int i=0;i<3;i++)
                {
                    float a=.3f+i*Mathf.PI*2/3+burst*.18f;
                    Arc(a,a+1.65f*(1-burst),1+burst*.30f,.04f*alpha,new Color(.4f,.8f,1,alpha));
                }
                for(int i=0;i<7;i++)
                {
                    float a=i*Mathf.PI*2/7;
                    Vector3 p=Point(a,1+burst*.5f); float r=.035f*alpha;
                    Quad(p+new Vector3(-r,0),p+new Vector3(0,r),p+new Vector3(r,0),p+new Vector3(0,-r),new Color(.55f,.88f,1,alpha));
                }
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
        }
    }
}
