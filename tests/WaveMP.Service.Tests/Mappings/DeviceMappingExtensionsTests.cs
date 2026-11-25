using AwesomeAssertions;
using WaveMP.Core.Devices.Devices;
using WaveMP.Core.Devices.Validations;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public class DeviceMappingExtensionsTests
{
    [Test]
    public void ToDto_Device_CarriesAllValues()
    {
        DeviceView device = new(
            "phone",
            "Kitchen",
            "Safari · iPhone",
            "Kitchen",
            DeviceType.Phone,
            true,
            true,
            false,
            DateTimeOffset.UnixEpoch);

        DeviceDto dto = device.ToDto();

        dto.Should().BeEquivalentTo(new
        {
            Id = "phone",
            Name = "Kitchen",
            ReportedName = "Safari · iPhone",
            CustomName = "Kitchen",
            Type = DeviceTypeDto.Phone,
            Online = true,
            RemoteControl = true,
            Active = false,
            LastActiveAt = DateTimeOffset.UnixEpoch,
        });
    }

    [TestCase(DeviceType.Desktop, DeviceTypeDto.Desktop)]
    [TestCase(DeviceType.Phone, DeviceTypeDto.Phone)]
    [TestCase(DeviceType.Tablet, DeviceTypeDto.Tablet)]
    [TestCase(DeviceType.Tv, DeviceTypeDto.Tv)]
    [TestCase(DeviceType.Watch, DeviceTypeDto.Watch)]
    [TestCase(DeviceType.Other, DeviceTypeDto.Other)]
    public void DeviceType_Always_MapsBothWays(DeviceType type, DeviceTypeDto dto)
    {
        type.ToDto().Should().Be(dto);
        dto.ToEntity().Should().Be(type);
    }

    [Test]
    public void DeviceType_EveryValue_HasADto()
    {
        Enum.GetValues<DeviceType>().Select(type => type.ToDto()).Should().OnlyHaveUniqueItems()
            .And.HaveCount(Enum.GetValues<DeviceTypeDto>().Length);
    }

    [TestCase(DeviceValidationCode.Required, ValidationErrorCodeDto.Required)]
    [TestCase(DeviceValidationCode.MaxLength, ValidationErrorCodeDto.MaxLength)]
    [TestCase(DeviceValidationCode.MinItems, ValidationErrorCodeDto.MinItems)]
    [TestCase(DeviceValidationCode.MaxItems, ValidationErrorCodeDto.MaxItems)]
    [TestCase(DeviceValidationCode.OutOfRange, ValidationErrorCodeDto.OutOfRange)]
    [TestCase(DeviceValidationCode.InvalidFormat, ValidationErrorCodeDto.InvalidFormat)]
    [TestCase(DeviceValidationCode.InvalidValue, ValidationErrorCodeDto.InvalidValue)]
    public void ValidationCode_Always_MapsToTheCodeOfTheApi(DeviceValidationCode code, ValidationErrorCodeDto dto)
    {
        code.ToDto().Should().Be(dto);
    }

    [Test]
    public void ValidationCode_EveryValue_IsMapped()
    {
        Enum.GetValues<DeviceValidationCode>().Select(code => code.ToDto()).Should().OnlyHaveUniqueItems();
    }
}
