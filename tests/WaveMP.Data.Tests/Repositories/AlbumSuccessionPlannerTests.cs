using AwesomeAssertions;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
public class AlbumSuccessionPlannerTests
{
    private static readonly DateTimeOffset _day = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid _firstUser = Id(101);
    private static readonly Guid _secondUser = Id(102);

    [Test]
    public void Plan_Merge_GivesTheUnionOfTheFavoritesAndTheValuesOfTheFirstPredecessor()
    {
        AlbumSuccessionAlbum one = Empty(1, _day.AddDays(2), onlineCover: Id(91));
        AlbumSuccessionAlbum two = Empty(2, _day.AddDays(1), CoverPriority.TrackFirst, Id(92));
        AlbumSuccessionAlbum three = Empty(3, _day.AddDays(4), CoverPriority.AlbumFirst);
        AlbumSuccessionAlbum merged = Live(10, _day.AddDays(5));

        AlbumSuccessionPlan plan = AlbumSuccessionPlanner.Plan(
            [one, two, three, merged],
            [Move(1, 10, 2), Move(2, 10, 1), Move(3, 10, 1)],
            [
                new AlbumSuccessionFavorite(_firstUser, one.Id, _day.AddDays(3)),
                new AlbumSuccessionFavorite(_firstUser, two.Id, _day.AddDays(1)),
                new AlbumSuccessionFavorite(_secondUser, three.Id, _day.AddDays(2)),
            ]);

        plan.MainSuccessors.Should().BeEquivalentTo(new Dictionary<Guid, Guid>
        {
            [one.Id] = merged.Id,
            [two.Id] = merged.Id,
            [three.Id] = merged.Id,
        });
        plan.Favorites.Should().BeEquivalentTo([
            new AlbumSuccessionFavorite(_firstUser, merged.Id, _day.AddDays(1)),
            new AlbumSuccessionFavorite(_secondUser, merged.Id, _day.AddDays(2)),
        ]);

        // Most songs first, then the earliest: album two comes before album three, both after album one.
        plan.Updates.Should().Equal(new AlbumSuccessionUpdate(merged.Id, _day.AddDays(1), CoverPriority.TrackFirst));
        plan.OnlineCovers.Should().Equal(new AlbumSuccessionOnlineCover(Id(91), merged.Id));
        plan.PassedOnMoves.Should().BeEmpty();
    }

    [Test]
    public void Plan_Split_GivesFavoritesRuleAndDateToEveryPartAndTheOnlineCoverToTheMainSuccessor()
    {
        AlbumSuccessionAlbum split = Empty(1, _day, CoverPriority.TrackFirst, Id(91));
        AlbumSuccessionAlbum small = Live(10, _day.AddDays(1));
        AlbumSuccessionAlbum large = Live(11, _day.AddDays(2));

        AlbumSuccessionPlan plan = AlbumSuccessionPlanner.Plan(
            [split, small, large],
            [Move(1, 10, 1), Move(1, 11, 3)],
            [new AlbumSuccessionFavorite(_firstUser, split.Id, _day.AddDays(4))]);

        plan.Successors[split.Id].Should().Equal(large.Id, small.Id);
        plan.Favorites.Should().BeEquivalentTo([
            new AlbumSuccessionFavorite(_firstUser, small.Id, _day.AddDays(4)),
            new AlbumSuccessionFavorite(_firstUser, large.Id, _day.AddDays(4)),
        ]);
        plan.Updates.Should().BeEquivalentTo([
            new AlbumSuccessionUpdate(small.Id, _day, CoverPriority.TrackFirst),
            new AlbumSuccessionUpdate(large.Id, _day, CoverPriority.TrackFirst),
        ]);
        plan.OnlineCovers.Should().Equal(new AlbumSuccessionOnlineCover(Id(91), large.Id));
    }

