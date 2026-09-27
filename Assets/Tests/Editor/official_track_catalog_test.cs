using System.Collections.Generic;
using NUnit.Framework;

public class OfficialTrackCatalogTests
{
    [Test]
    public void CatalogHasStableUniqueIdsAndSelectionMetadata()
    {
        IReadOnlyList<TrackSelectionOption> tracks = OfficialTrackCatalog.Tracks;
        var ids = new HashSet<string>();

        for (int i = 0; i < tracks.Count; i++)
        {
            TrackSelectionOption track = tracks[i];
            Assert.That(track.TrackId, Is.Not.Null.And.Not.Empty);
            Assert.That(track.DisplayName, Is.Not.Null.And.Not.Empty);
            Assert.That(track.Subtitle, Is.Not.Null.And.Not.Empty);
            Assert.That(ids.Add(track.TrackId), Is.True, track.TrackId);
            Assert.That(OfficialTrackCatalog.Contains(track.TrackId), Is.True, track.TrackId);
        }

        Assert.That(tracks.Count, Is.EqualTo(CareerModeRules.RaceCount));
        Assert.That(tracks[0].TrackId, Is.EqualTo("silverstone_afternoon_tea"));
        Assert.That(tracks[tracks.Count - 1].TrackId, Is.EqualTo("le_mans_old_mulsanne"));
    }

    [Test]
    public void CatalogRejectsUnknownIdsWithoutCaseFolding()
    {
        Assert.That(OfficialTrackCatalog.Contains(null), Is.False);
        Assert.That(OfficialTrackCatalog.Contains(string.Empty), Is.False);
        Assert.That(OfficialTrackCatalog.Contains("unknown_track"), Is.False);
        Assert.That(OfficialTrackCatalog.Contains("SILVERSTONE_AFTERNOON_TEA"), Is.False);
    }

    [Test]
    public void TrackDefinitionsCannotBeMutatedThroughThePublicView()
    {
        Assert.Throws<System.NotSupportedException>(() =>
            ((IList<TrackSelectionOption>)OfficialTrackCatalog.Tracks)[0] = default);
    }
}
