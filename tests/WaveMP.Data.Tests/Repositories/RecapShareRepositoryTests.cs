using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class RecapShareRepositoryTests : LibraryDatabaseTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    protected override string DatabaseName => "wavemp_tests_recap_shares";

    [SetUp]
    public async Task ClearSharesAsync()
    {
        await CreateDbContext().RecapShares.ExecuteDeleteAsync();
    }

    [Test]
    public async Task SaveAsync_NewPeriod_CreatesTheShare()
    {
        User user = await AddUserAsync("Ada");
        RecapShare share = NewShare(user.Id, RecapPeriod.LastMonth, new DateOnly(2026, 10, 2), "aurora");

        RecapShareSaveResult result = await CreateRepository().SaveAsync(share);

        result.Created.Should().BeTrue();
        result.Row.OwnerName.Should().Be("Ada");
        result.Row.Share.Id.Should().Be(share.Id);
        RecapShareRow? found = await CreateRepository().FindAsync(share.Id);
        found.Should().NotBeNull();
        found!.Share.Should().BeEquivalentTo(share, options => options.Excluding(saved => saved.User));
        found.OwnerName.Should().Be("Ada");
    }

    [Test]
    public async Task SaveAsync_SamePeriodAgain_KeepsTheShareAndChangesItsDesign()
    {
        User user = await AddUserAsync("Ada");
        RecapShare first = NewShare(user.Id, RecapPeriod.AllTime, null, "aurora");
        await CreateRepository().SaveAsync(first);
        RecapShare again = NewShare(user.Id, RecapPeriod.AllTime, null, "prism");
        again.UpdatedAt = _start.AddHours(1);

        RecapShareSaveResult result = await CreateRepository().SaveAsync(again);

        result.Created.Should().BeFalse();
        result.Row.Share.Id.Should().Be(first.Id);
        result.Row.Share.Design.Should().Be("prism");
        result.Row.Share.UpdatedAt.Should().Be(_start.AddHours(1));
        result.Row.Share.CreatedAt.Should().Be(first.CreatedAt);
        await using WaveMpDbContext dbContext = CreateDbContext();
        (await dbContext.RecapShares.CountAsync(share => share.UserId == user.Id)).Should().Be(1);
    }

    [Test]
    public async Task SaveAsync_OtherDays_CreatesAnotherShare()
    {
        User user = await AddUserAsync("Ada");
        await CreateRepository().SaveAsync(NewShare(user.Id, RecapPeriod.Custom, new DateOnly(2026, 1, 1), "aurora"));

        RecapShareSaveResult result = await CreateRepository()
            .SaveAsync(NewShare(user.Id, RecapPeriod.Custom, new DateOnly(2026, 2, 1), "aurora"));

        result.Created.Should().BeTrue();
    }

    [Test]
    public async Task SaveAsync_UnknownUser_ThrowsUserNotFound()
    {
        var userId = Guid.NewGuid();

        Func<Task> save = () => CreateRepository().SaveAsync(NewShare(userId, RecapPeriod.LastYear, null, "aurora"));

        (await save.Should().ThrowAsync<UserNotFoundException>()).Which.UserId.Should().Be(userId);
    }

    [Test]
    public async Task ListAsync_Always_SeparatesOwnAndReceivedSharesNewestFirst()
    {
        User ada = await AddUserAsync("Ada");
        User bob = await AddUserAsync("Bob");
        User cleo = await AddUserAsync("Cleo");
        RecapShare own = NewShare(ada.Id, RecapPeriod.LastMonth, null, "aurora");
        RecapShare older = NewShare(bob.Id, RecapPeriod.LastMonth, null, "frost");
        RecapShare newer = NewShare(cleo.Id, RecapPeriod.LastYear, null, "prism");
        newer.UpdatedAt = _start.AddMinutes(5);
        IRecapShareRepository repository = CreateRepository();
        await repository.SaveAsync(own);
        await CreateRepository().SaveAsync(older);
        await CreateRepository().SaveAsync(newer);

        Page<RecapShareRow> received =
            await CreateRepository().ListAsync(new RecapShareListQuery(ada.Id, false, 0, 10));
        Page<RecapShareRow> mine = await CreateRepository().ListAsync(new RecapShareListQuery(ada.Id, true, 0, 10));

        received.Items.Select(row => (row.Share.Id, row.OwnerName)).Should().Equal(
            (newer.Id, "Cleo"),
            (older.Id, "Bob"));
        received.Total.Should().Be(2);
        mine.Items.Select(row => row.Share.Id).Should().Equal(own.Id);
    }

    [Test]
    public async Task DeleteAsync_Always_DeletesOnlySharesOfTheUser()
    {
        User ada = await AddUserAsync("Ada");
        User bob = await AddUserAsync("Bob");
        RecapShare share = NewShare(ada.Id, RecapPeriod.LastMonth, null, "aurora");
        await CreateRepository().SaveAsync(share);

        bool byOther = await CreateRepository().DeleteAsync(bob.Id, share.Id);
        bool byOwner = await CreateRepository().DeleteAsync(ada.Id, share.Id);

        byOther.Should().BeFalse();
        byOwner.Should().BeTrue();
        (await CreateRepository().FindAsync(share.Id)).Should().BeNull();
    }

    [Test]
    public async Task DeletingTheUser_Always_DeletesItsShares()
    {
        User ada = await AddUserAsync("Ada");
        RecapShare share = NewShare(ada.Id, RecapPeriod.LastMonth, null, "aurora");
        await CreateRepository().SaveAsync(share);

        await using (WaveMpDbContext dbContext = CreateDbContext())
        {
            await dbContext.Users.Where(user => user.Id == ada.Id).ExecuteDeleteAsync();
        }

        (await CreateRepository().FindAsync(share.Id)).Should().BeNull();
    }

    private IRecapShareRepository CreateRepository()
    {
        return new RecapShareRepository(CreateDbContext());
    }

    private static RecapShare NewShare(Guid userId, RecapPeriod period, DateOnly? from, string design)
    {
        return new RecapShare
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Period = period,
            FromDate = from,
            ToDate = new DateOnly(2026, 11, 1),
            TimeZone = "Europe/Berlin",
            Design = design,
            CreatedAt = _start,
            UpdatedAt = _start,
        };
    }

    private async Task<User> AddUserAsync(string prefix)
    {
        string name = prefix + " " + Guid.NewGuid().ToString("N")[..8];
        User user = new()
        {
            Id = Guid.CreateVersion7(),
            Name = prefix,
            NormalizedName = name.ToUpperInvariant(),
            Initials = prefix[..1],
            Color = "#336699",
            Role = UserRole.User,
            IsBuiltIn = false,
            CreatedAt = _start,
        };
        await using WaveMpDbContext dbContext = CreateDbContext();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }
}
