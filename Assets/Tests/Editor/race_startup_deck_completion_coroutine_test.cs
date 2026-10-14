using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// Real Start decks, default speed distribution and team trick cards survive a
/// complete authored race. Fixed random seeds are restored by the shared scope.
/// Except for explicit authored-weather/pit cases, weather and pit are disabled.
/// Gear-one/headless remain bounded fixture settings; the explicit controlled pit case replaces decks. This is
/// not a normal-settings, visual, physical-input or real-storage acceptance run.
/// Natural AI/player retirement is a valid outcome, never injected by this case.
/// </summary>
public partial class RaceGameLoopCoroutineTests
{
    // Combined subsystem path only; the bounded headless input policy is not
    // a default-settings or physical-button acceptance run.
    [UnityTest]
    public IEnumerator RealStartupDeckCompletesShanghaiWithWeatherAndPit()
    {
        yield return CompleteAuthoredRace(TeamId.CN, 0, false,
            useStartupDeck: true, startupSeed: 2101, useAuthoredWeather: true, useAuthoredPit: true);
        yield return CompleteAuthoredRace(TeamId.US, 0, false,
            useStartupDeck: true, startupSeed: 2103, useAuthoredWeather: true, useAuthoredPit: true);
    }

    // Keep the actual Start cards and native recycling while exercising entry,
    // the consumed stop turn, reopened input and later-lap skip choices.
    [UnityTest]
    public IEnumerator RealStartupDeckCompletesShanghaiPitWithoutReplacingCards()
    {
        yield return CompleteAuthoredRace(TeamId.CN, 0, false,
            useStartupDeck: true, startupSeed: 2101, useAuthoredPit: true);
        yield return CompleteAuthoredRace(TeamId.US, 0, false,
            useStartupDeck: true, startupSeed: 2103, useAuthoredPit: true);
    }

    // Controlled speed-one decks, one permanent heat card and a waiting panel;
    // actual Start/GameLoop/Shanghai laps, not default settings or pointer UI.
    [UnityTest]
    public IEnumerator RealAuthoredPitChoiceSuspendsResumesAndCompletesShanghaiRace()
    {
        foreach (TeamId team in new[] { TeamId.CN, TeamId.US })
            yield return CompleteAuthoredRace(team, 0, false, useAuthoredPit: true);
    }

    // Real authored weather; gear-one/no pit/headless/isolated saves remain
    // bounded settings, not a normal-settings acceptance run.
    [UnityTest]
    public IEnumerator RealStartupDeckCompletesWithAuthoredWeatherAndSharedLapGate()
    {
        foreach (int seed in new[] { 2001, 2023 })
            yield return CompleteAuthoredRace(TeamId.UK, 0, false,
                useStartupDeck: true, startupSeed: seed, useAuthoredWeather: true);
    }

    [UnityTest]
    public IEnumerator RealStartupDeckCompletesAcrossTeamsWithoutReplacingCards()
    {
        TeamId[] teams = { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP };
        for (int index = 0; index < teams.Length; index++)
            yield return CompleteAuthoredRace(teams[index], 0, false,
                useStartupDeck: true, startupSeed: 1701 + index);
        // Same team with a different seed guards against a single lucky opening.
        yield return CompleteAuthoredRace(TeamId.UK, 0, false,
            useStartupDeck: true, startupSeed: 1723);
    }

    [UnityTest]
    public IEnumerator RealStartupDeckCompletesCustomRostersAndPreservesParticipantOwnership()
    {
        foreach (int rosterSize in new[] { 2, 6 })
            foreach (int seedOffset in new[] { 1800, 1820 })
                yield return CompleteAuthoredRace(TeamId.UK, rosterSize, false,
                    useStartupDeck: true, startupSeed: seedOffset + rosterSize);
    }

