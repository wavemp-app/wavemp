namespace WaveMP.Core.Streaming.PlaybackStates;

/// <summary>
/// The rules of a saved queue: what a device may save, and how the queue follows when tracks left the library.
/// </summary>
public static class PlaybackStateRules
{
    /// <summary>
    /// Checks the positions of a state: the current index lies in the queue (or is -1), and a non-empty order before
    /// shuffling of a shuffled queue names every position of the queue exactly once. Returns <c>null</c> if the state
    /// is valid.
    /// </summary>
    public static PlaybackStateSaveStatus? Validate(SavePlaybackStateCommand command)
    {
        int count = command.Items.Count;
        if (command.CurrentIndex < -1 || command.CurrentIndex >= count)
        {
            return PlaybackStateSaveStatus.InvalidCurrentIndex;
        }

        return command.Shuffle && command.OriginalOrder.Count > 0 && !IsPermutation(command.OriginalOrder, count)
            ? PlaybackStateSaveStatus.InvalidOriginalOrder
            : null;
    }

    /// <summary>
    /// The queue of <paramref name="count"/> entries without those whose track is gone (<paramref name="exists"/>
    /// returns false for their position). The current entry stays current; if it is gone, the next remaining entry
    /// becomes current (the last one if none follows). Autoplay marks and the order before shuffling follow the
    /// remaining entries.
    /// </summary>
    public static RemainingQueue Remaining(
        int count,
        int currentIndex,
        IReadOnlyList<int> autoplayPositions,
        IReadOnlyList<int> originalOrder,
        Func<int, bool> exists)
    {
        List<int> kept = [];
        int[] newIndex = new int[count];
        for (int position = 0; position < count; position++)
        {
            newIndex[position] = exists(position) ? kept.Count : -1;
            if (newIndex[position] >= 0)
            {
                kept.Add(position);
            }
        }

        int current = -1;
        bool currentKept = false;
        if (currentIndex >= 0 && currentIndex < count && kept.Count > 0)
        {
            currentKept = newIndex[currentIndex] >= 0;
            int following = kept.FindIndex(position => position >= currentIndex);
            current = following >= 0 ? following : kept.Count - 1;
        }

        return new RemainingQueue(
            kept,
            current,
            currentKept,
            [.. autoplayPositions.Where(position => IsKept(position, newIndex)).Select(position => newIndex[position])],
            [.. originalOrder.Where(position => IsKept(position, newIndex)).Select(position => newIndex[position])]);
    }

    private static bool IsKept(int position, int[] newIndex)
    {
        return position >= 0 && position < newIndex.Length && newIndex[position] >= 0;
    }

    /// <summary>
    /// Whether <paramref name="order"/> names every position of a queue of <paramref name="count"/> entries exactly
    /// once, as the order before shuffling of a shuffled queue does.
    /// </summary>
    public static bool IsPermutation(IReadOnlyList<int> order, int count)
    {
        if (order.Count != count)
        {
            return false;
        }

        bool[] seen = new bool[count];
        foreach (int position in order)
        {
            if (position < 0 || position >= count || seen[position])
            {
                return false;
            }

            seen[position] = true;
        }

        return true;
    }
}
