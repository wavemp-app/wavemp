using WaveMP.Core.Devices.Validations;
using WaveMP.Core.Streaming.PlaybackStates;

namespace WaveMP.Core.Devices.Playbacks;

/// <summary>
/// Checks the state a device publishes.
/// </summary>
public static class PlaybackValidator
{
    private const string Prefix = "playback.";

    /// <summary>
    /// The rejected fields of a publication; empty if it is valid. <paramref name="currentQueueLength"/> is the length
    /// of the live queue, which stays if the publication leaves its queue out. A claim must name its queue.
    /// </summary>
    public static IReadOnlyList<DeviceValidationError> Validate(PublishPlaybackCommand command, int currentQueueLength)
    {
        List<DeviceValidationError> errors = [];
        LivePlaybackInput? playback = command.Playback;
        if (playback is null)
        {
            errors.Add(new DeviceValidationError("playback", DeviceValidationCode.Required));
            return errors;
        }

        if (!Enum.IsDefined(playback.Status))
        {
            errors.Add(new DeviceValidationError(Prefix + "status", DeviceValidationCode.InvalidValue));
        }

        if (!Enum.IsDefined(playback.Repeat))
        {
            errors.Add(new DeviceValidationError(Prefix + "repeat", DeviceValidationCode.InvalidValue));
        }

        int queueLength = currentQueueLength;
        if (playback.Queue is { } queue)
        {
            queueLength = ValidateQueue(queue, errors);
        }
        else if (command.Claim)
        {
            errors.Add(new DeviceValidationError(Prefix + "queue", DeviceValidationCode.Required));
        }

        if (playback.CurrentIndex < -1 || playback.CurrentIndex >= queueLength)
        {
            errors.Add(new DeviceValidationError(
                Prefix + "currentIndex",
                DeviceValidationCode.OutOfRange,
                -1,
                queueLength - 1));
        }

        AddMilliseconds(playback.PositionMilliseconds, Prefix + "positionMilliseconds", errors);
        AddMilliseconds(playback.DurationMilliseconds, Prefix + "durationMilliseconds", errors);
        if (!IsVolume(playback.Volume))
        {
            errors.Add(new DeviceValidationError(Prefix + "volume", DeviceValidationCode.OutOfRange, 0, 1));
        }

        return errors;
    }

    /// <summary>
    /// Whether <paramref name="volume"/> is a volume from 0 to 1.
    /// </summary>
    public static bool IsVolume(double volume)
    {
        return double.IsFinite(volume) && volume is >= 0 and <= 1;
    }

    /// <summary>
    /// Whether <paramref name="milliseconds"/> is a position or duration from 0 to 24 hours.
    /// </summary>
    public static bool IsMilliseconds(int milliseconds)
    {
        return milliseconds is >= 0 and <= DeviceLimits.MaxMilliseconds;
    }

    private static int ValidateQueue(LiveQueueInput queue, List<DeviceValidationError> errors)
    {
        const string QueuePrefix = Prefix + "queue.";
        if (queue.Items is null)
        {
            errors.Add(new DeviceValidationError(QueuePrefix + "items", DeviceValidationCode.Required));
            return 0;
        }

        int count = queue.Items.Count;
        if (count > DeviceLimits.MaxQueueItems)
        {
            errors.Add(new DeviceValidationError(
                QueuePrefix + "items",
                DeviceValidationCode.MaxItems,
                Max: DeviceLimits.MaxQueueItems));
        }

        if (queue.Items.Any(item => item is null))
        {
            errors.Add(new DeviceValidationError(QueuePrefix + "items", DeviceValidationCode.Required));
        }

        if (queue.Shuffle
            && queue.OriginalOrder is { Count: > 0 } order
            && !PlaybackStateRules.IsPermutation(order, count))
        {
            errors.Add(new DeviceValidationError(QueuePrefix + "originalOrder", DeviceValidationCode.InvalidValue));
        }

        if (queue.Source is { } source && !Enum.IsDefined(source.Kind))
        {
            errors.Add(new DeviceValidationError(QueuePrefix + "source.kind", DeviceValidationCode.InvalidValue));
        }

        return count;
    }

    private static void AddMilliseconds(int value, string field, List<DeviceValidationError> errors)
    {
        if (!IsMilliseconds(value))
        {
            errors.Add(
                new DeviceValidationError(field, DeviceValidationCode.OutOfRange, 0, DeviceLimits.MaxMilliseconds));
        }
    }
}
