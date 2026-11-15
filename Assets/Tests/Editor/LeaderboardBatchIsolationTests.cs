using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public sealed class LeaderboardBatchIsolationTests
    {
        static void CompletedWithoutWork(Task task)
        {
            Assert.IsTrue(task.IsCompleted, "Batch isolation must return immediately without network work");
            Assert.IsFalse(task.IsFaulted);
            Assert.IsFalse(task.IsCanceled);
        }
        [Test] public void BatchQaDoesNotAuthenticateOrSubmit()
        {
            if (!Application.isBatchMode) Assert.Ignore("Only exercise this isolation boundary in automated editor sessions");
            Assert.IsTrue(QaLeaderboard.IsBatchQa);
            CompletedWithoutWork(QaLeaderboard.Ready());
            var begin = QaLeaderboard.BeginRun(); CompletedWithoutWork(begin); Assert.IsNull(begin.Result);
            var submit = QaLeaderboard.Submit("batch-probe-never-sent", new RunScoreData());
            CompletedWithoutWork(submit); Assert.IsFalse(submit.Result);
            var nickname = QaLeaderboard.SetNickname("BatchProbe");
            CompletedWithoutWork(nickname); Assert.IsFalse(nickname.Result);
            QaLeaderboard.LoadCache(); QaLeaderboard.Refresh(true);
            Assert.IsNull(QaLeaderboard.PlayerId);
            Assert.AreEqual("검수", QaLeaderboard.Nickname);
            Assert.AreEqual("자동 검수 · 순위 연결 제외", QaLeaderboard.Status);
        }
    }
}
