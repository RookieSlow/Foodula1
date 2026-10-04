using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Exercises the real human card-phase coordinator without starting a scene or race coroutine.</summary>
public class RacePlayerCardPhaseAdapterTests
{
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    private GameObject host;
    private MVPGameManager manager;
    private GameConfigSO config;
    private RaceInputState input;
    private PlayerState player;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Card phase adapter test");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        manager.cardHandUI = host.AddComponent<CardHandUI>();
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        manager.config = config;

        var session = new RaceSession(new SystemRandomSource(1));
        player = new PlayerState("driver", false, 8, 1) { teamId = TeamId.UK };
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 3) }, new HeatPool(6));
        session.Players.Add(player);
        typeof(MVPGameManager).GetField("session", PrivateInstance).SetValue(manager, session);

        input = (RaceInputState)typeof(MVPGameManager).GetField("inputState", PrivateInstance).GetValue(manager);
        input.BeginCardSelection();
    }

    [TearDown]
    public void TearDown()
    {
        if (host != null) Object.DestroyImmediate(host);
        if (config != null) Object.DestroyImmediate(config);
    }

    private void Finish()
    {
        MethodInfo method = typeof(MVPGameManager).GetMethod("FinishPlayerCardPhase", PrivateInstance);
        Assert.IsNotNull(method, "Card-phase coordinator changed; update this adapter fixture.");
        method.Invoke(manager, new object[] { player });
    }

    [Test]
    public void RequiredCardPlayedClosesInputWithoutChargingHeatAndKeepsPlayedOwnership()
    {
        var played = new CardData(CardType.Speed, 3);
        player.playedSpeedCardsThisTurn.Add(played);
        player.extraCardSlotsThisTurn = 1; // Optional slots do not raise the minimum.

        Finish();

        Assert.IsFalse(input.WaitingForCards);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.IsEmpty(player.heatPaidCardsThisTurn);
        CollectionAssert.AreEqual(new[] { played }, player.playedSpeedCardsThisTurn);
        Assert.AreEqual(0, player.spinCounter);
    }

    [TestCase(2, 1, 5)]
    [TestCase(3, 2, 4)]
    public void MissingRequiredCardsChargeExactHeatAndCloseInput(int gear, int missing, int remaining)
    {
        player.gear = gear;
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 3));

        Finish();

        Assert.IsFalse(input.WaitingForCards);
        Assert.AreEqual(remaining, player.deck.heatPool.remaining);
        Assert.AreEqual(missing, player.deck.CountHeatInHand());
        Assert.AreEqual(missing, player.heatPaidCardsThisTurn.Count);
        Assert.AreEqual(1, player.playedSpeedCardsThisTurn.Count);
        Assert.AreEqual(0, player.spinCounter);
    }

    [Test]
    public void EmptyEngineSpinsButLeavesCommittedSpeedForTurnCleanup()
    {
        player.gear = 2;
        player.deck.DrawHeatFromPool(6);
        var played = new CardData(CardType.Speed, 3);
        player.playedSpeedCardsThisTurn.Add(played);
        player.playedHeatCardsThisTurn.Add(CardData.CreateTempHeat());

        Finish();

        Assert.IsFalse(input.WaitingForCards);
        Assert.AreEqual(1, player.spinCounter);
        Assert.IsTrue(player.skipNextTurn);
        Assert.AreEqual(1, player.gear);
        Assert.IsEmpty(player.playedHeatCardsThisTurn);
        CollectionAssert.AreEqual(new[] { played }, player.playedSpeedCardsThisTurn);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
    }
}
