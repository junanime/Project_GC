using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public sealed class OralRollingStone
    {
        readonly SpriteRenderer rock, shadow;
        readonly float radius, scale, bodyHeight;
        struct Impact { public uint generation; public float until; }
        readonly Dictionary<Monster,Impact> impacts = new Dictionary<Monster,Impact>();
        int lastDash = -1;
        float fallAge, angle, bounceReady, hop;
        Vector2 velocity;
        public Vector2 Position { get; private set; }
        public bool Falling { get; private set; }
        public bool Finished => Falling && fallAge >= .9f;

        public OralRollingStone(Transform parent, Vector2 at, Sprite sprite, float height)
        {
            Position = at; bodyHeight = height;
            float h = Mathf.Clamp(height * 1.8f,.7f,2.5f);
            radius = h * .42f; scale = h / sprite.bounds.size.y;
            rock = OralEventArt.Sprite(parent, "치석 굴림돌", sprite, 25);
            shadow = OralEventArt.Sprite(parent, "굴림돌 그림자", OralEventArt.Disc, 7);
            GroundVisualSorting.Apply(shadow,7);
            shadow.transform.localScale = new Vector3(h*.85f,h*.24f,1f);
            Pose();
        }

        public bool NeedsUpdate(Character player) =>
            Falling || hop > 0 || velocity.sqrMagnitude > .0001f || (player.Position - Position).sqrMagnitude < (radius+1f)*(radius+1f);

        // Consume the same collision-checked physics segment the player actually
        // travels. Sampling at render rate can miss the entire contact at low FPS.
        public void PushAlongDash(Vector2 from, Vector2 to, int dashId)
        {
            if (Falling || lastDash==dashId) return;
            Vector2 path=to-from, offset=Position-from;
            float t=Vector2.Dot(offset,path)/path.sqrMagnitude;
            if (t<0 || (offset-path*Mathf.Min(t,1f)).sqrMagnitude>(radius+.42f)*(radius+.42f)) return;
            lastDash=dashId;
            velocity=path.normalized*9f;
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.MonsterHit);
        }

        public bool Tick(float dt, FiniteStomachMap map, Character player, List<Monster> monsters, out int hits)
        {
            hits = 0; hop = Mathf.Max(0,hop-dt);
            if (Falling) { fallAge += dt; Pose(); return false; }
            Vector2 previous = Position, offset = Position - player.Position;
            var input = player.IsDashing && player.Velocity.sqrMagnitude>.01f ? player.Velocity.normalized : player.EffectiveMoveDirection.normalized;
            bool touching = offset.magnitude < radius+.42f && Vector2.Dot(input,offset.normalized)>.4f && !player.IsOralSliding &&
                !(OralStageRuntime.Instance?.IsAirborne??false) && OralMapGeometry.ZoneAt(map,player.Position)==OralZone.Tongue;
            bool pushing = touching && !player.IsDashing && velocity.sqrMagnitude<.1f;
            if (pushing)
            {
                float speed = Mathf.Max(.1f,Vector2.Dot(player.Velocity,input));
                Position += input * speed * dt;
                var rb=player.GetComponent<Rigidbody2D>();
                if (rb!=null && Vector2.Distance(player.Position,Position)<radius+.16f)
                    rb.position=Position-offset.normalized*(radius+.16f);
            }
            else if (velocity.sqrMagnitude>.0001f)
            {
                Position += velocity*dt;
                velocity = Vector2.MoveTowards(velocity,Vector2.zero,6f*dt);
            }
            if (OralTasteEvents.Instance!=null && Time.time>=bounceReady && OralTasteEvents.Instance.TryBounceBoulder(Position,out var bounce))
            { velocity=bounce*7f; bounceReady=Time.time+.9f; hop=.6f; }
            float floor = OralMapGeometry.SouthFloor(map)+radius*.4f;
            if (Position.y < floor) { Position = new Vector2(Position.x,floor); velocity.y=0; }
            if (OralMapGeometry.TongueDistance(map, Position, out _) < -radius*.2f)
            { Falling=true; fallAge=0; velocity=Vector2.zero; Pose(); return false; }

            var path = Position - previous;
            if (path.sqrMagnitude > .000004f)
            {
                angle -= Mathf.Sign(Mathf.Abs(path.x)>.001f?path.x:path.y)*path.magnitude/radius*Mathf.Rad2Deg;
                HitLine(previous,path,monsters,map,ref hits);
            }
            Pose(); return pushing;
        }
        void HitLine(Vector2 previous, Vector2 path, List<Monster> monsters, FiniteStomachMap map, ref int hits)
        {
            foreach (var m in monsters)
            {
                if (m==null || !m.IsAlive || !m.gameObject.activeInHierarchy || m.IsMiniStageOwned || m.IsFieldRuntimeSuspended ||
                    m is BloodClotMonster || m is TrapMonster || OralMapGeometry.ZoneAt(map,m.Position)!=OralZone.Tongue) continue;
                if (impacts.TryGetValue(m,out var old) && old.generation==m.SpawnGeneration && Time.time<old.until) continue;
                float t = Mathf.Clamp01(Vector2.Dot(m.Position-previous,path)/path.sqrMagnitude);
                float width = radius+Mathf.Min(.6f,m.Size.magnitude*.5f);
                if ((m.Position-(previous+path*t)).sqrMagnitude > width*width) continue;
                impacts[m] = new Impact { generation=m.SpawnGeneration, until=Time.time+.65f };
                float damage=Mathf.Max(12f, m.Blueprint!=null ? m.Blueprint.hp*.40f : 12f);
                if (velocity.sqrMagnitude>4f && OralMonsterRoster.IsBlueprint(m.Blueprint))
                    damage=Mathf.Max(damage,m.SpawnMaxHealth*1.05f);
                m.TakeDamage(damage,path.normalized*1.6f);
                hits++;
            }
        }
        void Pose()
        {
            float fall=Mathf.Clamp01(fallAge/.9f);
            float lift=Falling?0:Mathf.Sin(hop/.6f*Mathf.PI)*bodyHeight*1.5f;
            rock.transform.position=Position+Vector2.up*(radius*.65f+lift-fall*fall*bodyHeight*2.3f);
            rock.transform.rotation=Quaternion.Euler(0,0,angle+fall*140f);
            rock.transform.localScale=Vector3.one*scale*(1-fall*.7f);
            rock.color=new Color(1,1,1,1-fall);
            shadow.transform.position=Position;
            shadow.color=new Color(.18f,.035f,.07f,.27f*(1-fall));
        }
        public void Release()
        {
            rock.gameObject.SetActive(false); shadow.gameObject.SetActive(false);
            Object.Destroy(rock.gameObject); Object.Destroy(shadow.gameObject); impacts.Clear();
        }
    }
}
