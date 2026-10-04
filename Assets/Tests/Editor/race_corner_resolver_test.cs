using System.Collections.Generic;
using NUnit.Framework;

public class RaceCornerResolverTests
{
    [Test]
    public void ResolvesSafeAndOverspeedCornersInPathOrder()
    {
        var player = new PlayerState("Racer", false, 0, 1) { cornerTotalThisTurn = 5 };
        var events = new List<string>();

        var result = RaceCornerResolver.Resolve(player, 1, new[] { 1, 2 },
            id => { events.Add($"limit:{id}"); return id == 1 ? 5 : 3; },
            id => { events.Add($"name:{id}"); return $"C{id}"; },
            overspeed => { events.Add($"heat:{overspeed}"); return overspeed; },
            (heat, reason) => { events.Add($"pay:{heat}:{reason}"); return true; },
            over => events.Add(over ? "sound:over" : "sound:safe"));

        Assert.That(result.completed, Is.True);
        Assert.That(result.log, Is.EqualTo(
            "Racer 安全通过 C1 (lane 2, 5<=5)。\n" +
            "Racer 在 C2 超速 (lane 2, 限速 3) 超 2！+2 热量。\n"));
        CollectionAssert.AreEqual(new[] {
            "limit:1", "sound:safe", "name:1", "limit:2", "heat:2", "name:2",
            "pay:2:overspeed at C2 (5>3)", "sound:over"
        }, events);
    }

    [Test]
    public void FailedHeatPaymentStopsBeforeLaterCornersOrAudio()
    {
        var player = new PlayerState("Racer", false, 0, 1) { cornerTotalThisTurn = 5 };
        var visited = new List<int>();
        int sounds = 0;

        var result = RaceCornerResolver.Resolve(player, 0, new[] { 1, 2, 3 },
            id => { visited.Add(id); return id == 1 ? 5 : 3; },
            id => $"C{id}", overspeed => overspeed,
            (_, __) => false, _ => sounds++);

        Assert.That(result.completed, Is.False);
        Assert.That(result.log, Is.EqualTo("Racer 安全通过 C1 (lane 1, 5<=5)。\n"));
        CollectionAssert.AreEqual(new[] { 1, 2 }, visited);
        Assert.That(sounds, Is.EqualTo(1));
    }

    [Test]
    public void ZeroHeatOverspeedSkipsPaymentButStillPlaysOverSound()
    {
        var player = new PlayerState("Racer", false, 0, 1) { cornerTotalThisTurn = 5 };
        bool overSound = false;
        var result = RaceCornerResolver.Resolve(player, 0, new[] { 1 },
            _ => 3, _ => "Apex", _ => 0,
            (_, __) => throw new System.Exception("Zero heat must not be paid"),
            over => overSound = over);

        Assert.That(result.completed, Is.True);
        Assert.That(result.log, Is.EqualTo(
            $"Racer 使用 {player.DriverProfile.ActiveName} 零热量通过 Apex。\n"));
        Assert.That(overSound, Is.True);
    }

    [Test]
    public void DriverIgnoreConsumesOneCornerBeforeQueryingItsLimit()
    {
        DriverCatalog.TryGet("jp_takumi_fujiwara", out DriverProfile driver);
        var player = new PlayerState("Racer", false, 0, 1)
        {
            teamId = TeamId.JP, driverId = "jp_takumi_fujiwara", cornerTotalThisTurn = 5
        };
        player.driverSkill.Initialize(driver, 7, true);
        Assert.That(player.driverSkill.TryActivate(driver,
            new DriverSkillActivationContext(true, 0, 3, 10, 10, 0), out _), Is.True);
        var queried = new List<int>();
        var result = RaceCornerResolver.Resolve(player, 0, new[] { 1, 2, 3 },
            id => { queried.Add(id); return 5; },
            id => $"C{id}", _ => 1, (_, __) => true, _ => { });

        Assert.That(result.completed, Is.True);
        Assert.That(result.log, Does.StartWith("Racer 使用 "));
        Assert.That(result.log, Does.Contain("无视 C1 限速。\n"));
        Assert.That(result.log, Does.Contain("无视 C2 限速。\n"));
        CollectionAssert.AreEqual(new[] { 3 }, queried);
    }

    [Test]
    public void EmptyCornersKeepOriginalFallbackWithoutCallbacks()
    {
        var player = new PlayerState("Racer", false, 0, 1) { cornerTotalThisTurn = 5 };
        var result = RaceCornerResolver.Resolve(player, 0, new int[0],
            _ => throw new System.Exception("Unexpected limit lookup"),
            _ => throw new System.Exception("Unexpected name lookup"),
            _ => throw new System.Exception("Unexpected heat lookup"),
            (_, __) => throw new System.Exception("Unexpected payment"),
            _ => throw new System.Exception("Unexpected sound"));

        Assert.That(result.completed, Is.True);
        Assert.That(result.log, Is.EqualTo("Racer 直道 - 无弯道。\n"));
    }
}
