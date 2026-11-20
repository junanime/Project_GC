using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public sealed class GStarAnalyticsTests
    {
        string directory;
        GStarAnalyticsStore store;
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "ProjectGC-GStarQA-" + Guid.NewGuid().ToString("N"));
            store = new GStarAnalyticsStore(directory);
        }
        [TearDown] public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        static GStarAnalyticsRun Run(string id, string character = "Ashi", int stage = 1) => new GStarAnalyticsRun
        {
            runId = id, sessionId = "session-test", characterId = character, characterName = character,
            startedUtc = "2026-11-16T03:00:00.0000000Z", stageId = stage,
            startingWeaponId = 19, startingRelic = "beads", startingItems = new[] { "1:비타민", "2:칼슘" }
        };
        static GStarAnalyticsEvent Event(string run, int sequence, string type, string id, params string[] options) => new GStarAnalyticsEvent
        {
            runId = run, sessionId = "session-test", sequence = sequence, type = type, id = id,
            utc = "2026-11-16T03:00:01.0000000Z", elapsedSeconds = sequence, detail = "choice context", offered = options
        };
        string Export(string file)
        {
            var result = store.Export(); Assert.IsTrue(result.success, result.error);
            return File.ReadAllText(Path.Combine(result.directory, file));
        }
        [Test] public void RawEventsPreserveOffersSelectionOrderAndNoIdentityFields()
        {
            Assert.IsTrue(store.Begin(Run("one")));
            Assert.IsTrue(store.Append(Event("one", 1, "augment_offered", "level2", "fire:화염침", "ice:빙결침")));
            Assert.IsTrue(store.Append(Event("one", 2, "augment_selected", "fire:화염침")));
            var rows = Export("events.jsonl").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(JsonUtility.FromJson<GStarAnalyticsEvent>).ToArray();
            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual("augment_offered", rows[0].type);
            CollectionAssert.AreEqual(new[] { "fire:화염침", "ice:빙결침" }, rows[0].offered);
            Assert.AreEqual("augment_selected", rows[1].type);
            Assert.Less(rows[0].sequence, rows[1].sequence);
            string runs = Export("runs.jsonl");
            StringAssert.Contains("startingItems", runs);
            StringAssert.DoesNotContain("\"phone\"", runs);
            StringAssert.DoesNotContain("\"playerName\"", runs);
            StringAssert.Contains("fire:화염침", Export("offered-counts.csv"));
            StringAssert.Contains("augment_selected", Export("choice-counts.csv"));
        }
        [Test] public void FinishedRoundsRefreshQaExportsWithoutRegistrationOrF8()
        {
            foreach (string outcome in new[] { "clear", "fail" })
            {
                var run = Run("auto-" + outcome);
                Assert.IsTrue(store.Begin(run));
                run.status = outcome;
                run.endedUtc = "2026-11-21T04:05:06Z";
                run.normalKills = 17;
                Assert.IsTrue(store.Save(run));
                string output = Path.Combine(directory, "exports");
                StringAssert.Contains(run.runId, File.ReadAllText(Path.Combine(output, "runs.csv")));
                var saved = File.ReadAllLines(Path.Combine(output, "runs.jsonl")).Select(JsonUtility.FromJson<GStarAnalyticsRun>)
                    .Single(row => row.runId == run.runId);
                Assert.AreEqual(outcome, saved.status);
                Assert.AreEqual(17, saved.normalKills);
            }
            Assert.AreEqual(3, File.ReadAllLines(Path.Combine(directory, "exports", "runs.csv")).Length);
            Assert.IsTrue(File.Exists(Path.Combine(directory, "exports", "character-stage-summary.csv")));
        }

        [Test] public void LockedSummaryCannotLoseUnregisteredOutcomeAndNextFinishRefreshesIt()
        {
            var first = Run("first"); Assert.IsTrue(store.Begin(first));
            first.status = "clear"; Assert.IsTrue(store.Save(first));
            string csv = Path.Combine(directory, "exports", "runs.csv");
            using (var workbook = File.Open(csv, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var second = Run("second"); Assert.IsTrue(store.Begin(second));
                second.status = "fail"; second.dashCount = 9; Assert.IsTrue(store.Save(second));
                string stageSummary = File.ReadAllText(Path.Combine(directory, "exports", "stage-summary.csv"));
                StringAssert.Contains("\"1\",\"2\",\"1\",\"1\"", stageSummary, "Unlocked stage summary is current");
                var recovered = new GStarAnalyticsStore(directory).GetRun("second");
                Assert.AreEqual("fail", recovered.status);
                Assert.AreEqual(9, recovered.dashCount);
            }
            // Run a third round through the ordinary end path, without invoking Export.
            var third = Run("third"); Assert.IsTrue(store.Begin(third));
            third.status = "clear"; Assert.IsTrue(store.Save(third));
            var rows = File.ReadAllLines(csv);
            Assert.AreEqual(4, rows.Length);
            StringAssert.Contains("\"fail\"", rows.Single(row => row.StartsWith("\"second\",", StringComparison.Ordinal)));
        }

        [Test] public void CompletedDataIsImmutableAndRestartRecoversActiveAsInterrupted()
        {
            var clear = Run("clear"); Assert.IsTrue(store.Begin(clear));
            clear.normalKills = 10; clear.dashCount = 7; clear.status = "clear";
            clear.endingReason = "stage_clear"; Assert.IsTrue(store.Save(clear));
            var playing = Run("playing", "Hyuki", 0); Assert.IsTrue(store.Begin(playing));
            playing.normalKills = 4; playing.dashCount = 3; playing.elapsedSeconds = 12;
            Assert.IsTrue(store.Save(playing));
            var restarted = new GStarAnalyticsStore(directory);
            Assert.AreEqual("clear", restarted.GetRun("clear").status);
            var recovered = restarted.GetRun("playing");
            Assert.AreEqual("interrupted", recovered.status);
            Assert.AreEqual(4, recovered.normalKills); Assert.AreEqual(3, recovered.dashCount);
            Assert.AreEqual(12, recovered.elapsedSeconds);
            Assert.IsFalse(restarted.Begin(Run("clear")));
            clear.normalKills = 999; Assert.IsFalse(restarted.Save(clear));
            Assert.AreEqual(10, restarted.GetRun("clear").normalKills);
        }
        [Test] public void FinishedBackupRecoversFinishedOutcomeAfterPrimaryCorruption()
        {
            var run = Run("backup"); Assert.IsTrue(store.Begin(run));
            run.status = "fail"; run.endingReason = "hp_zero"; run.damageTaken = 80; run.combatStatsAvailable = true;
            Assert.IsTrue(store.Save(run));
            File.WriteAllText(Path.Combine(directory, "runs", "run-backup.json"), "broken");
            var recovered = new GStarAnalyticsStore(directory).GetRun("backup");
            Assert.AreEqual("fail", recovered.status);
            Assert.AreEqual(80, recovered.damageTaken);
        }
        [Test] public void TornEventLineDoesNotConsumeTheNextValidEvent()
        {
            Assert.IsTrue(store.Begin(Run("events")));
            Assert.IsTrue(store.Append(Event("events", 1, "mini_enter", "room-a")));
            File.AppendAllText(Path.Combine(directory, "events-live.jsonl"), "{\"runId\":\"events\",\"type\":");
            Assert.IsTrue(store.Append(Event("events", 2, "mini_success", "room-a")));
            var rows = Export("events.jsonl").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(2, rows.Length);
            StringAssert.Contains("mini_enter", rows[0]); StringAssert.Contains("mini_success", rows[1]);
        }
        [Test] public void CharacterAndStageSummariesSeparateClearFailAndInterruptedDenominators()
        {
            var clear = Run("a"); store.Begin(clear);
            clear.normalKills = 10; clear.dashCount = 6; clear.elapsedSeconds = 100; clear.status = "clear"; store.Save(clear);
            var fail = Run("b"); store.Begin(fail);
            fail.normalKills = 4; fail.dashCount = 2; fail.elapsedSeconds = 20; fail.status = "fail"; store.Save(fail);
            var abandoned = Run("c"); store.Begin(abandoned); abandoned.status = "abandoned"; store.Save(abandoned);
            string summary = Export("character-summary.csv");
            StringAssert.Contains("\"Ashi\",\"3\",\"1\",\"1\",\"1\",\"0\",\"0\",\"0.5\",\"10\",\"4\",\"6\",\"2\",\"100\",\"20\"", summary);
            StringAssert.Contains("\"1\",\"3\",\"1\",\"1\"", Export("stage-summary.csv"));
        }
        [Test] public void UnknownRunsCannotInjectEventsAndCsvEscapesFormulaCells()
        {
            Assert.IsFalse(store.Append(Event("unknown", 1, "augment_selected", "unsafe")));
            Assert.IsTrue(store.Begin(Run("valid")));
            Assert.IsTrue(store.Append(Event("valid", 1, "item_acquired", "=1+1")));
            StringAssert.Contains("\"'=1+1\"", Export("choices.csv"));
            Assert.IsFalse(store.Begin(Run("../escape")));
        }
    }
}
