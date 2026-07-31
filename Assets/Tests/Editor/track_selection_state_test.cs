using System.Collections.Generic;
using NUnit.Framework;

public class TrackSelectionStateTests
{
    private const string SilverstoneId = "silverstone_afternoon_tea";
    private const string MonzaId = "monza_pasta";

    [SetUp]
    public void SetUp()
    {
        TrackSelectionState.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        TrackSelectionState.Reset();
    }

    [Test]
    public void AvailableTracks_ContainsEightUniqueTrackIds()
    {
        IReadOnlyList<TrackSelectionOption> tracks = TrackSelectionState.AvailableTracks;
        var ids = new HashSet<string>();

        foreach (TrackSelectionOption track in tracks)
        {
            ids.Add(track.TrackId);
        }

        Assert.That(tracks.Count, Is.EqualTo(8));
        Assert.That(ids.Count, Is.EqualTo(tracks.Count));
    }

    [Test]
    public void AvailableTracks_NordschleifeUsesUpdatedPlayerFacingName()
    {
        TrackSelectionOption nordschleife = default;
        foreach (TrackSelectionOption track in TrackSelectionState.AvailableTracks)
        {
            if (track.TrackId == "nurburgring_24h_endurance")
            {
                nordschleife = track;
                break;
            }
        }

        Assert.That(nordschleife.DisplayName, Is.EqualTo("纽博格林北环"));
        Assert.That(nordschleife.Subtitle, Is.EqualTo("德国 · 绿色地狱"));
    }

    [Test]
    public void TrySelect_KnownTrack_UpdatesResolvedTrack()
    {
        bool selected = TrackSelectionState.TrySelect(MonzaId);

        Assert.That(selected, Is.True);
        Assert.That(TrackSelectionState.SelectedTrackId, Is.EqualTo(MonzaId));
        Assert.That(TrackSelectionState.ResolveTrackId(SilverstoneId), Is.EqualTo(MonzaId));
    }

    [Test]
    public void TrySelect_UnknownTrack_PreservesEmptySelection()
    {
        bool selected = TrackSelectionState.TrySelect("unknown_track");

        Assert.That(selected, Is.False);
        Assert.That(TrackSelectionState.SelectedTrackId, Is.Empty);
    }

    [Test]
    public void ResolveTrackId_NoSelection_UsesKnownConfiguredFallback()
    {
        string resolved = TrackSelectionState.ResolveTrackId(MonzaId);

        Assert.That(resolved, Is.EqualTo(MonzaId));
    }

    [Test]
    public void ResolveTrackId_UnknownFallback_UsesCatalogDefault()
    {
        string resolved = TrackSelectionState.ResolveTrackId("unknown_track");

        Assert.That(resolved, Is.EqualTo(SilverstoneId));
    }
}
