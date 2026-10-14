using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Keep NUnit identities stable while separating summer-break assertions from
// completed-season flows. Both partials use the same isolated fixture lifecycle.
public sealed partial class CareerCompletedMenuPointerTests
{
    private CareerTechTreeDraft SummerDraft => (CareerTechTreeDraft)typeof(CareerTechTreeUI)
        .GetField("draft", Private).GetValue(root.GetComponent<CareerTechTreeUI>());

    [TestCase(TeamId.CN)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.UK)]
    [TestCase(TeamId.JP)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.DE)]
    public void SummerCancel_DiscardsUnlockAndToggleWithoutSavingOrLaunching(TeamId team)
    {
        ShowSeason(team, 4);
        string token = store.Token;
        Click("CareerPrimary");
        Assert.That(Text("Team"), Does.Contain(CareerMenuPresentation.GetTeamLabel(team)));
        TechNodeDef node = SummerDraft.GetNodes(TechTreeTier.L1).First(item => SummerDraft.CanUnlock(item.id));
        Click(node.id);
        Assert.That(SummerDraft.IsUnlocked(node.id) && SummerDraft.IsActive(node.id), Is.True);
        Assert.That(SummerDraft.RpBalance, Is.EqualTo(25000 - node.rpCost));
        Click(node.id);
        Assert.That(SummerDraft.IsUnlocked(node.id), Is.True);
        Assert.That(SummerDraft.IsActive(node.id), Is.False);
        Click(node.id);
        Assert.That(SummerDraft.IsActive(node.id), Is.True);
        Assert.That(SummerDraft.RpBalance, Is.EqualTo(25000 - node.rpCost), "Toggling must not charge twice");
        AssertSummerUnchanged(token, 0);

        Click("CareerTechCancel");
        Assert.That(Find("CareerTechTreeOverlay").activeInHierarchy, Is.False);
        Assert.That(Find("CareerOverview").activeInHierarchy, Is.True);
        Assert.That(SummerDraft, Is.Null);
        AssertSummerUnchanged(token, 0);
        Click("CareerPrimary");
        Assert.That(SummerDraft.RpBalance, Is.EqualTo(25000));
        Assert.That(SummerDraft.IsUnlocked(node.id) || SummerDraft.IsActive(node.id), Is.False);
        Click("CareerTechCancel");
        AssertSummerUnchanged(token, 0);
    }

    [TestCase(TeamId.CN)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.UK)]
    [TestCase(TeamId.JP)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.DE)]
    public void SummerConfirm_FailedWritesRetainDraftAndExplicitRetryConsumesGateOnce(TeamId team)
    {
        ShowSeason(team, 4);
        string token = store.Token;
        Click("CareerPrimary");
        TechNodeDef node = SummerDraft.GetNodes(TechTreeTier.L1).First(item => SummerDraft.CanUnlock(item.id));
        Click(node.id);
        CareerTechTreeDraft edited = SummerDraft;
        store.FailWrites = true;
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            Click("CareerTechConfirm");
            Assert.That(Find("CareerTechTreeOverlay").activeInHierarchy, Is.True);
            Assert.That(Text("Hint"), Does.Contain("保存失败"));
            Assert.That(SummerDraft, Is.SameAs(edited));
            Assert.That(SummerDraft.IsActive(node.id), Is.True);
            Assert.That(SummerDraft.RpBalance, Is.EqualTo(25000 - node.rpCost));
            AssertSummerUnchanged(token, attempt);
        }

        store.FailWrites = false;
        Click("CareerTechConfirm");
        Assert.That(store.WriteAttempts, Is.EqualTo(3));
        Assert.That(store.SuccessfulWrites, Is.EqualTo(1));
        Assert.That(store.DeleteAttempts, Is.Zero);
        Assert.That(Find("CareerTechTreeOverlay").activeInHierarchy, Is.False);
        Assert.That(SummerDraft, Is.Null);
        CareerSeasonState state = Service.CurrentState;
        Assert.That(state.Phase, Is.EqualTo(CareerPhase.Racing));
        Assert.That(state.SummerBreakUsed, Is.True);
        Assert.That(state.NextTrackIndex, Is.EqualTo(4));
        Assert.That(state.RaceResults.Count, Is.EqualTo(4));
        Assert.That(state.LockedTeam, Is.EqualTo(team));
        Assert.That(state.InitialTechSnapshot.RpBalance, Is.EqualTo(25000));
        Assert.That(state.InitialTechSnapshot.UnlockedNodeIds, Is.Empty);
        Assert.That(state.InitialTechSnapshot.ActiveNodeIds, Is.Empty);
        Assert.That(state.ActiveTechSnapshot.RpBalance, Is.EqualTo(25000 - node.rpCost));
        CollectionAssert.AreEquivalent(new[] { node.id }, state.ActiveTechSnapshot.UnlockedNodeIds);
        CollectionAssert.AreEquivalent(new[] { node.id }, state.ActiveTechSnapshot.ActiveNodeIds);
        AssertSnapshot(state.ActiveTechSnapshot, state.SummerBreakTechSnapshot);
        Assert.That(state.ActiveTechSnapshot, Is.Not.SameAs(state.InitialTechSnapshot));
        Assert.That(repository.Load().Status, Is.EqualTo(CareerLoadStatus.Loaded));
        Assert.That(Serialize(repository.Load().State), Is.EqualTo(Serialize(state)));
        Assert.That(Button("CareerPrimary").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("开始第 5 站"));
        string saved = store.Token;
        Button confirm = Button("CareerTechConfirm");
        Assert.That(confirm.IsActive(), Is.False);
        ExecuteEvents.Execute(confirm.gameObject, new PointerEventData(eventSystem)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        ui.Hide();
        ui.Show();
        Assert.That(root.GetComponent<CareerTechTreeUI>().Show(state,
            snapshot => { Assert.Fail("Consumed summer break must reject another confirmation"); return false; }, null), Is.False);
        Assert.That(store.Token, Is.EqualTo(saved));
        Assert.That(store.WriteAttempts, Is.EqualTo(3));
        AssertNoLaunch();
        AssertProfilesUnchanged();
    }

    private void AssertSummerUnchanged(string token, int attempts)
    {
        Assert.That(store.Token, Is.EqualTo(token));
        Assert.That(Serialize(Service.CurrentState), Is.EqualTo(token));
        Assert.That(Serialize(repository.Load().State), Is.EqualTo(token));
        Assert.That(Service.CurrentState.Phase, Is.EqualTo(CareerPhase.SummerBreak));
        Assert.That(Service.CurrentState.SummerBreakUsed, Is.False);
        Assert.That(Service.CurrentState.NextTrackIndex, Is.EqualTo(4));
        Assert.That(Service.CurrentState.ActiveTechSnapshot.UnlockedNodeIds, Is.Empty);
        Assert.That(store.WriteAttempts, Is.EqualTo(attempts));
        Assert.That(store.SuccessfulWrites, Is.Zero);
        Assert.That(store.DeleteAttempts, Is.Zero);
        AssertNoLaunch();
        AssertProfilesUnchanged();
    }
}
