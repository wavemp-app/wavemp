namespace WaveMP.Core.Library.Groups;

/// <summary>
/// Splits a list into its groups: maximal runs of consecutive entries with the same key. A key that comes back later
/// in the list starts a group of its own, so every entry belongs to exactly one group and the groups follow the order
/// of the list.
/// </summary>
public static class GroupRuns
{
    public static IReadOnlyList<GroupRun<TRow>> Of<TRow, TKey>(IReadOnlyList<TRow> rows, Func<TRow, TKey> keyOf)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(keyOf);

        List<GroupRun<TRow>> runs = [];
        EqualityComparer<TKey> comparer = EqualityComparer<TKey>.Default;
        int start = 0;
        TKey? current = default;
        for (int index = 0; index < rows.Count; index++)
        {
            TKey key = keyOf(rows[index]);
            if (index == 0)
            {
                current = key;
                continue;
            }

            if (!comparer.Equals(current, key))
            {
                runs.Add(new GroupRun<TRow>(rows[start], start, index - start));
                start = index;
                current = key;
            }
        }

        if (rows.Count > 0)
        {
            runs.Add(new GroupRun<TRow>(rows[start], start, rows.Count - start));
        }

        return runs;
    }
}
