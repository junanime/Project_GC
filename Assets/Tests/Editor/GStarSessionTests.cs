using System;
using System.IO;
using NUnit.Framework;

namespace Vampire.Tests.Editor
{
    public sealed class GStarSessionTests
    {
        string directory;
        bool? priorEnabled;

        [SetUp] public void SetUp()
        {
            priorEnabled = GStarDemo.TestOverride;
            GStarDemo.TestOverride = true;
            directory = Path.Combine(Path.GetTempPath(), "ProjectGC-GStarSession-" + Guid.NewGuid().ToString("N"));
            GStarSession.TestDirectory = directory;
            GStarSession.ReloadForTests();
        }
        [TearDown] public void TearDown()
        {
            GStarSession.TestDirectory = null; GStarSession.ReloadForTests();
            GStarDemo.TestOverride = priorEnabled;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test] public void ThreeDeparturesMayReplayTheSameStageAndCannotDoubleCharge()
        {
            string session = GStarSession.SessionId, priorRun = "";
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(GStarSession.TryBeginAttempt(2, out var reason), reason);
                Assert.AreEqual(2 - i, GStarSession.RemainingAttempts);
                Assert.AreNotEqual(priorRun, GStarSession.RunId);
                Assert.IsFalse(GStarSession.TryBeginAttempt(1, out _));
                Assert.AreEqual(2 - i, GStarSession.RemainingAttempts);
                Assert.IsTrue(GStarSession.MarkAttemptStarted());
                Assert.IsTrue(GStarSession.CompleteAttempt(i != 1));
                Assert.IsFalse(GStarSession.CompleteAttempt(false));
                Assert.AreEqual(i != 1, GStarSession.LastSuccess);
                priorRun = GStarSession.RunId;
                Assert.AreEqual(session, GStarSession.SessionId);
            }
            Assert.IsFalse(GStarSession.TryBeginAttempt(0, out _));
            Assert.AreEqual(0, GStarSession.RemainingAttempts);
        }

        [Test] public void FailedLoadingRefundsOnceButPlayedRunNeverRefunds()
        {
            Assert.IsTrue(GStarSession.TryBeginAttempt(0, out _));
            Assert.IsTrue(GStarSession.CancelPendingAttempt());
            Assert.IsFalse(GStarSession.CancelPendingAttempt());
            Assert.AreEqual(3, GStarSession.RemainingAttempts);
            Assert.IsTrue(GStarSession.TryBeginAttempt(1, out _));
            Assert.IsTrue(GStarSession.MarkAttemptStarted());
            Assert.IsFalse(GStarSession.CancelPendingAttempt());
            Assert.IsFalse(GStarSession.ResetVisitor());
            Assert.AreEqual(2, GStarSession.RemainingAttempts);
        }

        [Test] public void RestartDuringLoadingRefundsAndRestartDuringGameplayConsumes()
        {
            Assert.IsTrue(GStarSession.TryBeginAttempt(0, out _));
            GStarSession.ReloadForTests();
            Assert.AreEqual(3, GStarSession.RemainingAttempts);
            Assert.IsFalse(GStarSession.ActiveAttempt);
            Assert.IsTrue(GStarSession.TryBeginAttempt(2, out _));
            string run = GStarSession.RunId;
            Assert.IsTrue(GStarSession.MarkAttemptStarted());
            GStarSession.ReloadForTests();
            Assert.AreEqual(2, GStarSession.RemainingAttempts);
            Assert.IsFalse(GStarSession.ActiveAttempt);
            Assert.IsTrue(GStarSession.Interrupted);
            Assert.IsTrue(GStarSession.HasCompletedAttempt);
            Assert.AreEqual(run, GStarSession.LastRunId);
            Assert.AreEqual(2, GStarSession.StageId);
        }

        [Test] public void PreparationAndApplicationRestartKeepFinishedRunAndIdentity()
        {
            Assert.IsTrue(GStarSession.TryBeginAttempt(1, out _));
            Assert.IsTrue(GStarSession.MarkAttemptStarted());
            Assert.IsTrue(GStarSession.CompleteAttempt(true));
            Assert.IsTrue(GStarSession.SetIdentity("테스트 참가자", "01000000000"));
            string run = GStarSession.RunId, session = GStarSession.SessionId;
            GStarSession.ReloadForTests();
            Assert.AreEqual(2, GStarSession.RemainingAttempts);
            Assert.AreEqual(run, GStarSession.RunId);
            Assert.AreEqual(session, GStarSession.SessionId);
            Assert.AreEqual("테스트 참가자", GStarSession.PlayerName);
            Assert.AreEqual("01000000000", GStarSession.Phone);
            Assert.IsTrue(GStarSession.LastSuccess);
        }

        [Test] public void SurveyPersistsUntilExplicitNextVisitorAndClearsContactData()
        {
            Assert.IsTrue(GStarSession.SetIdentity("테스트", "01000000000"));
            string session = GStarSession.SessionId;
            Assert.IsTrue(GStarSession.FinishVisitor());
            GStarSession.ReloadForTests();
            Assert.IsTrue(GStarSession.VisitorFinished);
            Assert.IsFalse(GStarSession.TryBeginAttempt(1, out _));
            Assert.IsTrue(GStarSession.ResetVisitor());
            Assert.AreNotEqual(session, GStarSession.SessionId);
            Assert.AreEqual("", GStarSession.PlayerName);
            Assert.AreEqual("", GStarSession.Phone);
            Assert.AreEqual(3, GStarSession.RemainingAttempts);
            Assert.IsFalse(GStarSession.HasCompletedAttempt);
        }

        [Test] public void InvalidStageAndDisabledEventCannotConsumeAttempts()
        {
            Assert.IsFalse(GStarSession.TryBeginAttempt(-1, out _));
            Assert.IsFalse(GStarSession.TryBeginAttempt(3, out _));
            Assert.AreEqual(3, GStarSession.RemainingAttempts);
            GStarDemo.TestOverride = false;
            Assert.IsTrue(GStarSession.TryBeginAttempt(1, out _));
            Assert.IsFalse(GStarSession.ActiveAttempt);
            Assert.AreEqual(3, GStarSession.RemainingAttempts);
        }

        [Test] public void CorruptPrimaryRecoversBackupRatherThanCreatingANewVisitor()
        {
            Assert.IsTrue(GStarSession.TryBeginAttempt(2, out _));
            Assert.IsTrue(GStarSession.MarkAttemptStarted());
            Assert.IsTrue(GStarSession.CompleteAttempt(true));
            Assert.IsTrue(GStarSession.SetIdentity("참가자", "01000000000"));
            string session = GStarSession.SessionId;
            File.WriteAllText(GStarSession.FilePath, "not valid json");
            GStarSession.ReloadForTests();
            Assert.AreEqual(session, GStarSession.SessionId);
            Assert.AreEqual(2, GStarSession.RemainingAttempts);
            Assert.IsTrue(GStarSession.HasCompletedAttempt);
        }

        [Test] public void UnrecoverableSessionRequiresExplicitStaffReset()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(GStarSession.FilePath, "broken");
            File.WriteAllText(GStarSession.FilePath + ".bak", "broken backup");
            GStarSession.ReloadForTests();
            Assert.AreEqual(0, GStarSession.RemainingAttempts);
            Assert.IsTrue(GStarSession.VisitorFinished);
            Assert.IsNotEmpty(GStarSession.StorageError);
            Assert.IsFalse(GStarSession.TryBeginAttempt(1, out _));
            Assert.IsTrue(GStarSession.ResetVisitor());
            Assert.AreEqual(3, GStarSession.RemainingAttempts);
        }
    }
}
