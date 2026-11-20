using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public sealed class GStarLeaderboardTests
    {
        string directory;
        GStarLeaderboardStore store;
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "ProjectGC-GStarRanking-" + Guid.NewGuid().ToString("N"));
            store = new GStarLeaderboardStore(directory);
        }
        [TearDown] public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        static RunScoreData Score(int normal = 1) => new RunScoreData { rules = GStarScoreRules.Version, normal = normal };
        GStarLeaderboard.Registration Register(string run, int stage, string phone, int normal = 1, string name = "참가자", string session = "visitor")
        {
            var data = Score(normal);
            Assert.IsTrue(store.CaptureCompleted(run, session, stage, data), store.LastError);
            return store.Submit(name, phone, stage, run, session, data);
        }
        [Test] public void DrawIncludesLowerRanksAndKeepsOnlyPersonalStageBests()
        {
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(Register("draw-" + i, 0, "", 10 - i, session: "visitor" + i).success);
            Assert.IsTrue(Register("repeat", 0, "", 1, session: "visitor4").success);
            Assert.IsTrue(Register("alone", 2, "", 2, session: "visitor4").success);
            Assert.IsTrue(store.CaptureCompleted("unregistered", "guest", 0, Score(50)));
            var rows = File.ReadAllLines(Path.Combine(directory, "prize-draw-private.csv"));
            Assert.AreEqual(7, rows.Length);
            for (int i = 0; i < 5; i++)
            {
                StringAssert.Contains("\"draw-" + i + "\"", rows[1+i]);
                StringAssert.EndsWith("\"" + (8 - i) + "\"", rows[1+i]);
            }
            StringAssert.EndsWith("\"1\"", rows[6]);
            StringAssert.DoesNotContain("unregistered", string.Join("\n", rows));
        }

        [Test] public void OpenPrivateWorkbookDoesNotBlockUpdatedDrawOrPublicRanking()
        {
            Assert.IsTrue(Register("before", 2, "", 10).success);
            string all = Path.Combine(directory, "all-runs-private.csv");
            using (var workbook = File.Open(all, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.IsTrue(Register("during", 2, "", 4, "새참가자", "another").success);
                StringAssert.Contains("새참가자", File.ReadAllText(Path.Combine(directory, "ranking-public.csv")));
                StringAssert.Contains("\"during\"", File.ReadAllText(Path.Combine(directory, "prize-draw-private.csv")));
                Assert.IsNotNull(new GStarLeaderboardStore(directory).GetRunRank("during"));
            }
            Assert.IsTrue(store.CaptureCompleted("after", "another", 1, Score()));
            StringAssert.Contains("\"during\"", File.ReadAllText(all));
        }

        [Test] public void OfficialStandingUsesOnePhoneBestPerStageAndPreservesAllRuns()
        {
            Assert.IsTrue(Register("first", 1, "010-1234-5678", 10).success);
            Assert.IsTrue(Register("competitor", 1, "01099998888", 8, session: "competitor").success);
            var lower = Register("lower", 1, "+82 10 1234 5678", 2);
            Assert.AreEqual(20, lower.submittedScore);
            Assert.AreEqual(100, lower.bestScore);
            Assert.AreEqual(1, lower.current.rank);
            Assert.AreEqual("first", lower.current.runId);
            Assert.IsFalse(lower.improvedBest);
            Assert.AreEqual(2, store.GetTop(1).Count);
            Assert.IsTrue(Register("other-stage", 2, "01012345678", 3).success);
            Assert.AreEqual(30, store.GetTop(2)[0].score);
            Assert.IsEmpty(store.GetTop(0));
            Assert.AreEqual(4, store.RunCount);
            var restarted = new GStarLeaderboardStore(directory);
            Assert.AreEqual(4, restarted.RunCount);
            Assert.AreEqual(1, restarted.GetRunRank("lower").rank);
        }
        [Test] public void DuplicateRegistrationCannotChangeIdentityOrOverwriteFinishedScore()
        {
            Assert.IsTrue(Register("run", 0, "01011112222", 3).success);
            Assert.IsTrue(store.CaptureCompleted("run", "visitor", 0, Score(999)));
            var duplicate = store.Submit("변경", "01011112222", 0, "run", "visitor", Score(999));
            Assert.IsTrue(duplicate.success);
            Assert.IsTrue(duplicate.duplicate);
            Assert.AreEqual(30, duplicate.submittedScore);
            Assert.AreEqual("참가자", duplicate.current.name);
            Assert.IsFalse(store.Submit("타인", "01099992222", 0, "run", "visitor", Score()).success);
            Assert.AreEqual(1, store.RunCount);
            Assert.IsFalse(store.CaptureCompleted("run", "other-session", 0, Score()));
        }
        [Test] public void OptionalPhoneUsesVisitorBestAndDoesNotMergeSameNamedVisitors()
        {
            var first = Register("anonymous-first", 1, "", 10, "익명참가자");
            Assert.IsTrue(first.success, first.message);
            Assert.IsTrue(first.message.Contains("전화번호 없이"));
            var lower = Register("anonymous-lower", 1, "   ", 4, "익명참가자");
            Assert.IsTrue(lower.success);
            Assert.AreEqual(100, lower.bestScore);
            Assert.AreEqual("anonymous-first", lower.current.runId);
            Assert.AreEqual(1, store.GetTop(1).Count);
            Assert.AreEqual(1, store.GetParticipantRank(null, "visitor", 1).rank);
            Assert.IsNull(store.GetPlayerRank("", 1));
            Assert.IsTrue(store.CaptureCompleted("anonymous-other", "second-visitor", 1, Score(8)));
            Assert.IsTrue(store.Submit("익명참가자", null, 1, "anonymous-other", "second-visitor", Score(8)).success);
            Assert.AreEqual(2, store.GetTop(1).Count);
            Assert.AreEqual(2, store.GetParticipantRank("", "second-visitor", 1).rank);
            Assert.IsTrue(Register("anonymous-other-stage", 2, "", 3, "익명참가자").success);
            var restarted = new GStarLeaderboardStore(directory);
            Assert.AreEqual(100, restarted.GetRunRank("anonymous-lower").score);
            Assert.AreEqual(30, restarted.GetParticipantRank("", "visitor", 2).score);
            string privateCsv = File.ReadAllText(Path.Combine(directory, "prize-top3-private.csv"));
            StringAssert.Contains("\"익명참가자\",\"\",", privateCsv);
            Assert.IsFalse(Register("invalid-phone", 0, "not-a-phone").success);
            Assert.IsFalse(Register("missing-name", 0, "", 1, "").success);
        }
        [Test] public void LaterPhoneLinksAnonymousAttemptsWithoutAddingContactToOriginalRecord()
        {
            Assert.IsTrue(Register("anonymous", 1, "", 10).success);
            Assert.IsTrue(Register("older-phone-visit", 1, "01012345678", 8, session: "older-visitor").success);
            Assert.AreEqual(2, store.GetTop(1).Count);
            var contact = Register("contact-later", 1, "01012345678", 6);
            Assert.IsTrue(contact.success, contact.message);
            Assert.IsFalse(contact.improvedBest);
            Assert.AreEqual(1, store.GetTop(1).Count);
            Assert.AreEqual("anonymous", contact.current.runId);
            var blankAgain = Register("blank-again", 1, "", 4);
            Assert.IsTrue(blankAgain.success);
            Assert.AreEqual(100, blankAgain.bestScore);
            Assert.AreEqual(1, store.GetTop(1).Count);
            Assert.AreEqual(100, store.GetParticipantRank("", "visitor", 1).score);
            Assert.AreEqual(100, store.GetPlayerRank("01012345678", 1).score);
            Assert.IsFalse(Register("different-number", 2, "01099998888").success);
            var restarted = new GStarLeaderboardStore(directory);
            Assert.AreEqual("anonymous", restarted.GetRunRank("contact-later").runId);
            string allRuns = File.ReadAllText(Path.Combine(directory, "all-runs-private.csv"));
            var anonymousLine = Array.Find(allRuns.Split('\n'), line => line.StartsWith("\"anonymous\","));
            Assert.IsNotNull(anonymousLine);
            Assert.IsFalse(anonymousLine.Contains("01012345678"), "Do not retroactively insert contact into anonymous record");
            foreach (string file in new[] { "prize-draw-private.csv", "prize-top3-private.csv" })
            {
                string row = File.ReadAllLines(Path.Combine(directory, file))[1];
                StringAssert.Contains("\"anonymous\"", row, "Personal best keeps its original run");
                StringAssert.Contains("01012345678", row, "Private prize lists resolve the participant's later contact");
            }
            StringAssert.DoesNotContain("01012345678", File.ReadAllText(Path.Combine(directory, "ranking-public.csv")));
        }
        [Test] public void ExcludedAndUnfinishedRunsNeverEnterPublicRanking()
        {
            Assert.IsFalse(store.Submit("참가자", "01011112222", 1, "missing", "visitor", Score()).success);
            Assert.IsTrue(store.CaptureCompleted("test", "visitor", 1, Score(500), "검수용"));
            Assert.IsFalse(store.Submit("참가자", "01011112222", 1, "test", "visitor", Score()).success);
            Assert.IsEmpty(store.GetTop(1));
            Assert.AreEqual(1, store.RunCount);
        }
        [Test] public void CorruptPrimaryRecoversLatestRegistrationFromBackup()
        {
            Register("one", 2, "01011112222");
            Register("two", 2, "01033334444", 5, session: "second-visitor");
            File.WriteAllText(store.FilePath, "{interrupted-file");
            var restarted = new GStarLeaderboardStore(directory);
            Assert.AreEqual(2, restarted.RunCount);
            Assert.AreEqual("two", restarted.GetTop(2)[0].runId);
            Assert.AreEqual(2, new GStarLeaderboardStore(directory).RunCount);
        }
        [Test] public void InvalidPrimaryAndBackupArePreservedInsteadOfResettingScores()
        {
            Register("one", 1, "01011112222");
            File.WriteAllText(store.FilePath, "bad-primary");
            File.WriteAllText(store.FilePath + ".bak", "bad-backup");
            var restarted = new GStarLeaderboardStore(directory);
            Assert.IsFalse(restarted.CaptureCompleted("new", "visitor", 1, Score()));
            Assert.IsNotEmpty(restarted.LastError);
            Assert.AreEqual("bad-primary", File.ReadAllText(store.FilePath));
            Assert.AreEqual("bad-backup", File.ReadAllText(store.FilePath + ".bak"));
        }
        [Test] public void EqualScoresRetainEarlierCompletionOrderAcrossRestarts()
        {
            Register("a", 1, "01011112222", 10);
            Register("b", 1, "01033334444", 10, session: "second-visitor");
            Assert.AreEqual("a", store.GetTop(1)[0].runId);
            Assert.AreEqual("a", new GStarLeaderboardStore(directory).GetTop(1)[0].runId);
        }
        [Test] public void CsvSeparatesPrivateContactsAndEscapesSpreadsheetFormulas()
        {
            Assert.IsTrue(Register("run", 1, "01011112222", 10, "-참가자").success);
            string publicCsv = File.ReadAllText(Path.Combine(directory, "ranking-public.csv"));
            string privateCsv = File.ReadAllText(Path.Combine(directory, "prize-top3-private.csv"));
            Assert.IsFalse(publicCsv.Contains("01011112222"));
            Assert.IsTrue(publicCsv.Contains("'-참가자"));
            Assert.IsTrue(privateCsv.Contains("'01011112222"));
            Assert.IsFalse(store.Submit("<b>이름</b>", "01011112222", 1, "run", "visitor", Score()).success);
        }
        [Test] public void CountersAndTotalSaturateAndEntryAloneAwardsNothing()
        {
            var score = Score(0); score.entered = 500;
            Assert.AreEqual(0, score.Total);
            score.normal = 1; score.special = 1; score.elite = 1; score.miniBoss = 1;
            score.boss = 1; score.cleared = 1; score.overcharge = 1; score.success = true;
            score.gold = 1; score.silver = 1;
            Assert.AreEqual(19338, score.Total);
            int counter = int.MaxValue;
            GStarScoreRules.Add(ref counter, int.MaxValue);
            Assert.AreEqual(GStarScoreRules.MaximumCount, counter);
            GStarScoreRules.Add(ref counter, -5);
            Assert.AreEqual(GStarScoreRules.MaximumCount, counter);
            score.boss = int.MaxValue;
            Assert.AreEqual(int.MaxValue, score.Total);
            Assert.AreEqual(30, new RunScoreData { normal = 1 }.Total, "Non-event QA rules must remain unchanged");
        }
        [Test] public void MiniStageAndOverchargeCannotAwardTwiceForSameSource()
        {
            var host = new GameObject("score-test"); var portal = new GameObject("portal"); var second = new GameObject("second");
            try
            {
                var score = host.AddComponent<RunScore>(); score.Data.rules = GStarScoreRules.Version;
                score.Clear(); Assert.AreEqual(0, score.Data.cleared);
                score.Enter(portal); score.Clear(); score.Clear(); score.Enter(portal); score.Clear();
                score.Overcharge(portal); score.Overcharge(portal);
                Assert.AreEqual(1, score.Data.cleared); Assert.AreEqual(1, score.Data.overcharge);
                score.Enter(second); score.Clear(); score.Overcharge(second);
                Assert.AreEqual(2, score.Data.cleared); Assert.AreEqual(2, score.Data.overcharge);
                score.Gold(10); score.Gold(-10); score.Silver(2);
                Assert.AreEqual(10, score.Data.gold); Assert.AreEqual(2, score.Data.silver);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(portal); UnityEngine.Object.DestroyImmediate(second); }
        }
    }
}
