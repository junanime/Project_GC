using System.Collections;
using UnityEngine;

namespace Vampire
{
    public sealed partial class ConstipationKingRuntime
    {
        static Vector2 Rotate(Vector2 direction, float degrees) => FoodBossRuntime.Rotate(direction, degrees);
        IEnumerator Pattern(int index)
        {
            switch (index)
            {
                case 0: yield return Barrage(); break;
                case 1: yield return Fan(); break;
                case 2: yield return Compressed(); break;
                case 3: yield return Dash(); break;
                case 4: yield return Hook(); break;
                case 5: yield return Paper(); break;
                case 6: yield return Flush(); break;
                case 7: yield return Tribute(); break;
                case 8: yield return Shield(); break;
                case 9: yield return Summon(); break;
            }
        }
        void Fire(Vector2 from, Vector2 direction, float speed = 7, float damage = 12, float radius = .22f, bool split = false)
        {
            var map = FiniteStomachMap.Instance;
            if (map != null && map.IsColon && map.FloorClearance(from) < radius + .1f) return;
            ConstipationKingProjectile.Create(this, from, direction, speed, damage, radius, split); ProjectilesFired++;
        }
        IEnumerator Barrage()
        {
            Visual.SetPose(KingPose.Barrage); yield return Wait(.7f);
            int count = PhaseTwo ? 8 : 5;
            for (int i = 0; i < count; i++)
            {
                Visual.Face(Aim); Visual.SetPoseProgress(i / (float)count);
                Fire(Visual.Mouth, AimFrom(Visual.Mouth)); yield return Wait(.31f);
            }
            yield return Wait(.45f);
        }
        IEnumerator Fan()
        {
            Visual.SetPose(KingPose.Fan);
            Vector2 dir = AimFrom(Visual.Mouth);
            var warning = ConstipationKingEffects.Warning(this, FoodBossEffects.FanOutline(Visual.Mouth, dir, 10, 58), Visual.Mouth);
            yield return Wait(1.0f); Destroy(warning.gameObject);
            for (int volley = 0; volley < 2; volley++)
            {
                for (int i = -3; i <= 3; i++) Fire(Visual.Mouth, Rotate(dir, i * 8 + volley * 7), 6.3f);
                if (volley == 0) yield return Wait(.65f);
            }
            yield return Wait(.6f);
        }
        IEnumerator Compressed()
        {
            Visual.SetPose(KingPose.Charge);
            Vector2 dir = AimFrom(Visual.Mouth);
            var warning = ConstipationKingEffects.Warning(this, ColonBossEffects.Lane(Visual.Mouth, ColonAttackWall.Stop(Visual.Mouth, Visual.Mouth + dir * 14, .6f), 1.3f), Visual.Mouth);
            // The large shot always has a full three-second open-mouth charge.
            yield return Tween(3, q => { Visual.Face(dir); Visual.SetPoseProgress(q); });
            Destroy(warning.gameObject); Visual.SetPose(KingPose.Shoot);
            Fire(Visual.Mouth, dir, 6.5f, 25, .65f, PhaseTwo);
            yield return Wait(1.1f);
        }
        IEnumerator Dash()
        {
            int count = PhaseTwo ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                Visual.SetPose(KingPose.DashCharge); Vector2 from = transform.position, dir = AimFrom(from);
                var end = ColonAttackWall.Stop(from, from + dir * 12, BodyRadius);
                var warning = ConstipationKingEffects.Warning(this, ColonBossEffects.Lane(from, end, BodyRadius * 2), from);
                yield return Wait(i == 0 ? 1.25f : 1.05f); Destroy(warning.gameObject);
                Visual.SetPose(KingPose.Dash);
                Vector2 previous = from;
                yield return Tween(.95f, q =>
                {
                    MoveTo(Vector2.Lerp(from, end, Mathf.SmoothStep(0, 1, q)));
                    if (SnailBossProjectile.SegmentDistance(Player.transform.position, previous, transform.position) < BodyRadius + .2f && ColonAttackWall.Clear(previous, Player.transform.position)) TryHit(24);
                    previous = transform.position;
                });
                Visual.SetMoving(false); yield return Wait(.5f);
            }
        }
        IEnumerator Hook()
        {
            Visual.SetPose(KingPose.Hook);
            Vector2 start = Visual.StaffTip, dir = AimFrom(start), end = ColonAttackWall.Stop(start, start + dir * 10, .3f);
            var warning = ConstipationKingEffects.Warning(this, ColonBossEffects.Lane(start, end, .8f), start);
            yield return Wait(1.0f); Destroy(warning.gameObject);
            var rope = ConstipationKingEffects.Line(this, new[] { start, start }, .06f, new Color(.56f, .35f, .16f), 521);
            var cup = ConstipationKingEffects.Prop(this, "Plunger", end, .85f, new Color(.73f, .24f, .14f));
            cup.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            bool hooked = false; Vector2 tip = start, previous = start;
            yield return Tween(.8f, q =>
            {
                tip = Vector2.Lerp(start, end, q); cup.transform.position = tip; rope.SetPosition(1, tip);
                if (!hooked && ColonAttackWall.Clear(previous, Player.transform.position) && SnailBossProjectile.SegmentDistance(Player.transform.position, previous, tip) < .55f)
                { hooked = true; TryHit(16); }
                previous = tip;
            });
            if (PhaseTwo)
            {
                var leftEnd = ColonAttackWall.Stop(end, end + Rotate(dir, -45) * 5, .3f);
                var rightEnd = ColonAttackWall.Stop(end, end + Rotate(dir, 45) * 5, .3f);
                var left = ConstipationKingEffects.Warning(this, ColonBossEffects.Lane(end, leftEnd, .7f), end);
                var right = ConstipationKingEffects.Warning(this, ColonBossEffects.Lane(end, rightEnd, .7f), end);
                yield return Wait(.65f); Destroy(left.gameObject); Destroy(right.gameObject);
                var branches = new[] { leftEnd, rightEnd };
                foreach (var target in branches) ConstipationKingHookBranch.Create(this, end, target);
                yield return Wait(1.6f);
            }
            else
            {
                yield return Tween(1.5f, q =>
                {
                    if (hooked) PushPlayer(((Vector2)transform.position - (Vector2)Player.transform.position).normalized * (2.3f * Time.deltaTime), transform.position);
                    if (hooked) { tip = Player.transform.position; cup.transform.position = tip; rope.SetPosition(1, tip); }
                });
            }
            Vector2 backFrom = tip;
            yield return Tween(.6f, q => { tip = Vector2.Lerp(backFrom, start, q); cup.transform.position = tip; rope.SetPosition(1, tip); });
            Destroy(cup.gameObject); Destroy(rope.gameObject);
        }
        IEnumerator Paper()
        {
            Visual.SetPose(KingPose.Paper);
            yield return Wait(.35f);
            Vector2 from = Visual.StaffTip, dir = AimFrom(from);
            int count = PhaseTwo ? 3 : 1;
            for (int i = 0; i < count; i++)
            {
                Vector2 laneDir = Rotate(dir, count == 1 ? 0 : (i - 1) * 24);
                var end = ColonAttackWall.Stop(from, from + laneDir * 11, .5f);
                ConstipationKingPaper.Create(this, from, end, PhaseTwo);
            }
            if (PhaseTwo)
            {
                // Launch from the original raised staff, then slam while the three strips travel.
                yield return Wait(1.05f);
                yield return Tween(.5f, q => Visual.SetPoseProgress(q));
                yield return Wait(3.55f);
            }
            else yield return Wait(3.5f);
        }
        IEnumerator Flush()
        {
            Visual.SetPose(KingPose.Flush);
            // Preserve V3's one large vortex / four differently-sized independent vortices.
            // The phase-two radii are proportional to its authored 82/101/105/78 vs 178 pixels.
            float[] radii = PhaseTwo ? new[] { 2.6f * 82 / 178, 2.6f * 101 / 178, 2.6f * 105 / 178, 2.6f * 78 / 178 } : new[] { 2.6f };
            Vector2 target = Player.transform.position, forward = AimFrom(transform.position), side = Vector2.Perpendicular(forward);
            var centers = new Vector2[radii.Length]; var warnings = new LineRenderer[radii.Length];
            Vector2[] offsets = { new Vector2(-2.60f, 2.66f), new Vector2(1.24f, 2.40f), new Vector2(-2.13f, -1.92f), new Vector2(1.78f, -1.92f) };
            for (int i = 0; i < radii.Length; i++)
            {
                Vector2 desired = PhaseTwo ? target + forward * offsets[i].x + side * offsets[i].y : target;
                centers[i] = FindFlushCenter(desired, target, radii, centers, i);
                warnings[i] = ConstipationKingEffects.Warning(this, FoodBossEffects.Arc(centers[i], radii[i]), centers[i]);
                warnings[i].name = "Flush warning · " + i;
            }
            yield return Tween(1.25f, q => Visual.SetPoseProgress(q));
            // All boundaries are visible before the common release. Nothing connects to the boss.
            for (int i = 0; i < radii.Length; i++)
                ConstipationKingVortex.Create(this, centers[i], warnings[i], PhaseTwo ? 5.5f : 4.5f, radii[i]);
            yield return Wait(.6f);
        }
        static Vector2 FindFlushCenter(Vector2 desired, Vector2 target, float[] radii, Vector2[] centers, int index)
        {
            var map = FiniteStomachMap.Instance; float radius = radii[index];
            bool Fits(Vector2 point)
            {
                if (map != null && map.IsColon && (map.FloorClearance(point) < radius + .2f || map.FloorClearance(point, true) < radius + .15f)) return false;
                for (int i = 0; i < index; i++) if (Vector2.Distance(point, centers[i]) < radius + radii[i] + .55f) return false;
                return true;
            }
            if (Fits(desired)) return desired;
            if (map != null && map.IsColon)
            {
                var projected = map.ClampToFloor(desired, radius + .3f);
                if (Fits(projected)) return projected;
            }
            // A wall can collapse two nearest-floor projections onto one point. Search nearby
            // open floor instead, retaining a traversable gap between every pair of vortices.
            for (float reach = 1.5f; reach <= 22; reach += 1.25f)
                for (int step = 0; step < 32; step++)
                {
                    var point = target + FoodBossRuntime.Dir(step * 360f / 32 + index * 37) * reach;
                    if (Fits(point)) return point;
                }
            if (map != null && map.IsColon)
                for (float y = map.WorldRect.yMin + radius; y < map.WorldRect.yMax - radius; y += 2)
                    for (float x = map.WorldRect.xMin + radius; x < map.WorldRect.xMax - radius; x += 2)
                    { var point = new Vector2(x, y); if (Fits(point)) return point; }
            throw new System.InvalidOperationException("No clear floor for independent king vortex " + index);
        }
        IEnumerator Tribute()
        {
            Visual.SetPose(KingPose.Tribute); yield return Wait(.9f);
            var carriers = new ConstipationKingMinion[3];
            for (int i = 0; i < 3; i++)
            {
                Vector2 at = FiniteStomachMap.SafeSpawn((Vector2)transform.position + Rotate(Aim, -70 + i * 70) * 7, .45f);
                carriers[i] = ConstipationKingMinion.Create(this, at, true, i);
            }
            ColonBossDirector.ShowNotice("진상품을 막으세요", "운반 몬스터가 왕에게 닿으면 체력을 회복합니다");
            for (float elapsed = 0; elapsed < 10;)
            {
                bool waiting = false; foreach (var carrier in carriers) if (carrier != null && carrier.Health > 0) { waiting = true; break; }
                if (!waiting) break;
                if (!EncounterPaused && !Immobilized) elapsed += Time.deltaTime;
                yield return null;
            }
            foreach (var carrier in carriers) if (carrier != null) { carrier.gameObject.SetActive(false); Destroy(carrier.gameObject); }
        }
        IEnumerator Shield()
        {
            Visual.SetPose(KingPose.Shield); yield return Wait(.85f);
            for (int i = 0; i < 3; i++) ConstipationKingShield.Create(this, i);
            yield return Wait(4.5f);
        }
        IEnumerator Summon()
        {
            Visual.SetPose(KingPose.Summon);
            float radius = Size * 1.5f;
            var warning = ConstipationKingEffects.Warning(this, FoodBossEffects.Arc(transform.position, radius), transform.position);
            yield return Tween(1.1f, q => Visual.SetPoseProgress(q)); Destroy(warning.gameObject);
            var overflow = ConstipationKingEffects.Overflow(this, radius);
            var pool = overflow.GetComponent<ConstipationKingOverflowVisual>();
            yield return Tween(1.2f, q => pool.SetProgress(q));
            int count = PhaseTwo ? 7 : 5;
            for (int i = 0; i < count; i++)
            {
                Vector2 desired = Rotate(Aim, (i - (count - 1) * .5f) * 360 / count);
                Vector2 at = transform.position;
                // Keep the authored count even beside an inner wall: bring an obstructed
                // spawn inward along its lane instead of silently omitting the minion.
                for (float distance = radius * .82f; distance >= .6f; distance -= .5f)
                {
                    Vector2 candidate = FiniteStomachMap.SafeSpawn((Vector2)transform.position + desired * distance, .45f);
                    if (!ColonAttackWall.Clear(transform.position, candidate, .45f)) continue;
                    at = candidate; break;
                }
                ConstipationKingMinion.Create(this, at, false, i % 3);
                yield return Wait(PhaseTwo ? .18f : .22f);
            }
            yield return Wait(1.1f); yield return Tween(.4f, q => pool.SetOpacity(1 - q)); Destroy(overflow);
        }
    }
}
