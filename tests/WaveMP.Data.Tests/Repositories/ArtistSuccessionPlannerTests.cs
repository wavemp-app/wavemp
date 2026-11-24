using AwesomeAssertions;
using WaveMP.Data.Repositories;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
public class ArtistSuccessionPlannerTests
{
    private static readonly DateTimeOffset _day = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid _firstUser = Id(101);
    private static readonly Guid _secondUser = Id(102);

    [Test]
    public void Plan_Rename_GivesTheFavoritesAndTheEarlierDateToTheNewArtist()
    {
        ArtistSuccessionArtist old = Empty(1, _day);
        ArtistSuccessionArtist renamed = Live(10, _day.AddDays(5));

        ArtistSuccessionPlan plan = ArtistSuccessionPlanner.Plan(
            [old, renamed],
            [Move(1, 10, 2)],
            [
                new ArtistSuccessionFavorite(_firstUser, old.Id, _day.AddDays(1)),
                new ArtistSuccessionFavorite(_secondUser, old.Id, _day.AddDays(2)),
            ]);

        plan.MainSuccessors.Should().BeEquivalentTo(new Dictionary<Guid, Guid> { [old.Id] = renamed.Id });
        plan.Favorites.Should().Equal(
            new ArtistSuccessionFavorite(_firstUser, renamed.Id, _day.AddDays(1)),
            new ArtistSuccessionFavorite(_secondUser, renamed.Id, _day.AddDays(2)));
        plan.Updates.Should().Equal(new ArtistSuccessionUpdate(renamed.Id, _day));
        plan.PassedOnMoves.Should().BeEmpty();
    }

    [Test]
    public void Plan_Merge_GivesTheUnionOfTheFavoritesWithTheEarliestTimeAndTheEarliestDate()
    {
        ArtistSuccessionArtist one = Empty(1, _day.AddDays(2));
        ArtistSuccessionArtist two = Empty(2, _day.AddDays(1));
        ArtistSuccessionArtist merged = Live(10, _day.AddDays(5));

        ArtistSuccessionPlan plan = ArtistSuccessionPlanner.Plan(
            [one, two, merged],
            [Move(1, 10, 2), Move(2, 10, 1)],
            [
                new ArtistSuccessionFavorite(_firstUser, one.Id, _day.AddDays(3)),
                new ArtistSuccessionFavorite(_firstUser, two.Id, _day.AddDays(1)),
                new ArtistSuccessionFavorite(_secondUser, two.Id, _day.AddDays(2)),
            ]);

        plan.MainSuccessors.Should().BeEquivalentTo(new Dictionary<Guid, Guid>
        {
            [one.Id] = merged.Id,
            [two.Id] = merged.Id,
        });
        plan.Favorites.Should().BeEquivalentTo([
            new ArtistSuccessionFavorite(_firstUser, merged.Id, _day.AddDays(1)),
            new ArtistSuccessionFavorite(_secondUser, merged.Id, _day.AddDays(2)),
        ]);
        plan.Updates.Should().Equal(new ArtistSuccessionUpdate(merged.Id, _day.AddDays(1)));
    }

    [Test]
    public void Plan_Split_GivesFavoritesAndDateToEveryArtistAndTheMostSongsMakeTheMainSuccessor()
    {
        ArtistSuccessionArtist split = Empty(1, _day);
        ArtistSuccessionArtist small = Live(10, _day.AddDays(1));
        ArtistSuccessionArtist large = Live(11, _day.AddDays(2));

        ArtistSuccessionPlan plan = ArtistSuccessionPlanner.Plan(
            [split, small, large],
            [Move(1, 10, 1), Move(1, 11, 3)],
            [new ArtistSuccessionFavorite(_firstUser, split.Id, _day.AddDays(4))]);

        plan.Successors[split.Id].Should().Equal(large.Id, small.Id);
        plan.MainSuccessors[split.Id].Should().Be(large.Id);
        plan.Favorites.Should().BeEquivalentTo([
            new ArtistSuccessionFavorite(_firstUser, small.Id, _day.AddDays(4)),
            new ArtistSuccessionFavorite(_firstUser, large.Id, _day.AddDays(4)),
        ]);
        plan.Updates.Should().BeEquivalentTo([
            new ArtistSuccessionUpdate(small.Id, _day),
            new ArtistSuccessionUpdate(large.Id, _day),
        ]);
    }

    [Test]
    public void Plan_SuccessorsWithTheSameSongs_AreOrderedByTheirFirstSeenDateAndThenByTheirId()
    {
        ArtistSuccessionArtist split = Empty(1, _day);

        ArtistSuccessionPlan plan = ArtistSuccessionPlanner.Plan(
            [split, Live(12, _day.AddDays(2)), Live(11, _day.AddDays(1)), Live(10, _day.AddDays(2))],
            [Move(1, 10, 2), Move(1, 11, 2), Move(1, 12, 2)],
            []);

        plan.Successors[split.Id].Should().Equal(Id(11), Id(10), Id(12));
        plan.MainSuccessors[split.Id].Should().Be(Id(11));
    }

