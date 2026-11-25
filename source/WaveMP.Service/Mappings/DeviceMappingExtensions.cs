using WaveMP.Core.Devices.Devices;
using WaveMP.Core.Devices.Validations;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class DeviceMappingExtensions
{
    public static DeviceDto ToDto(this DeviceView device)
    {
        return new DeviceDto
        {
            Id = device.Id,
            Name = device.Name,
            ReportedName = device.ReportedName,
            CustomName = device.CustomName,
            Type = device.Type.ToDto(),
            Online = device.Online,
            RemoteControl = device.RemoteControl,
            Active = device.Active,
            LastActiveAt = device.LastActiveAt,
        };
    }

    public static ValidationErrorCodeDto ToDto(this DeviceValidationCode code)
    {
        return code switch
        {
            DeviceValidationCode.Required => ValidationErrorCodeDto.Required,
            DeviceValidationCode.MaxLength => ValidationErrorCodeDto.MaxLength,
            DeviceValidationCode.MinItems => ValidationErrorCodeDto.MinItems,
            DeviceValidationCode.MaxItems => ValidationErrorCodeDto.MaxItems,
            DeviceValidationCode.OutOfRange => ValidationErrorCodeDto.OutOfRange,
            DeviceValidationCode.InvalidFormat => ValidationErrorCodeDto.InvalidFormat,
            DeviceValidationCode.InvalidValue => ValidationErrorCodeDto.InvalidValue,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown validation code."),
        };
    }

    public static DeviceTypeDto ToDto(this DeviceType type)
    {
        return type switch
        {
            DeviceType.Desktop => DeviceTypeDto.Desktop,
            DeviceType.Phone => DeviceTypeDto.Phone,
            DeviceType.Tablet => DeviceTypeDto.Tablet,
            DeviceType.Tv => DeviceTypeDto.Tv,
            DeviceType.Watch => DeviceTypeDto.Watch,
            DeviceType.Other => DeviceTypeDto.Other,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown device type."),
        };
    }

    public static DeviceType ToEntity(this DeviceTypeDto type)
    {
        return type switch
        {
            DeviceTypeDto.Desktop => DeviceType.Desktop,
            DeviceTypeDto.Phone => DeviceType.Phone,
            DeviceTypeDto.Tablet => DeviceType.Tablet,
            DeviceTypeDto.Tv => DeviceType.Tv,
            DeviceTypeDto.Watch => DeviceType.Watch,
            DeviceTypeDto.Other => DeviceType.Other,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown device type."),
        };
    }
}
