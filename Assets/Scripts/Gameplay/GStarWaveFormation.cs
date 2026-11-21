using UnityEngine;

namespace Vampire
{
    // Repositions existing ordinary wave rolls. Special rolls, loot and the wave clock are unchanged.
    internal sealed class GStarWaveFormation
    {
        const int Cycle = 18;
        int count, reserved, laneIndex;
        float deadline;
        Vector2 anchor, farAnchor, inward;
        bool prepared;
        readonly int[] cohort = new int[18];
        readonly Vector2[] cohortPoints = new Vector2[18];
        int queued, planned, formationIndex;
        float nextCohort, firstRoll, announceTime;
        public int FormationsReleased { get; private set; }
        public string LastFormation { get; private set; }
        float nextRelease;
        BloodClotOvercharge clot;
        CharacterSkillDefinition.SkillKind shape;
        static readonly Color Track = new Color(1,.85f,.57f,.68f);

        public void Reset()
        {
            count = reserved = laneIndex = queued = planned = formationIndex = FormationsReleased = 0;
            nextCohort = nextRelease = 0;
            LastFormation = null;
            prepared = false;
            clot = null;
        }

        public void Reserve(LevelManager level, BloodClotOvercharge target)
        {
            clot = target;
            reserved = 8;
            laneIndex = 0;
            deadline = level.CurrentLevelTime + 20;
            prepared = false;
            Preview(level);
        }

        bool Enabled(LevelManager level)
        {
            var map = FiniteStomachMap.Instance;
            return map != null && map.FieldActive && level.PlayerCharacter != null && !level.IsRunFlowPaused
                && !StageEntryLoading.IsLoading && !MiniStageRuntimeState.IsInsideMiniStage
                && !(OralStageRuntime.Instance?.OnCeiling ?? false) && !GStarControlPractice.Active;
        }

        void ExpireReservation(LevelManager level)
        {
            if (reserved == 0) return;
            if (clot != null && clot.CanProgress && level.CurrentLevelTime < deadline) return;
            reserved = 0;
            prepared = false;
        }

        public void Preview(LevelManager level)
        {
            if (!Enabled(level)) return;
            ExpireReservation(level);
            if (prepared || (reserved == 0 && (queued != 0 || count % Cycle >= 8))) return;
            var player = level.PlayerCharacter;
            var center = reserved > 0 ? (Vector2)clot.transform.position : player.Position;
            var heading = player.EffectiveMoveDirection;
            if (heading.sqrMagnitude < .04f) heading = Vector2.right;
            heading.Normalize();
            float distance = reserved > 0 ? RemakeBalance.Current.overchargeRadius + .8f : count < Cycle ? 3.6f : 5.7f;
            var map = FiniteStomachMap.Instance;
            // Rotate around the player near a bend; never project the whole group through a wall.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var facing = (Vector2)(Quaternion.Euler(0,0,(-1 + count / Cycle % 3) * 28 + attempt * 45) * heading);
                var first = center + facing * distance;
                var other = center + (Vector2)(Quaternion.Euler(0,0,100) * facing) * distance;
                if (!Accessible(map, center, first) || (reserved > 0 && !Accessible(map, center, other))) continue;
                anchor = first; farAnchor = other; inward = -facing; prepared = true;
                var def = player.Skills?.Definition;
                shape = count < Cycle && def != null ? def.kind
                    : (CharacterSkillDefinition.SkillKind)((count / Cycle) % 4);
                GStarFieldStreak.Draw(level, first, inward, Track);
                if (reserved > 0) GStarFieldStreak.Draw(level, other, (center-other).normalized, Track);
                break;
            }
        }

        bool GroupWindow(LevelManager level, FiniteStomachMap map)
        {
            if (map == null || !Enabled(level) || reserved > 0 || level.CurrentLevelTime < 300 ||
                level.CurrentLevelTime >= (map.IsOral ? OralSwallowFinale.StartTime - 12f : level.LevelDuration - 20f)) return false;
            if (!map.IsOral) return true;
            var events = OralFieldEvents.Instance;
            return OralMapGeometry.ZoneAt(map,level.PlayerCharacter.Position) == OralZone.Tongue &&
                !(OralSwallowFinale.Instance?.Active ?? false) &&
                (events == null || (events.CurrentEvent != OralEventKind.Ceiling &&
                 !(events.Tastes != null && events.Tastes.Active && events.Tastes.Kind == OralTasteKind.Spicy)));
        }

