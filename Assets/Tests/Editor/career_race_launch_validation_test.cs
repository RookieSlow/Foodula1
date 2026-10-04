using NUnit.Framework;

public sealed class CareerRaceLaunchValidationTests
{
    private static readonly TeamId[] Field = { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US };

    [TestCase(CareerLoadStatus.Missing, "未找到当前生涯存档")]
    [TestCase(CareerLoadStatus.Invalid, "生涯存档校验失败")]
    public void MissingOrInvalidSaveRejectsBeforeRequestAndTrack(
        CareerLoadStatus status, string expectedReason)
    {
        CareerRaceLaunchRequest request = CreateRequest(out _);
        var loaded = new CareerLoadResult(status, null);

        Assert.That(CareerRaceLaunchValidation.TryValidate(
            request, loaded, "fallback_42", out string reason), Is.False);
        Assert.That(reason, Is.EqualTo(expectedReason));
    }

    [Test]
    public void ChangedSeasonRejectsBeforeWrongTrack()
    {
        CareerRaceLaunchRequest request = CreateRequest(out _);
        var changed = new CareerSeasonState();
        Assert.That(CareerModeRules.TryStartSeason(changed, TeamId.DE, Field), Is.True);

        Assert.That(CareerRaceLaunchValidation.TryValidate(
            request, new CareerLoadResult(CareerLoadStatus.Loaded, changed),
            "fallback_42", out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("启动请求与当前生涯存档不一致"));
    }

    [TestCase("fallback_42")]
    [TestCase("")]
    [TestCase(null)]
    public void WrongOrMissingLoadedTrackRejectsValidSeason(string loadedTrackId)
    {
        CareerRaceLaunchRequest request = CreateRequest(out CareerSeasonState season);

        Assert.That(CareerRaceLaunchValidation.TryValidate(
            request, new CareerLoadResult(CareerLoadStatus.Loaded, season),
            loadedTrackId, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("指定正式赛道加载失败，已阻止 fallback 赛道计入生涯"));
        Assert.That(request.Matches(season), Is.True);
    }

    [Test]
    public void MatchingSaveAndOfficialTrackAllowLaunchWithoutChangingSeason()
    {
        CareerRaceLaunchRequest request = CreateRequest(out CareerSeasonState season);
        int nextTrack = season.NextTrackIndex;

        Assert.That(CareerRaceLaunchValidation.TryValidate(
            request, new CareerLoadResult(CareerLoadStatus.Loaded, season),
            request.TrackId, out string reason), Is.True);
        Assert.That(reason, Is.Empty);
        Assert.That(season.NextTrackIndex, Is.EqualTo(nextTrack));
        Assert.That(request.Matches(season), Is.True);
    }

    private static CareerRaceLaunchRequest CreateRequest(out CareerSeasonState season)
    {
        season = new CareerSeasonState();
        Assert.That(CareerModeRules.TryStartSeason(season, TeamId.UK, Field), Is.True);
        Assert.That(CareerRaceLaunchRequest.TryCreate(season, "launch-validation", out var request), Is.True);
        return request;
    }
}
