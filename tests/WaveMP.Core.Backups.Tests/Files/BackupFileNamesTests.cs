using AwesomeAssertions;
using WaveMP.Core.Backups.Files;

namespace WaveMP.Core.Backups.Tests.Files;

[TestFixture]
public class BackupFileNamesTests
{
    private static readonly DateTimeOffset _createdAt = new(2026, 11, 16, 3, 0, 5, TimeSpan.Zero);

    [TestCase(BackupTrigger.Schedule, "wavemp-20261116T030005Z-schedule")]
    [TestCase(BackupTrigger.Manual, "wavemp-20261116T030005Z-manual")]
    [TestCase(BackupTrigger.Migration, "wavemp-20261116T030005Z-migration")]
    public void CreateId_Always_NamesTheUtcTimeAndTheTrigger(BackupTrigger trigger, string expected)
    {
        BackupFileNames.CreateId(_createdAt, trigger).Should().Be(expected);
    }

    [Test]
    public void CreateId_WithLocalTime_UsesUtc()
    {
        DateTimeOffset local = new(2026, 11, 16, 4, 0, 5, TimeSpan.FromHours(1));

        BackupFileNames.CreateId(local, BackupTrigger.Manual).Should().Be("wavemp-20261116T030005Z-manual");
    }

    [Test]
    public void CreateId_WithSequence_AddsItFromTheSecondOn()
    {
        BackupFileNames.CreateId(_createdAt, BackupTrigger.Manual, 1).Should().Be("wavemp-20261116T030005Z-manual");
        BackupFileNames.CreateId(_createdAt, BackupTrigger.Manual, 2).Should().Be("wavemp-20261116T030005Z-manual-2");
    }

    [Test]
    public void TryParseId_WithCreatedId_ReturnsTimeTriggerAndSequence()
    {
        string id = BackupFileNames.CreateId(_createdAt, BackupTrigger.Migration, 12);

        bool parsed = BackupFileNames.TryParseId(
            id,
            out DateTimeOffset createdAt,
            out BackupTrigger trigger,
            out int sequence);

        parsed.Should().BeTrue();
        createdAt.Should().Be(_createdAt);
        createdAt.Offset.Should().Be(TimeSpan.Zero);
        trigger.Should().Be(BackupTrigger.Migration);
        sequence.Should().Be(12);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("wavemp-20261116T030005Z-schedule.dump")]
    [TestCase("../wavemp-20261116T030005Z-schedule")]
    [TestCase("wavemp-20261116T030005Z-schedule/../../etc")]
    [TestCase("wavemp-20261116T030005Z-daily")]
    [TestCase("wavemp-20261316T030005Z-schedule")]
    [TestCase("wavemp-20261116T030005Z-schedule-1")]
    [TestCase("wavemp-20261116T030005Z-schedule-02")]
    [TestCase("WAVEMP-20261116T030005Z-schedule")]
    [TestCase("wavemp-20261116T030005-schedule")]
    public void TryParseId_WithOtherText_ReturnsFalse(string? id)
    {
        BackupFileNames.TryParseId(id, out _, out _, out _).Should().BeFalse();
    }

    [Test]
    public void IdOfFileName_WithExport_ReturnsTheNameWithoutExtension()
    {
        BackupFileNames.IdOfFileName("wavemp-20261116T030005Z-manual-3.dump")
            .Should().Be("wavemp-20261116T030005Z-manual-3");
    }

    [TestCase("wavemp-20261116T030005Z-manual.dump.partial")]
    [TestCase("wavemp-20261116T030005Z-manual.sql")]
    [TestCase("notes.dump")]
    public void IdOfFileName_WithOtherFile_ReturnsNull(string fileName)
    {
        BackupFileNames.IdOfFileName(fileName).Should().BeNull();
    }
}
