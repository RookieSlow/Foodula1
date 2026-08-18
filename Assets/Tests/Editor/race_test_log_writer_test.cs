using System;
using System.IO;
using NUnit.Framework;

public class RaceTestLogWriterTests
{
    [Test]
    public void WritesMetadataAndStripsHudRichText()
    {
        string directory = Path.Combine(Path.GetTempPath(), "foodular1-race-log-" + Guid.NewGuid().ToString("N"));
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
}
