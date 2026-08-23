using System;
using System.IO;
using NUnit.Framework;

public class RaceTestLogWriterTests
{
    [Test]
    public void WritesMetadataAndStripsHudRichText()
    {
        string directory = Path.Combine(Path.GetTempPath(), "foodula1-race-log-" + Guid.NewGuid().ToString("N"));
        try
        {
            var writer = new RaceTestLogWriter(directory);
            writer.BeginRace("silverstone", "Silverstone", "driver", TeamId.CN, 1);
            string path = writer.FilePath;
            writer.Append("<color=red>SPINS OUT!</color>");
            writer.End("finished");

            string contents = File.ReadAllText(path);
            Assert.That(contents, Does.Contain("track_id=silverstone"));
            Assert.That(contents, Does.Contain("SPINS OUT!"));
            Assert.That(contents, Does.Not.Contain("<color=red>"));
            Assert.That(contents, Does.Contain("[RACE_END] finished"));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public void RestartingRaceClosesPreviousFileAndUsesFreshPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "foodula1-race-log-" + Guid.NewGuid().ToString("N"));
        try
        {
            var writer = new RaceTestLogWriter(directory);
            writer.BeginRace("silverstone", "Silverstone", "driver", TeamId.CN, 1);
            string firstPath = writer.FilePath;
            writer.Append("first");

            writer.BeginRace("suzuka", "Suzuka", "driver", TeamId.JP, 1);
            string secondPath = writer.FilePath;
            writer.End("finished");

            Assert.That(firstPath, Is.Not.EqualTo(secondPath));
            Assert.That(File.ReadAllText(firstPath), Does.Contain("first"));
            Assert.That(File.ReadAllText(secondPath), Does.Contain("track_id=suzuka"));
            Assert.That(writer.IsActive, Is.False);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
