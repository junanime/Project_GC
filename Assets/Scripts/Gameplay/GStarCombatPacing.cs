using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Vampire
{
    // Side-channel observations for the booth CSV. Timing advances only during controllable field play.
    internal static class GStarCombatPacing
    {
        static readonly Queue<float> kills = new Queue<float>();
        static readonly HashSet<ulong> targets = new HashSet<ulong>();
        static float elapsed, lastContact, longestGap, skillUntil;
        static int skillNumber;
        static bool firstKill, firstGroup, openingReported, onField;
        public static void Begin()
        {
            elapsed = lastContact = longestGap = 0;
            skillUntil = -1; skillNumber = 0;
            onField = firstKill = firstGroup = openingReported = false;
            kills.Clear(); targets.Clear();
        }

        static string Seconds(float t) => t.ToString("F2", CultureInfo.InvariantCulture);
        public static void Tick(LevelManager level, float delta)
        {
            onField = GStarAnalytics.IsRecording && level != null && !level.IsRunFlowPaused &&
                !StageEntryLoading.IsLoading && !MiniStageRuntimeState.IsInsideMiniStage && delta > 0;
            if (!onField) return;
            elapsed += delta;
            if (!openingReported)
            {
                longestGap = Mathf.Max(longestGap, Mathf.Min(90, elapsed) - lastContact);
                if (elapsed >= 90) ReportOpening();
            }
            if (skillUntil >= 0 && elapsed >= skillUntil) ReportSkill();
        }

        public static void Contact()
        {
            if (onField && !openingReported) lastContact = elapsed;
        }

        public static void Affected(Monster monster)
        {
            if (!onField || monster == null || monster is BloodClotMonster) return;
            Contact();
            if (skillUntil >= elapsed) targets.Add(((ulong)(uint)monster.GetInstanceID() << 32) | monster.SpawnGeneration);
        }

        public static void Skill()
        {
            if (!onField) return;
            if (skillUntil >= 0) ReportSkill();
            skillNumber++; targets.Clear(); skillUntil = elapsed + 3;
        }

        public static void Kill()
        {
            if (!onField) return;
            Contact();
            if (!firstKill)
            {
                firstKill = true;
                GStarAnalytics.Record("first_field_kill", "seconds", Seconds(elapsed));
            }
            if (firstGroup) return;
            while (kills.Count > 0 && elapsed - kills.Peek() > 1.5f) kills.Dequeue();
            kills.Enqueue(elapsed);
            if (kills.Count < 3) return;
            firstGroup = true;
            GStarAnalytics.Record("first_three_field_kills_in_1.5s", "seconds", Seconds(elapsed));
            kills.Clear();
        }

        static void ReportOpening()
        {
            openingReported = true;
            GStarAnalytics.Record("opening_longest_combat_gap", Seconds(Mathf.Min(90, elapsed)), Seconds(longestGap));
        }

        static void ReportSkill()
        {
            GStarAnalytics.Record("targets_affected_within_3s_of_skill", skillNumber.ToString(), targets.Count.ToString());
            skillUntil = -1;
        }

        public static void End()
        {
            if (!openingReported) ReportOpening();
            if (skillUntil >= 0) ReportSkill();
            onField = false;
        }
    }
}
