using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Vampire
{
    // Local playtest measurements. Release builds omit all calls to the probe.
    internal static class CombatPacingProbe
    {
        static readonly HashSet<ulong> skillTargets = new HashSet<ulong>();
        static float elapsed, lastContact, longestOpeningGap, skillEnds;
        static bool onField, firstKill, openingReported;

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Begin()
        {
            elapsed = lastContact = longestOpeningGap = 0;
            skillEnds = -1;
            onField = firstKill = openingReported = false;
            skillTargets.Clear();
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Tick(LevelManager level, float delta)
        {
            onField = level != null && !level.IsLevelEnded && !level.IsRunFlowPaused &&
                !StageEntryLoading.IsLoading && !MiniStageRuntimeState.IsInsideMiniStage &&
                !GStarControlPractice.Active && delta > 0;
            if (!onField) return;
            elapsed += delta;
            if (!openingReported)
            {
                longestOpeningGap = Mathf.Max(longestOpeningGap, Mathf.Min(90, elapsed) - lastContact);
                if (elapsed >= 90) ReportOpening();
            }
            if (skillEnds >= 0 && elapsed >= skillEnds) ReportSkill();
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Contact()
        {
            if (onField) lastContact = elapsed;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Affected(Monster target)
        {
            if (!onField || target == null || target is BloodClotMonster) return;
            Contact();
            if (skillEnds > elapsed)
                skillTargets.Add(((ulong)(uint)target.GetInstanceID() << 32) | target.SpawnGeneration);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Skill()
        {
            if (!onField) return;
            if (skillEnds >= 0) ReportSkill();
            skillTargets.Clear();
            skillEnds = elapsed + 3f;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Kill()
        {
            if (!onField) return;
            Contact();
            if (firstKill) return;
            firstKill = true;
            UnityEngine.Debug.Log($"[Combat QA] First field kill at {elapsed:F2}s");
        }

        static void ReportOpening()
        {
            openingReported = true;
            UnityEngine.Debug.Log($"[Combat QA] Longest opening combat gap {longestOpeningGap:F2}s in {Mathf.Min(90, elapsed):F2}s");
        }

        static void ReportSkill()
        {
            UnityEngine.Debug.Log($"[Combat QA] {skillTargets.Count} distinct targets affected in the observation window after skill use");
            skillEnds = -1;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void End()
        {
            if (!openingReported && elapsed > 0) ReportOpening();
            if (skillEnds >= 0) ReportSkill();
            onField = false;
        }
    }
}
