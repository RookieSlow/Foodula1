using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

// Extends the same native completion fixture, not a second setup or synthetic
// finish. Faults affect memory storage after real startup; retries exercise the
// existing result entry after GameLoop ended, not a rendered retry UI.
public partial class RaceGameLoopCoroutineTests
{
    private enum CareerCompletionFault { None, SaveRejected, SaveThrows, SnapshotChanged, InvalidSave }

    [UnityTest]
    public IEnumerator RealCareerCompletionRejectsFailedOrStaleSavesAndRetriesWithoutNormalRewards()
    {
        yield return CompleteAuthoredRace(TeamId.UK, 0, false, true, CareerCompletionFault.SaveRejected);
        yield return CompleteAuthoredRace(TeamId.CN, 0, true, true, CareerCompletionFault.SaveThrows);
        yield return CompleteAuthoredRace(TeamId.US, 0, false, true, CareerCompletionFault.SnapshotChanged);
        yield return CompleteAuthoredRace(TeamId.IT, 0, false, true, CareerCompletionFault.InvalidSave);
    }

    private sealed partial class CareerCompletionContext
    {
        private CareerCompletionFault fault;
        private string originalJson;
        private string rejectedJson;
        private bool retrying;
        private readonly List<string> writeMoments = new List<string>();
        private bool HasInjectedFault => fault != CareerCompletionFault.None;
        private bool IsStorageFailure => fault == CareerCompletionFault.SaveRejected || fault == CareerCompletionFault.SaveThrows;
        private string RejectionReason => IsStorageFailure
            ? "生涯赛果保存失败，进度未推进" : "生涯存档已变化，未写入本场结果";

        internal void ConfigureFault(CareerCompletionFault value)
        {
            fault = value;
            originalJson = store.Json;
        }

        internal void ApplyFaultAfterStartup()
        {
            store.FailWrites = fault == CareerCompletionFault.SaveRejected;
            store.ThrowOnWrite = fault == CareerCompletionFault.SaveThrows;
            if (fault == CareerCompletionFault.SnapshotChanged)
            {
                var serializer = new JsonUtilityCareerSerializer();
                CareerSaveData data = serializer.Deserialize(originalJson);
                data.initialTech.rpBalance++;
                // An externally changed, still-valid authority, not a forged race result.
                store.ReplaceExternalValue(serializer.Serialize(data));
                Assert.That(repository.Load().Status, Is.EqualTo(CareerLoadStatus.Loaded));
                Assert.That(launch.Matches(repository.Load().State), Is.False);
            }
            else if (fault == CareerCompletionFault.InvalidSave)
            {
                store.ReplaceExternalValue("invalid-career-value-preserved-by-fixture");
                Assert.That(repository.Load().Status, Is.EqualTo(CareerLoadStatus.Invalid));
            }
            rejectedJson = store.Json;
        }

        internal void AssertResultRepositoryEntry(MVPGameManager manager)
        {
            Assert.That(RepositoryRequests, Is.EqualTo(retrying ? 3 : 2), "Startup, first result, optional explicit retry only.");
            Assert.That(manager.Session.IsRaceOver(), Is.True, "No persistence while surviving AI race.");
            Assert.That(manager.hudUI.gameOverPanel.activeSelf, Is.EqualTo(retrying), "First result saves before UI.");
        }

        internal void ObserveResultWrite(MVPGameManager manager)
        {
            writeMoments.Add(manager.Session.IsRaceOver() + ":" + manager.hudUI.gameOverPanel.activeSelf + ":" + RepositoryRequests);
        }

        private void AssertFirstWriteEvidence()
        {
            bool attempted = !HasInjectedFault || IsStorageFailure;
            Assert.That(store.WriteAttempts, Is.EqualTo(attemptsBeforeRace + (attempted ? 1 : 0)));
            Assert.That(writeMoments, Is.EqualTo(attempted ? new[] { "True:False:2" } : Array.Empty<string>()));
        }