    [Test]
    public void Plan_SuccessorsWithTheSameSongs_AreOrderedByTheirFirstSeenDateAndThenByTheirId()
    {
        AlbumSuccessionAlbum split = Empty(1, _day);

        AlbumSuccessionPlan plan = AlbumSuccessionPlanner.Plan(
            [split, Live(12, _day.AddDays(2)), Live(11, _day.AddDays(1)), Live(10, _day.AddDays(2))],
            [Move(1, 10, 2), Move(1, 11, 2), Move(1, 12, 2)],
            []);

        plan.Successors[split.Id].Should().Equal(Id(11), Id(10), Id(12));
    }

    [Test]
    public void Plan_PredecessorsWithTheSameSongs_AreOrderedByTheirFirstSeenDateAndThenByTheirId()
    {
        AlbumSuccessionPlan plan = AlbumSuccessionPlanner.Plan(
            [
                Empty(3, _day.AddDays(1), CoverPriority.AlbumFirst, Id(93)),
                Empty(2, _day.AddDays(1), CoverPriority.TrackFirst, Id(92)),
                Empty(1, _day.AddDays(2), CoverPriority.AlbumFirst, Id(91)),
                Live(10, _day.AddDays(3)),
            ],
            [Move(1, 10, 1), Move(2, 10, 1), Move(3, 10, 1)],
            []);

        plan.Updates.Should().Equal(new AlbumSuccessionUpdate(Id(10), _day.AddDays(1), CoverPriority.TrackFirst));
        plan.OnlineCovers.Should().Equal(new AlbumSuccessionOnlineCover(Id(92), Id(10)));
    }

    [Test]
    public void Plan_SuccessorWithItsOwnValues_KeepsThemAndOnlyGetsTheFavoritesAndAnEarlierDate()
    {
        AlbumSuccessionAlbum old = Empty(1, _day.AddDays(1), CoverPriority.TrackFirst, Id(91));
        AlbumSuccessionAlbum existing = Live(10, _day.AddDays(2), CoverPriority.AlbumFirst, Id(90));

        AlbumSuccessionPlan plan = AlbumSuccessionPlanner.Plan(
            [old, existing],
            [Move(1, 10, 1)],
            [new AlbumSuccessionFavorite(_firstUser, old.Id, _day)]);

        plan.Favorites.Should().Equal(new AlbumSuccessionFavorite(_firstUser, existing.Id, _day));
        plan.Updates.Should().Equal(new AlbumSuccessionUpdate(existing.Id, _day.AddDays(1), CoverPriority.AlbumFirst));
        plan.OnlineCovers.Should().BeEmpty();
    }

    [Test]
    public void Plan_SuccessorThatIsAlreadyEarlierAndHasARule_IsNotUpdated()
    {
        AlbumSuccessionPlan plan = AlbumSuccessionPlanner.Plan(
            [Empty(1, _day.AddDays(1)), Live(10, _day, CoverPriority.AlbumFirst)],
            [Move(1, 10, 1)],
            []);

        plan.Successors.Should().ContainKey(Id(1));
        plan.Updates.Should().BeEmpty();
    }

    [Test]
    public void Plan_Chain_CarriesTheDataThroughAnAlbumLeftEmptyToTheLastOne()
    {
        AlbumSuccessionAlbum first = Empty(1, _day, CoverPriority.TrackFirst);
        AlbumSuccessionAlbum middle = Empty(2, _day.AddDays(1), onlineCover: Id(92));
        AlbumSuccessionAlbum last = Live(10, _day.AddDays(2));

        AlbumSuccessionPlan plan = AlbumSuccessionPlanner.Plan(
            [first, middle, last],
            [Move(1, 2, 2), Move(2, 10, 2)],
            [
                new AlbumSuccessionFavorite(_firstUser, first.Id, _day.AddDays(5)),
                new AlbumSuccessionFavorite(_secondUser, middle.Id, _day.AddDays(6)),
            ]);

        plan.MainSuccessors.Should().BeEquivalentTo(new Dictionary<Guid, Guid>
        {
            [first.Id] = last.Id,
            [middle.Id] = last.Id,
        });
        plan.Favorites.Should().BeEquivalentTo([
            new AlbumSuccessionFavorite(_firstUser, last.Id, _day.AddDays(5)),
            new AlbumSuccessionFavorite(_secondUser, last.Id, _day.AddDays(6)),
        ]);
        plan.Updates.Should().Equal(new AlbumSuccessionUpdate(last.Id, _day, CoverPriority.TrackFirst));
        plan.OnlineCovers.Should().Equal(new AlbumSuccessionOnlineCover(Id(92), last.Id));
    }

