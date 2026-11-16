using AwesomeAssertions;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Validations;

namespace WaveMP.Core.Settings.Tests.Validations;

[TestFixture]
public class BackupSettingsValidatorTests
{
    [Test]
    public void Validate_WithDefaults_ReturnsNoErrors()
    {
        BackupSettingsValidator.Validate(new BackupSettings()).Should().BeEmpty();
    }

    [TestCase(1)]
    [TestCase(100)]
    public void Validate_WithRetentionAtTheLimits_ReturnsNoErrors(int count)
    {
        BackupSettingsValidator.Validate(new BackupSettings { RetentionCount = count }).Should().BeEmpty();
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(101)]
    public void Validate_WithRetentionOutOfRange_ReturnsOutOfRangeWithTheLimits(int count)
    {
        IReadOnlyList<BackupSettingsValidationError> errors =
            BackupSettingsValidator.Validate(new BackupSettings { RetentionCount = count });

        errors.Should().Equal(new BackupSettingsValidationError(
            BackupSettingsField.RetentionCount,
            SettingsErrorReason.OutOfRange,
            1,
            100));
    }

    [Test]
    public void Validate_WithAWholeMinute_ReturnsNoErrors()
    {
        BackupSettingsValidator.Validate(new BackupSettings { ScheduleTime = new TimeOnly(23, 59) })
            .Should().BeEmpty();
    }

    [Test]
    public void Validate_WithSeconds_ReturnsInvalidScheduleTime()
    {
        IReadOnlyList<BackupSettingsValidationError> errors =
            BackupSettingsValidator.Validate(new BackupSettings { ScheduleTime = new TimeOnly(3, 0, 1) });

        errors.Should().Equal(
            new BackupSettingsValidationError(BackupSettingsField.ScheduleTime, SettingsErrorReason.Invalid));
    }
}