    private void AssertStartupRoster(int rosterSize)
    {
        if (rosterSize == FreeRaceRosterRules.ThunderstormParticipants)
        {
            DriverProfile primary = DriverCatalog.GetDefaultForTeam(TeamId.UK);
            // Independent catalog expectation: do not ask the roster builder
            // under test to produce its own expected output.
            DriverProfile[] drivers = new[] { primary }.Concat(DriverCatalog.All
                .Where(driver => driver.Id != primary.Id)).ToArray();
            CollectionAssert.AreEqual(drivers.Select(driver => driver.Team),
                session.Players.Select(player => player.teamId));
            CollectionAssert.AreEqual(drivers.Select(driver => driver.Id),
                session.Players.Select(player => player.driverId));
            foreach (TeamId team in FreeRaceRosterRules.AvailableTeams)
                Assert.That(session.Players.Count(player => player.teamId == team), Is.EqualTo(2));
        }
        else
        {
            TeamId[] teams = FreeRaceRosterRules.AvailableTeams.Take(rosterSize).ToArray();
            CollectionAssert.AreEqual(teams, session.Players.Select(player => player.teamId));
            CollectionAssert.AreEqual(teams.Select(team => DriverCatalog.GetDefaultForTeam(team).Id),
                session.Players.Select(player => player.driverId));
        }
        Assert.That(session.Players.Select(player => player.driverId).Distinct().Count(), Is.EqualTo(rosterSize));
        CollectionAssert.AreEquivalent(session.Players.Skip(1),
            Get<Dictionary<PlayerState, AIController>>(manager, "aiControllers").Keys);
        foreach (PlayerState player in session.Players)
        {
            Assert.That(player.DriverProfile.Team, Is.EqualTo(player.teamId));
            Assert.That(player.isAI, Is.EqualTo(player != session.Human));
            Assert.That(player.driverXp, Is.EqualTo(player == session.Human ? 137 : 0));
            Assert.That(player.deck.heatPool, Is.Not.Null);
            Assert.That(player.techState, Is.Not.Null);
        }
        Assert.That(session.Players.Select(player => player.deck).Distinct().Count(), Is.EqualTo(rosterSize));
        Assert.That(session.Players.Select(player => player.deck.heatPool).Distinct().Count(), Is.EqualTo(rosterSize));
        Assert.That(session.Players.Select(player => player.techState).Distinct().Count(), Is.EqualTo(rosterSize));
        Assert.That(session.Players.Select(player => player.driverSkill).Distinct().Count(), Is.EqualTo(rosterSize));
        Assert.That(session.Players.Select(player => player.trickState).Distinct().Count(), Is.EqualTo(rosterSize));
    }

    [UnityTest]
    public IEnumerator RealStartupDeckCompletesThunderstormWithIndependentTeammates()
    {
        foreach (int seed in new[] { 1912, 1932 })
            yield return CompleteAuthoredRace(TeamId.UK, FreeRaceRosterRules.ThunderstormParticipants, false,
                useStartupDeck: true, startupSeed: seed);
    }

    private List<CardData> AssertStartupDecks()
    {
        var initial = new List<CardData>();
        foreach (PlayerState player in session.Players)
        {
            Assert.That(player.deck.UsesExactOrder, Is.False, "Must retain the actual shuffled Start deck");
            List<CardData> cards = PlayableDeckCards(player);
            CollectionAssert.AreEquivalent(config.speedCardDistribution,
                cards.Where(card => card.IsSpeed).Select(card => card.value));
            CollectionAssert.AreEquivalent(session.CreateInitialTrickCards(player.teamId).Select(card => card.trickId),
                cards.Where(card => card.IsTrick).Select(card => card.trickId));
            Assert.That(cards.Distinct().Count(), Is.EqualTo(cards.Count), "No duplicate runtime card instance");
            initial.AddRange(cards);
        }
        Assert.That(initial.Distinct().Count(), Is.EqualTo(initial.Count), "Participants own separate card instances");
        return initial;
    }

    private void AssertStartupCardsConserved(List<CardData> initial)
    {
        List<CardData> final = session.Players.SelectMany(PlayableDeckCards).ToList();
        Assert.That(session.Players.All(player => !player.deck.UsesExactOrder), Is.True);
        Assert.That(final.Distinct().Count(), Is.EqualTo(final.Count), "No card instance duplicated during recycling");
        CollectionAssert.AreEquivalent(initial, final, "Keep every original non-heat card through native cleanup/reshuffle");
    }

    private static List<CardData> PlayableDeckCards(PlayerState player) => player.deck.Hand
        .Concat(player.deck.DrawPile).Concat(player.deck.DiscardPile)
        .Where(card => !card.IsHeat).ToList();
}