        // The director banks already-rolled ordinary tickets for a short formation.
        // Specials stay live; no additional rolls, population cap or item multiplier is introduced.
        public bool QueueCohort(LevelManager level, int index, int bonusTenths, int bonusRate)
        {
            var map = FiniteStomachMap.Instance;
            if (queued >= cohort.Length || planned != 0 || !GroupWindow(level,map) || level.CurrentLevelTime < nextCohort) return false;
            if (queued == 0)
            {
                int limit = level.CurrentLevelBlueprint.normalSpawnPopulationLimit;
                if (level.EntityManager.LivingMonsters.Count + 4 > limit + Mathf.CeilToInt(limit * bonusRate / 10f)) return false;
                firstRoll = level.CurrentLevelTime;
            }
            cohort[queued++] = index;
            if (queued + (bonusTenths + queued * bonusRate) / 10 >= 18)
            {
                planned = PlanCohort(level,map) ? 18 : -1;
                announceTime = Time.time;
            }
            return true;
        }

        public void DispatchCohort(LevelManager level, int bonusTenths, int bonusRate)
        {
            if (queued == 0) return;
            var map = FiniteStomachMap.Instance;
            bool eligible = GroupWindow(level,map);
            if (eligible && planned == 0 && level.CurrentLevelTime - firstRoll < 8f) return;
            if (eligible && planned > 0 && Time.time - announceTime < .70f) return;
            Vector2[] points = eligible && planned > 0 ? cohortPoints : null;
            int slot = 0;
            if (points != null)
            {
                int limit = level.CurrentLevelBlueprint.normalSpawnPopulationLimit;
                if (level.EntityManager.LivingMonsters.Count + 9 > limit + Mathf.CeilToInt(limit * bonusRate / 10f)) points = null;
            }
            if (points != null)
            {
                // The player may have dashed since the warning. Discard a blocked layout as a whole.
                foreach (var point in points)
                    if (!LegalCohortPoint(map, level.PlayerCharacter.Position, point)) { points = null; break; }
            }
            if (points == null)
            {
                // Space was lost or evacuation started: settle one old roll at a time,
                // at normal cadence and via the field's standard safe placement.
                planned = -1;
                if (Time.time < nextRelease) return;
                level.SpawnRolledWaveMonster(cohort[0], null, 0);
                queued--;
                System.Array.Copy(cohort, 1, cohort, 0, queued);
                nextRelease = Time.time + .35f;
                if (queued == 0) EndCohort(level,map);
                return;
            }
            for (int i = 0; i < queued; i++)
            {
                level.SpawnRolledWaveMonster(cohort[i],points,slot++);
                bonusTenths += bonusRate;
                if (bonusTenths >= 10) { bonusTenths -= 10; slot++; }
            }
            LastFormation = formationIndex % 3 == 0 ? "two-ranks" : formationIndex % 3 == 1 ? "open-ring" : "split-chevron";
            FormationsReleased++;
            queued = 0;
            EndCohort(level,map);
        }

        void EndCohort(LevelManager level, FiniteStomachMap map)
        {
            formationIndex++; planned = 0; prepared = false;
            nextCohort = level.CurrentLevelTime + (map != null && map.IsOral ? 20f : 35f);
        }

        static bool LegalCohortPoint(FiniteStomachMap map, Vector2 player, Vector2 position) =>
            (player-position).sqrMagnitude >= 3.25f*3.25f && Accessible(map,player,position);

        bool PlanCohort(LevelManager level, FiniteStomachMap map)
        {
            var player = level.PlayerCharacter;
            var origin = player.Position;
            var preferred = formationIndex % 3 == 0 ? Vector2.left : player.EffectiveMoveDirection;
            if (preferred.sqrMagnitude < .01f) preferred = Vector2.right;
            preferred.Normalize();
            for (int attempt=0;attempt<8;attempt++)
            {
                var facing=(Vector2)(Quaternion.Euler(0,0,45f*attempt)*preferred);
                var side=new Vector2(-facing.y,facing.x);
                bool clear=true;
                for (int i=0;i<18;i++)
                {
                    Vector2 point;
                    if (formationIndex%3==0)
                        point=origin + facing*(4.8f+(i/9)*.68f) + side*((i%9-4)*.58f);
                    else if (formationIndex%3==1)
                        // A quarter of the circumference stays open towards the player's heading.
                        point=origin+(Vector2)(Quaternion.Euler(0,0,54f+i*(252f/17f))*facing)*5.3f;
                    else
                    {
                        int row=i/2;
                        point=origin+facing*(4.45f+row*.22f)+side*((i%2==0?-1:1)*(.85f+row*.39f));
                    }
                    cohortPoints[i]=point;
                    if (!LegalCohortPoint(map,origin,point)) {clear=false; break;}
                }
                if (!clear) continue;
                foreach (int i in new[] {0,8,17})
                    GStarFieldStreak.Draw(level,cohortPoints[i],(origin-cohortPoints[i]).normalized,Track);
                return true;
            }
            return false;
        }