    [Test]
    public void Plan_SuccessorThatIsAlreadyEarlier_KeepsItsDateAndGetsTheFavorites()
    {
        ArtistSuccessionPlan plan = ArtistSuccessionPlanner.Plan(
            [Empty(1, _day.AddDays(1)), Live(10, _day)],
            [Move(1, 10, 1)],
            [new ArtistSuccessionFavorite(_firstUser, Id(1), _day.AddDays(3))]);

        plan.Favorites.Should().Equal(new ArtistSuccessionFavorite(_firstUser, Id(10), _day.AddDays(3)));
        plan.Updates.Should().BeEmpty();
    }

    [Test]
    public void Plan_Chain_CarriesTheDataThroughAnArtistLeftEmptyToTheLastOne()
    {
        ArtistSuccessionArtist first = Empty(1, _day);
        ArtistSuccessionArtist middle = Empty(2, _day.AddDays(1));
        ArtistSuccessionArtist last = Live(10, _day.AddDays(2));

        ArtistSuccessionPlan plan = ArtistSuccessionPlanner.Plan(
            [first, middle, last],
            [Move(1, 2, 2), Move(2, 10, 2)],
            [
                new ArtistSuccessionFavorite(_firstUser, first.Id, _day.AddDays(5)),
                new ArtistSuccessionFavorite(_secondUser, middle.Id, _day.AddDays(6)),
            ]);

        plan.MainSuccessors.Should().BeEquivalentTo(new Dictionary<Guid, Guid>
        {
            [first.Id] = last.Id,
            [middle.Id] = last.Id,
        });
        plan.Favorites.Should().BeEquivalentTo([
            new ArtistSuccessionFavorite(_firstUser, last.Id, _day.AddDays(5)),
            new ArtistSuccessionFavorite(_secondUser, last.Id, _day.AddDays(6)),
        ]);
        plan.Updates.Should().Equal(new ArtistSuccessionUpdate(last.Id, _day));
    }

    [Test]
    public void Plan_PartialMove_CarriesNothingWhileTheOldArtistHasSongs()
    {
        ArtistSuccessionPlan plan = ArtistSuccessionPlanner.Plan(
            [Live(1, _day), Live(10, _day.AddDays(1))],
            [Move(1, 10, 1)],
            [new ArtistSuccessionFavorite(_firstUser, Id(1), _day)]);

        plan.Successors.Should().BeEmpty();
        plan.Favorites.Should().BeEmpty();
        plan.Updates.Should().BeEmpty();
        plan.PassedOnMoves.Should().BeEmpty();
    }

    [Test]
    public void Plan_ArtistLeftEmptyWithoutMoves_HasNoSuccessorAndItsDataGoes()
    {
        ArtistSuccessionPlan plan = ArtistSuccessionPlanner.Plan(
            [Empty(1, _day), Live(10, _day.AddDays(1))],
            [Move(1, 1, 5), Move(1, 99, 5)],
            [new ArtistSuccessionFavorite(_firstUser, Id(1), _day)]);

        plan.Successors.Should().BeEmpty();
        plan.Favorites.Should().BeEmpty();
        plan.Updates.Should().BeEmpty();
    }

    [Test]
    public void Plan_MovesIntoAnArtistLeftEmpty_ArePassedOnToItsSuccessorsWithoutMovesToTheArtistItself()
    {
        // Artist 20 still has songs and gave two of them to artist 1, which is now empty; artist 21 gave one.
        ArtistSuccessionPlan plan = ArtistSuccessionPlanner.Plan(
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
            new ArtistSuccessionMove(Id(20), Id(10), 2, _day.AddDays(1)),
            new ArtistSuccessionMove(Id(21), Id(10), 1, _day.AddDays(2)),
            new ArtistSuccessionMove(Id(21), Id(20), 1, _day.AddDays(2)));
    }

    [Test]
    public void Plan_Rename_GivesBothPicturesAndTheMusicBrainzArtistToTheNewArtist()
    {
        ArtistSuccessionArtist old = Empty(1, _day) with
        {
            UploadedPictureId = Id(501),
            OnlinePictureId = Id(502),
            MusicBrainzArtistId = Id(901),
        };
        ArtistSuccessionArtist renamed = Live(10, _day);

        ArtistSuccessionPlan plan = ArtistSuccessionPlanner.Plan([old, renamed], [Move(1, 10, 2)], []);

        plan.Pictures.Should().Equal(
            new ArtistSuccessionPicture(Id(501), renamed.Id),
            new ArtistSuccessionPicture(Id(502), renamed.Id));
        plan.Updates.Should().Equal(new ArtistSuccessionUpdate(renamed.Id, _day, Id(901)));
    }