    [Test]
    public void Plan_WeightOfASuccessor_IsTheNumberOfSongsOfTheFirstMoveOnTheWay()
    {
        // Three songs went to album 2, which gave one of them to album 11 later; two songs went to album 10 directly.
        AlbumSuccessionPlan plan = AlbumSuccessionPlanner.Plan(
            [Empty(1, _day), Empty(2, _day), Live(10, _day), Live(11, _day)],
            [Move(1, 10, 2), Move(1, 2, 3), Move(2, 11, 1)],
            []);

        plan.Successors[Id(1)].Should().Equal(Id(11), Id(10));
    }

    [Test]
    public void Plan_SeveralWaysToTheSameSuccessor_UseTheHighestWeight()
    {
        AlbumSuccessionPlan plan = AlbumSuccessionPlanner.Plan(
            [Empty(1, _day), Empty(2, _day), Live(10, _day.AddDays(1)), Live(11, _day)],
            [Move(1, 2, 1), Move(1, 10, 3), Move(2, 10, 1), Move(1, 11, 2)],
            []);

        plan.Successors[Id(1)].Should().Equal(Id(10), Id(11));
    }

    [Test]
    public void Plan_AlbumLeftEmptyWithoutMoves_HasNoSuccessorAndItsDataGoes()
    {
        AlbumSuccessionPlan plan = AlbumSuccessionPlanner.Plan(
            [Empty(1, _day, CoverPriority.TrackFirst, Id(91)), Live(10, _day.AddDays(1))],
            [],
            [new AlbumSuccessionFavorite(_firstUser, Id(1), _day)]);

        plan.Successors.Should().BeEmpty();
        plan.Favorites.Should().BeEmpty();
        plan.Updates.Should().BeEmpty();
        plan.OnlineCovers.Should().BeEmpty();
    }

    [Test]
    public void Plan_MovesIntoAlbumsLeftEmptyOnly_FindNoSuccessorAndEndAlsoInACycle()
    {
        AlbumSuccessionPlan plan = AlbumSuccessionPlanner.Plan(
            [Empty(1, _day), Empty(2, _day), Empty(3, _day)],
            [Move(1, 2, 1), Move(2, 1, 1), Move(3, 1, 1)],
            [new AlbumSuccessionFavorite(_firstUser, Id(1), _day)]);

        plan.Successors.Should().BeEmpty();
        plan.Favorites.Should().BeEmpty();
    }

    [Test]
    public void Plan_MoveToTheSameAlbumOrToAnUnknownAlbum_IsIgnored()
    {
        AlbumSuccessionPlan plan = AlbumSuccessionPlanner.Plan(
            [Empty(1, _day), Live(10, _day)],
            [Move(1, 1, 5), Move(1, 99, 5), Move(1, 10, 1)],
            []);

        plan.Successors[Id(1)].Should().Equal(Id(10));
    }

