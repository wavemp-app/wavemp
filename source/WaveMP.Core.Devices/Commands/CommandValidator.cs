using WaveMP.Core.Devices.Playbacks;
using WaveMP.Core.Devices.Validations;

namespace WaveMP.Core.Devices.Commands;

/// <summary>
/// Checks the form of a command before the server looks at the devices: required members of its type, ranges and
/// list sizes. Positions in the queue are checked against the live queue later.
/// </summary>
public static class CommandValidator
{
    /// <summary>
    /// The rejected fields of the command; empty if it is valid.
    /// </summary>
    public static IReadOnlyList<DeviceValidationError> Validate(DeviceCommand command)
    {
        List<DeviceValidationError> errors = [];
        if (DeviceRules.ValidateId(command.TargetDeviceId, "targetDeviceId") is { } target)
        {
            errors.Add(target);
        }

        if (!Enum.IsDefined(command.Type))
        {
            errors.Add(new DeviceValidationError("type", DeviceValidationCode.InvalidValue));
            return errors;
        }

        switch (command.Type)
        {
            case DeviceCommandType.Seek:
                ValidateMilliseconds(command.PositionMilliseconds, "positionMilliseconds", errors);
                break;
            case DeviceCommandType.SetVolume:
                if (command.Volume is not { } volume)
                {
                    errors.Add(new DeviceValidationError("volume", DeviceValidationCode.Required));
                }
                else if (!PlaybackValidator.IsVolume(volume))
                {
                    errors.Add(new DeviceValidationError("volume", DeviceValidationCode.OutOfRange, 0, 1));
                }

                break;
            case DeviceCommandType.SetShuffle or DeviceCommandType.SetCrossfade or DeviceCommandType.SetAutoplay:
                Require(command.Enabled is not null, "enabled", errors);
                break;
            case DeviceCommandType.SetRepeat:
                if (command.Repeat is not { } repeat)
                {
                    errors.Add(new DeviceValidationError("repeat", DeviceValidationCode.Required));
                }
                else if (!Enum.IsDefined(repeat))
                {
                    errors.Add(new DeviceValidationError("repeat", DeviceValidationCode.InvalidValue));
                }

                break;
            case DeviceCommandType.PlayTracks:
                ValidatePlayTracks(command, errors);
                break;
            case DeviceCommandType.AddToQueue or DeviceCommandType.PlayNext:
                ValidateTrackIds(command.TrackIds, errors);
                break;
            case DeviceCommandType.PlayQueueEntry or DeviceCommandType.RemoveQueueEntry:
                Require(command.ExpectedQueueVersion is not null, "expectedQueueVersion", errors);
                ValidateIndex(command.Index, "index", errors);
                break;
            case DeviceCommandType.MoveQueueEntry:
                Require(command.ExpectedQueueVersion is not null, "expectedQueueVersion", errors);
                ValidateIndex(command.Index, "index", errors);
                ValidateIndex(command.ToIndex, "toIndex", errors);
                break;
            case DeviceCommandType.ClearQueue:
                Require(command.ExpectedQueueVersion is not null, "expectedQueueVersion", errors);
                break;
            default:
                // Play, Pause, Stop, Next and Previous have no members.
                break;
        }

        return errors;
    }

    /// <summary>
    /// Whether commands of this type carry songs the server loads and passes on.
    /// </summary>
    public static bool UsesTracks(DeviceCommandType type)
    {
        return type is DeviceCommandType.PlayTracks or DeviceCommandType.AddToQueue or DeviceCommandType.PlayNext;
    }

    /// <summary>
    /// Whether commands of this type name entries of the queue by their position, so they need the queue version the
    /// remote saw.
    /// </summary>
    public static bool UsesQueueVersion(DeviceCommandType type)
    {
        return type is DeviceCommandType.PlayQueueEntry
            or DeviceCommandType.RemoveQueueEntry
            or DeviceCommandType.MoveQueueEntry
            or DeviceCommandType.ClearQueue;
    }

    private static void ValidatePlayTracks(DeviceCommand command, List<DeviceValidationError> errors)
    {
        int count = ValidateTrackIds(command.TrackIds, errors);
        if (command.StartIndex is { } start && count > 0 && (start < 0 || start >= count))
        {
            errors.Add(new DeviceValidationError("startIndex", DeviceValidationCode.OutOfRange, 0, count - 1));
        }

        if (command.Source is { } source && !Enum.IsDefined(source.Kind))
        {
            errors.Add(new DeviceValidationError("source.kind", DeviceValidationCode.InvalidValue));
        }
    }

    private static int ValidateTrackIds(IReadOnlyList<Guid>? trackIds, List<DeviceValidationError> errors)
    {
        if (trackIds is null)
        {
            errors.Add(new DeviceValidationError("trackIds", DeviceValidationCode.Required));
            return 0;
        }

        if (trackIds.Count == 0)
        {
            errors.Add(new DeviceValidationError("trackIds", DeviceValidationCode.MinItems, Min: 1));
            return 0;
        }

        if (trackIds.Count > DeviceLimits.MaxTrackIds)
        {
            errors.Add(new DeviceValidationError(
                "trackIds",
                DeviceValidationCode.MaxItems,
                Max: DeviceLimits.MaxTrackIds));
            return 0;
        }

        return trackIds.Count;
    }

    private static void ValidateMilliseconds(int? value, string field, List<DeviceValidationError> errors)
    {
        if (value is not { } milliseconds)
        {
            errors.Add(new DeviceValidationError(field, DeviceValidationCode.Required));
        }
        else if (!PlaybackValidator.IsMilliseconds(milliseconds))
        {
            errors.Add(
                new DeviceValidationError(field, DeviceValidationCode.OutOfRange, 0, DeviceLimits.MaxMilliseconds));
        }
    }

    private static void ValidateIndex(int? value, string field, List<DeviceValidationError> errors)
    {
        if (value is not { } index)
        {
            errors.Add(new DeviceValidationError(field, DeviceValidationCode.Required));
        }
        else if (index < 0)
        {
            errors.Add(new DeviceValidationError(field, DeviceValidationCode.OutOfRange, Min: 0));
        }
    }

    private static void Require(bool present, string field, List<DeviceValidationError> errors)
    {
        if (!present)
        {
            errors.Add(new DeviceValidationError(field, DeviceValidationCode.Required));
        }
    }
}