        static bool Accessible(FiniteStomachMap map, Vector2 center, Vector2 point)
        {
            if (map.FloorClearance(point) < 1f || AcidLakeEvent.Contains(point)) return false;
            if (map.IsColon) return ColonAttackWall.Clear(center,point,.25f);
            if (!map.IsOral) return true;
            var zone = OralMapGeometry.ZoneAt(map,center);
            if (zone == OralZone.Chasm || OralMapGeometry.ZoneAt(map,point) != zone) return false;
            for (int i = 1; i <= 4; i++)
                if (OralMapGeometry.ZoneAt(map,Vector2.Lerp(center,point,i/5f)) != zone) return false;
            return true;
        }

        public bool TryPosition(LevelManager level, out Vector2 point)
        {
            point = default;
            if (!Enabled(level)) return false;
            Preview(level);
            int slot = count++ % Cycle;
            if (!prepared) return false;
            var player = level.PlayerCharacter.Position;
            bool clotLane = reserved > 0;
            if (!clotLane && slot >= 8) { prepared = false; return false; }
            int index = clotLane ? laneIndex++ : slot;
            var axis = inward;
            var origin = anchor;
            if (clotLane && (index & 1) == 1) { origin = farAnchor; axis = ((Vector2)clot.transform.position-origin).normalized; }
            Vector2 side = new Vector2(-axis.y,axis.x);
            Vector2 delta;
            if (clotLane) delta = -axis * ((index / 2) * .24f) + side * ((index % 3 - 1) * .30f);
            else delta = Offset(shape,index,axis,side);
            point = origin + delta;
            if (clotLane) { if (--reserved == 0) prepared = false; }
            else if (slot == 7) prepared = false;
            float distance = Vector2.Distance(player,point);
            // Crossing the entrance mid-wave must never materialize an enemy on the player.
            return distance >= 2.65f && distance <= 12f && Accessible(FiniteStomachMap.Instance,player,point);
        }

        internal static Vector2 Offset(CharacterSkillDefinition.SkillKind kind,int i,Vector2 axis,Vector2 side)
        {
            switch (kind)
            {
                case CharacterSkillDefinition.SkillKind.Ari:
                    // A short, spreading pin formation with a clear landing beyond it.
                    int row = i < 1 ? 0 : i < 3 ? 1 : i < 6 ? 2 : 3;
                    return -axis * (row * .57f) + side * ((i - row*(row+1)/2 - row*.5f) * .68f);
                case CharacterSkillDefinition.SkillKind.Hyuki:
                    return side * ((i % 2 == 0 ? -1 : 1) * (.62f + i/2*.23f)) - axis * (i/2*.37f);
                case CharacterSkillDefinition.SkillKind.Shini:
                    if (i < 2) return side * ((i-.5f)*.54f);
                    return -axis * (.8f+(i-2)/3*.64f) + side * (((i-2)%3-1)*(.48f+(i-2)/3*.24f));
                default:
                    return -axis * (i/2*.51f) + side * ((i%2-.5f)*.85f + i/2*.16f);
            }
        }
    }

    // A short directional scuff on the ground, never a damage warning or input prompt.
    internal sealed class GStarFieldStreak : MonoBehaviour
    {
        LevelManager level;
        LineRenderer[] marks;
        Color tint;
        float age;
        const float Duration = .95f;
        public static void Draw(LevelManager level, Vector2 at, Vector2 direction, Color color)
        {
            var streak = new GameObject("Field movement · floor streak").AddComponent<GStarFieldStreak>();
            streak.level = level; streak.tint = color;
            streak.transform.SetParent(level.transform,false);
            streak.marks = new LineRenderer[3];
            var side = new Vector2(-direction.y,direction.x);
            for (int i=0;i<3;i++)
            {
                var start = at + side*((i-1)*.23f);
                float length = i==1 ? .58f : .37f;
                var mark = FoodBossEffects.Line(streak.transform,new[]{start-direction*length, start},.062f,color,false,16);
                GroundVisualSorting.Apply(mark,16);
                streak.marks[i] = mark;
            }
        }

        void Update()
        {
            if (level == null || level.IsRunFlowPaused || level.IsLevelEnded || MiniStageRuntimeState.IsInsideMiniStage)
            { Destroy(gameObject); return; }
            age += Time.deltaTime;
            float q = Mathf.Clamp01(age / Duration);
            var c = tint; c.a *= Mathf.Sin(q * Mathf.PI);
            foreach (var mark in marks) { mark.startColor = mark.endColor = c; mark.endWidth = .055f*(1-q); }
            if (q >= 1) Destroy(gameObject);
        }
    }
}
