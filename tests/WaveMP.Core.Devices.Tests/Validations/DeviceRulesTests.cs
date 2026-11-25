using AwesomeAssertions;
using WaveMP.Core.Devices.Validations;

namespace WaveMP.Core.Devices.Tests.Validations;

[TestFixture]
public class DeviceRulesTests
{
    [TestCase("3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    [TestCase("a")]
    [TestCase("lmfj2x0k-4h7tq0a1b")]
    [TestCase("device.name_1:2")]
    public void IsValidId_AllowedCharacters_IsTrue(string deviceId)
    {
        DeviceRules.IsValidId(deviceId).Should().BeTrue();
        DeviceRules.ValidateId(deviceId, "deviceId").Should().BeNull();
    }

    [TestCase("with space")]
    [TestCase("slash/")]
    [TestCase("line\n")]
    [TestCase("umlaut-ä")]
    public void IsValidId_OtherCharacters_IsFalse(string deviceId)
    {
        DeviceRules.IsValidId(deviceId).Should().BeFalse();
        DeviceRules.ValidateId(deviceId, "deviceId").Should().Be(
            new DeviceValidationError("deviceId", DeviceValidationCode.InvalidFormat));
    }

    [Test]
    public void IsValidId_LongerThan64Characters_IsFalse()
    {
        DeviceRules.IsValidId(new string('a', 64)).Should().BeTrue();
        DeviceRules.IsValidId(new string('a', 65)).Should().BeFalse();
    }

    [TestCase(null)]
    [TestCase("")]
    public void ValidateId_Missing_IsRequired(string? deviceId)
    {
        DeviceRules.ValidateId(deviceId, "targetDeviceId").Should().Be(
            new DeviceValidationError("targetDeviceId", DeviceValidationCode.Required));
    }

    [TestCase("Firefox · Windows")]
    [TestCase("  Living room  ")]
    [TestCase("Lars's iPhone")]
    public void ValidateName_ValidName_IsNull(string name)
    {
        DeviceRules.ValidateName(name, "name").Should().BeNull();
    }

    [TestCase(null, DeviceValidationCode.Required)]
    [TestCase("   ", DeviceValidationCode.Required)]
    [TestCase("Tab\tName", DeviceValidationCode.InvalidFormat)]
    [TestCase("Line\nBreak", DeviceValidationCode.InvalidFormat)]
    public void ValidateName_InvalidName_ReturnsTheCode(string? name, DeviceValidationCode code)
    {
        DeviceRules.ValidateName(name, "name")!.Code.Should().Be(code);
    }

    [Test]
    public void ValidateName_LongerThan100CharactersAfterTrimming_IsTooLong()
    {
        DeviceRules.ValidateName(" " + new string('x', 100) + " ", "name").Should().BeNull();
        DeviceRules.ValidateName(new string('x', 101), "name").Should().Be(
            new DeviceValidationError("name", DeviceValidationCode.MaxLength, Max: 100));
    }

    [Test]
    public void NormalizeName_Always_RemovesTheSpacesAtTheEnds()
    {
        DeviceRules.NormalizeName("  Kitchen radio ").Should().Be("Kitchen radio");
    }
}
