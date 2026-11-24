using AwesomeAssertions;
using WaveMP.Core.Library.Groups;

namespace WaveMP.Core.Library.Tests.Groups;

[TestFixture]
public class GroupRunsTests
{
    [Test]
    public void Of_EmptyList_HasNoRuns()
    {
        IReadOnlyList<GroupRun<string>> runs = GroupRuns.Of(Array.Empty<string>(), row => row);

        runs.Should().BeEmpty();
    }

    [Test]
    public void Of_ConsecutiveEqualKeys_FormsOneRunEachWithPositionAndCount()
    {
        string[] rows = ["a1", "a2", "b1", "c1", "c2", "c3"];

        IReadOnlyList<GroupRun<string>> runs = GroupRuns.Of(rows, row => row[0]);

        runs.Should().Equal(
            new GroupRun<string>("a1", 0, 2),
            new GroupRun<string>("b1", 2, 1),
            new GroupRun<string>("c1", 3, 3));
    }

    [Test]
    public void Of_KeyThatComesBack_StartsARunOfItsOwn()
    {
        string[] rows = ["a1", "b1", "a2", "a3"];

        IReadOnlyList<GroupRun<string>> runs = GroupRuns.Of(rows, row => row[0]);

        runs.Should().Equal(
            new GroupRun<string>("a1", 0, 1),
            new GroupRun<string>("b1", 1, 1),
            new GroupRun<string>("a2", 2, 2));
    }

    [Test]
    public void Of_NullKeys_AreEqualToEachOther()
    {
        int?[] years = [null, null, 2001, null];

        IReadOnlyList<GroupRun<int?>> runs = GroupRuns.Of(years, year => year);

        runs.Select(run => run.Count).Should().Equal(2, 1, 1);
    }

    [Test]
    public void Of_LongList_CoversEveryEntryOnceInOrder()
    {
        Random random = new(45);
        int[] rows = [.. Enumerable.Range(0, 5000).Select(_ => random.Next(4))];

        IReadOnlyList<GroupRun<int>> runs = GroupRuns.Of(rows, row => row);

        runs.Sum(run => run.Count).Should().Be(rows.Length);
        runs[0].Position.Should().Be(0);
        runs.Zip(runs.Skip(1)).Should().OnlyContain(pair =>
            pair.First.Position + pair.First.Count == pair.Second.Position && pair.First.First != pair.Second.First);
        runs.Should().OnlyContain(run => rows.Skip(run.Position).Take(run.Count).All(row => row == run.First));
    }
}