    [Test]
    public void Plan_Split_GivesThePicturesAndTheMusicBrainzArtistOnlyToTheMainSuccessor()
    {
        ArtistSuccessionArtist split = Empty(1, _day) with
        {
            UploadedPictureId = Id(501),
            OnlinePictureId = Id(502),
            MusicBrainzArtistId = Id(901),
        };
        ArtistSuccessionArtist small = Live(10, _day);
        ArtistSuccessionArtist large = Live(11, _day);

        ArtistSuccessionPlan plan = ArtistSuccessionPlanner.Plan(
            [split, small, large],
            [Move(1, 10, 1), Move(1, 11, 3)],
            []);

        plan.Pictures.Should().Equal(
            new ArtistSuccessionPicture(Id(501), large.Id),
            new ArtistSuccessionPicture(Id(502), large.Id));
        plan.Updates.Should().Equal(new ArtistSuccessionUpdate(large.Id, _day, Id(901)));
    }

    [Test]
    public void Plan_Merge_TakesEachValueFromTheFirstPredecessorThatHasIt()
    {
        // "Larger" gave the most songs and comes first; it has an uploaded picture and a MusicBrainz artist, but no
        // online picture, which comes from "Smaller".
        ArtistSuccessionArtist larger = Empty(1, _day) with
        {
            UploadedPictureId = Id(501),
            MusicBrainzArtistId = Id(901),
        };
        ArtistSuccessionArtist smaller = Empty(2, _day) with
        {
            UploadedPictureId = Id(503),
            OnlinePictureId = Id(504),
            MusicBrainzArtistId = Id(902),
        };
        ArtistSuccessionArtist merged = Live(10, _day);

        ArtistSuccessionPlan plan = ArtistSuccessionPlanner.Plan(
            [larger, smaller, merged],
            [Move(1, 10, 3), Move(2, 10, 1)],
            []);

        plan.Pictures.Should().Equal(
            new ArtistSuccessionPicture(Id(501), merged.Id),
            new ArtistSuccessionPicture(Id(504), merged.Id));
        plan.Updates.Should().Equal(new ArtistSuccessionUpdate(merged.Id, _day, Id(901)));
    }

    [Test]
    public void Plan_SuccessorWithItsOwnValues_KeepsThemAndOnlyGetsTheKindItLacks()
    {
        ArtistSuccessionArtist old = Empty(1, _day) with
        {
            UploadedPictureId = Id(501),
            OnlinePictureId = Id(502),
            MusicBrainzArtistId = Id(901),
        };
        ArtistSuccessionArtist existing = Live(10, _day) with
        {
            UploadedPictureId = Id(510),
            MusicBrainzArtistId = Id(910),
        };

        ArtistSuccessionPlan plan = ArtistSuccessionPlanner.Plan([old, existing], [Move(1, 10, 1)], []);

        plan.Pictures.Should().Equal(new ArtistSuccessionPicture(Id(502), existing.Id));
        plan.Updates.Should().BeEmpty();
    }

    [Test]
    public void Plan_ArtistThatIsOnlyASecondarySuccessor_GetsNoPictureEvenWithoutOne()
    {
        // "Main" got most songs of "Split" but has pictures of its own; "Other" got fewer and takes nothing over.
        ArtistSuccessionArtist split = Empty(1, _day) with
        {
            UploadedPictureId = Id(501),
            MusicBrainzArtistId = Id(901),
        };
        ArtistSuccessionArtist main = Live(10, _day) with
        {
            UploadedPictureId = Id(510),
            MusicBrainzArtistId = Id(910),
        };
        ArtistSuccessionArtist other = Live(11, _day);

        ArtistSuccessionPlan plan = ArtistSuccessionPlanner.Plan(
            [split, main, other],
            [Move(1, 10, 3), Move(1, 11, 1)],
            []);

        plan.MainSuccessors[split.Id].Should().Be(main.Id);
        plan.Pictures.Should().BeEmpty();
        plan.Updates.Should().BeEmpty();
    }

    [Test]
    public void Plan_Chain_CarriesThePicturesThroughAnArtistLeftEmpty()
    {
        ArtistSuccessionArtist first = Empty(1, _day) with { OnlinePictureId = Id(502) };
        ArtistSuccessionArtist middle = Empty(2, _day) with { UploadedPictureId = Id(501) };
        ArtistSuccessionArtist last = Live(10, _day);

        ArtistSuccessionPlan plan = ArtistSuccessionPlanner.Plan(
            [first, middle, last],
            [Move(1, 2, 2), Move(2, 10, 2)],
            []);

        plan.Pictures.Should().BeEquivalentTo([
            new ArtistSuccessionPicture(Id(501), last.Id),
            new ArtistSuccessionPicture(Id(502), last.Id),
        ]);
    }

    private static ArtistSuccessionArtist Empty(int id, DateTimeOffset firstSeenAt)
    {
        return new ArtistSuccessionArtist(Id(id), false, firstSeenAt);
    }

    private static ArtistSuccessionArtist Live(int id, DateTimeOffset firstSeenAt)
    {
        return new ArtistSuccessionArtist(Id(id), true, firstSeenAt);
    }

    private static ArtistSuccessionMove Move(int from, int to, int trackCount, DateTimeOffset? movedAt = null)
    {
        return new ArtistSuccessionMove(Id(from), Id(to), trackCount, movedAt ?? _day);
    }

    /// <summary>
    /// A readable id that sorts by its number.
    /// </summary>
    private static Guid Id(int number)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{number:D12}");
    }
}