    [Test]
    public void Plan_PartialMove_CarriesNothingWhileTheOldAlbumHasSongs()
    {
        AlbumSuccessionPlan plan = AlbumSuccessionPlanner.Plan(
            [Live(1, _day, CoverPriority.TrackFirst, Id(91)), Live(10, _day.AddDays(1))],
            [Move(1, 10, 1)],
            [new AlbumSuccessionFavorite(_firstUser, Id(1), _day)]);

        plan.Successors.Should().BeEmpty();
        plan.Favorites.Should().BeEmpty();
        plan.Updates.Should().BeEmpty();
        plan.OnlineCovers.Should().BeEmpty();
        plan.PassedOnMoves.Should().BeEmpty();
    }

    [Test]
    public void Plan_MovesIntoAnAlbumLeftEmpty_ArePassedOnToItsSuccessorsWithoutMovesToTheAlbumItself()
    {
        // Album 20 still has songs and gave two of them to album 1, which is now empty; album 21 gave one.
        AlbumSuccessionPlan plan = AlbumSuccessionPlanner.Plan(
            [Empty(1, _day), Live(10, _day), Live(20, _day), Live(21, _day)],
            [
                Move(20, 1, 2, _day.AddDays(1)),
                Move(21, 1, 1, _day.AddDays(2)),
                Move(1, 10, 2),
                Move(1, 20, 1),
            ],
            []);

        plan.Successors[Id(1)].Should().Equal(Id(10), Id(20));
        plan.PassedOnMoves.Should().Equal(
            new AlbumSuccessionMove(Id(20), Id(10), 2, _day.AddDays(1)),
            new AlbumSuccessionMove(Id(21), Id(10), 1, _day.AddDays(2)),
            new AlbumSuccessionMove(Id(21), Id(20), 1, _day.AddDays(2)));
    }

    [Test]
    public void Plan_MovesFromOneAlbumIntoSeveralAlbumsLeftEmpty_AreAddedUpPerSuccessor()
    {
        AlbumSuccessionPlan plan = AlbumSuccessionPlanner.Plan(
            [Empty(1, _day), Empty(2, _day), Live(10, _day), Live(20, _day)],
            [Move(20, 1, 2, _day), Move(20, 2, 3, _day.AddDays(1)), Move(1, 10, 1), Move(2, 10, 1)],
            []);

        plan.PassedOnMoves.Should().Equal(new AlbumSuccessionMove(Id(20), Id(10), 5, _day.AddDays(1)));
    }

    [Test]
    public void Plan_OnlineCover_GoesOnlyToTheAlbumWhoseMainSuccessorItIs()
    {
        // Album 11 is a successor of album 1, but album 10 got most of its songs; album 2 gave everything to 11.
        AlbumSuccessionPlan plan = AlbumSuccessionPlanner.Plan(
            [Empty(1, _day, onlineCover: Id(91)), Empty(2, _day.AddDays(1)), Live(10, _day), Live(11, _day)],
            [Move(1, 10, 3), Move(1, 11, 1), Move(2, 11, 1)],
            []);

        plan.OnlineCovers.Should().Equal(new AlbumSuccessionOnlineCover(Id(91), Id(10)));
    }

    private static AlbumSuccessionAlbum Empty(
        int id,
        DateTimeOffset firstSeenAt,
        CoverPriority? coverPriority = null,
        Guid? onlineCover = null)
    {
        return new AlbumSuccessionAlbum(Id(id), false, firstSeenAt, coverPriority, onlineCover);
    }

    private static AlbumSuccessionAlbum Live(
        int id,
        DateTimeOffset firstSeenAt,
        CoverPriority? coverPriority = null,
        Guid? onlineCover = null)
    {
        return new AlbumSuccessionAlbum(Id(id), true, firstSeenAt, coverPriority, onlineCover);
    }

    private static AlbumSuccessionMove Move(int from, int to, int trackCount, DateTimeOffset? movedAt = null)
    {
        return new AlbumSuccessionMove(Id(from), Id(to), trackCount, movedAt ?? _day);
    }

    /// <summary>
    /// A readable id that sorts by its number.
    /// </summary>
    private static Guid Id(int number)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{number:D12}");
    }
}
