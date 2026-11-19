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
        BloodClotOvercharge clot;
        CharacterSkillDefinition.SkillKind shape;
        static readonly Color Track = new Color(1,.85f,.57f,.68f);

        public void Reset()
        {
            count = reserved = laneIndex = 0;
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
            if (prepared || (reserved == 0 && count % Cycle >= 8)) return;
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