        private void AssertRejected(MVPGameManager manager)
        {
            Assert.That(store.Json, Is.EqualTo(rejectedJson), "Rejection must preserve the exact stored token, including invalid data.");
            Assert.That(Get<bool>(manager, "careerResultRecorded"), Is.False);
            CareerLoadResult loaded = repository.Load();
            Assert.That(loaded.Status, Is.EqualTo(fault == CareerCompletionFault.InvalidSave
                ? CareerLoadStatus.Invalid : CareerLoadStatus.Loaded));
            Assert.That(loaded.State.NextTrackIndex, Is.Zero);
            Assert.That(loaded.State.RaceResults, Is.Empty);
            if (loaded.Status == CareerLoadStatus.Loaded)
            {
                Assert.That(loaded.State.ActiveTechSnapshot.RpBalance,
                    Is.EqualTo(fault == CareerCompletionFault.SnapshotChanged ? 25001 : 25000));
                Assert.That(CareerModeRules.GetStandings(loaded.State).All(s => s.Points == 0), Is.True);
            }
            Assert.That(manager.hudUI.gameOverText.text, Does.Contain(RejectionReason)
                .And.Not.Contain("生涯赛果已保存").And.Not.Contain("RP 奖励").And.Not.Contain("车手 XP"));
        }

        internal void AssertRecovery(MVPGameManager manager, bool retiredHuman, string logPath, string rejectedTrace)
        {
            if (!HasInjectedFault) return;
            var before = manager.Session.Players.Select(p => Tuple.Create(p.position, p.lap, p.finishOrder,
                p.isBlown, p.hasFinished, p.techState.rpBalance, p.driverXp)).ToArray();
            store.FailWrites = store.ThrowOnWrite = false;
            // Restoring the original valid authority is a fixture-only memory input.
            // Never edit PlayerPrefs or claim the rejected log means saved progress.
            store.ReplaceExternalValue(originalJson);
            Assert.That(repository.Load().Status, Is.EqualTo(CareerLoadStatus.Loaded));
            retrying = true;
            var resultEntry = typeof(MVPGameManager).GetMethod("ShowGameOver", PrivateInstance);
            resultEntry.Invoke(manager, null);
            Assert.That(Get<bool>(manager, "careerResultRecorded"), Is.True);
            AssertSavedState(manager, retiredHuman);
            Assert.That(store.SuccessfulWrites, Is.EqualTo(2));
            Assert.That(RepositoryRequests, Is.EqualTo(3));
            int expectedAttempts = IsStorageFailure ? 3 : 2;
            Assert.That(store.WriteAttempts, Is.EqualTo(expectedAttempts));
            Assert.That(writeMoments, Is.EqualTo(IsStorageFailure
                ? new[] { "True:False:2", "True:True:3" } : new[] { "True:True:3" }));
            string acceptedJson = store.Json;
            for (int repeat = 0; repeat < 2; repeat++) resultEntry.Invoke(manager, null);
            Assert.That(store.Json, Is.EqualTo(acceptedJson), "Repeated callbacks cannot duplicate scoring or progression.");
            Assert.That(store.SuccessfulWrites, Is.EqualTo(2));
            Assert.That(store.WriteAttempts, Is.EqualTo(expectedAttempts));
            Assert.That(RepositoryRequests, Is.EqualTo(3), "Saved callbacks must not reopen the repository.");
            Assert.That(manager.Session.Players.Select(p => Tuple.Create(p.position, p.lap, p.finishOrder,
                p.isBlown, p.hasFinished, p.techState.rpBalance, p.driverXp)), Is.EqualTo(before));
            Assert.That(manager.hudUI.gameOverText.text, Does.Contain("生涯赛果已保存。返回主菜单")
                .And.Not.Contain("RP 奖励").And.Not.Contain("车手 XP"));
            Assert.That(File.ReadAllText(logPath), Is.EqualTo(rejectedTrace), "Retry cannot rewrite the already-closed rejected trace.");
            TestContext.WriteLine($"Native career recovery: fault={fault}; retiredHuman={retiredHuman}; " +
                $"firstResult=rejected/Completed; retry=one-save; repeats=2; writes={store.SuccessfulWrites}; attempts={store.WriteAttempts}; log=unchanged");
        }
    }
}
