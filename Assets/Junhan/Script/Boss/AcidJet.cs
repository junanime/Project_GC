using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // One shared mouth-anchored stream for the field event and miniboss.
    public sealed class AcidJet : MonoBehaviour
    {
        Transform source;
        Vector2 direction;
        float length, width, duration, damage, age, cooldown;
        bool monsters;
        Character player;
        EntityManager manager;
        Renderer[] visuals;
        readonly List<Monster> targets = new List<Monster>();
        float nextMonsterCheck;
        readonly Dictionary<Monster, float> hits = new Dictionary<Monster, float>();
        float nextPlayerHit;
        LineRenderer outline, liquid, highlight;
        readonly List<SpriteRenderer> foam = new List<SpriteRenderer>();
        public float Progress => Mathf.Clamp01(age / duration);
        public float Reach => length * Mathf.Clamp01(age / .3f);
        public static AcidJet Create(Transform mouth, Vector2 direction, float length, float width, float duration, float damage, Character player, bool hitMonsters = false, float cooldown = .65f)
        {
            var go = new GameObject("산성액 발사 · continuous jet");
            var jet = go.AddComponent<AcidJet>(); jet.source = mouth; jet.direction = direction.normalized;
            jet.length = length; jet.width = width; jet.duration = Mathf.Max(.3f, duration); jet.damage = damage;
            jet.player = player; jet.monsters = hitMonsters; jet.cooldown = Mathf.Max(.2f, cooldown);
            jet.outline = Line(go.transform, new Color(.2f,.37f,.025f), width, 510);
            jet.liquid = Line(go.transform, new Color(.62f,.91f,.08f), width * .85f, 511);
            jet.highlight = Line(go.transform, new Color(.96f,1,.57f), width * .18f, 512);
            for (int i = 0; i < 10; i++)
            {
                var sr = new GameObject("Flowing acid foam").AddComponent<SpriteRenderer>(); sr.transform.SetParent(go.transform);
                sr.sortingOrder = 513; sr.sprite = AcidToadArt.Frame("AcidFx", i % 4); jet.foam.Add(sr);
            }
            jet.visuals=go.GetComponentsInChildren<Renderer>();jet.manager=Object.FindObjectOfType<EntityManager>();return jet;
        }
        public static LineRenderer Line(Transform parent, Color color, float width, int order)
        {
            var line = new GameObject("Fluid contour").AddComponent<LineRenderer>(); line.transform.SetParent(parent, false);
            line.sharedMaterial = Material; line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = width; line.positionCount = 20; line.numCapVertices = 5; line.sortingOrder = order;
            return line;
        }
        static Material material;
        static Material Material => material != null ? material : material = new Material(Shader.Find("Sprites/Default"));
        void Update()
        {
            if (source == null || !source.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
            bool hidden = MiniStageRuntimeState.IsInsideMiniStage;
            foreach (var r in visuals) r.enabled = !hidden;
            if (hidden || Time.timeScale <= 0) return;
            age += Time.deltaTime;
            if (age >= duration) { Destroy(gameObject); return; }
            Vector2 start = source.position, side = Vector2.Perpendicular(direction), end = start + direction * Reach;
            float tail = Mathf.Clamp01((duration - age) / .18f);
            for (int i = 0; i < 20; i++)
            {
                float t = i / 19f;
                Vector3 p = start + direction * Reach * t + side * Mathf.Sin(t * 25 - age * 19) * width * .06f * t;
                outline.SetPosition(i,p); liquid.SetPosition(i,p); highlight.SetPosition(i,p + (Vector3)(side * width * .17f));
            }
            outline.startWidth=outline.endWidth=width*.64f*tail;
            liquid.startWidth=liquid.endWidth=width*.58f*tail;
            highlight.startWidth=highlight.endWidth=width*.08f*tail;
            for (int i = 0; i < foam.Count; i++)
            {
                var sr = foam[i]; sr.sprite = AcidToadArt.Frame("AcidFx", (i + Mathf.FloorToInt(age * 12)) % 4);
                // Keep each textured segment entirely beyond the lip, including during extension.
                float segment = Reach / foam.Count;
                float center = (i + .5f) * segment;
                float tileLength = Mathf.Min(width * 1.7f, 2 * Mathf.Min(center, Reach - center));
                sr.transform.position = start + direction * center;
                sr.transform.rotation = Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
                if(sr.sprite!=null) sr.transform.localScale = new Vector3(tileLength / sr.sprite.bounds.size.x, width * Mathf.Min(1, Reach / width) / sr.sprite.bounds.size.y, 1);
                sr.color = new Color(1,1,1,tail);
            }
            if (player != null && player.IsAlive && Time.time >= nextPlayerHit && Contains(player.transform.position,start,end,width*.5f+.18f))
            { player.TakeDamage(damage, direction * 1.3f); nextPlayerHit = Time.time + cooldown; }
            if (!monsters || Time.time < nextMonsterCheck || manager == null) return;
            nextMonsterCheck=Time.time+.1f;targets.Clear();targets.AddRange(manager.LivingMonsters);
            // Snapshot: deaths can remove members from LivingMonsters during damage callbacks.
            foreach(var m in targets)
                if(m!=null && !(m is AcidToadMonster) && !m.IsFieldRuntimeSuspended && m.HP>0 &&
                   (!hits.TryGetValue(m,out float at)||Time.time>=at) && Contains(m.Position,start,end,width*.5f+.2f))
                { hits[m]=Time.time+cooldown; m.TakeDamage(damage,direction); }
        }
        public static bool Contains(Vector2 p, Vector2 start, Vector2 end, float radius) => SnailBossProjectile.SegmentDistance(p,start,end)<=radius;
    }

    public sealed class AcidGlob : MonoBehaviour
    {
        Character player; Vector2 velocity; float age, damage; SpriteRenderer art;
        public static void Fire(Vector2 origin,Vector2 direction,float speed,float damage,Character player)
        {
            var go=new GameObject("Acid coated sphere"); go.transform.position=origin;
            var glob=go.AddComponent<AcidGlob>();glob.player=player;glob.velocity=direction*speed;glob.damage=damage;
            glob.art=go.AddComponent<SpriteRenderer>();glob.art.sortingOrder=520;
            OctoberEnemyProjectile.Register(go,true);
        }
        void Update()
        {
            if(MiniStageRuntimeState.IsInsideMiniStage){Destroy(gameObject);return;}
            if(Time.timeScale<=0)return;
            age+=Time.deltaTime;if(age>8){Destroy(gameObject);return;}
            Vector2 before=transform.position;transform.position+=(Vector3)velocity*Time.deltaTime;
            art.sprite=AcidToadArt.Frame("AcidFx",4+Mathf.FloorToInt(age*12)%4);
            if(art.sprite!=null)transform.localScale=Vector3.one*(.38f/art.sprite.bounds.size.x);
            if(player!=null && player.IsAlive && AcidJet.Contains(player.transform.position,before,transform.position,.32f))
            {player.TakeDamage(damage);Destroy(gameObject);}
        }
    }
}
