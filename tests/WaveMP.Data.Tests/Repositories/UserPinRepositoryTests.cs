using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Seeds;

namespace WaveMP.Data.Tests.Repositories;

/// <summary>
/// Counting of PIN attempts and the lockout in the database: the counting is one atomic statement, so these tests run
/// against PostgreSQL.
/// </summary>
[TestFixture]
[Category("Integration")]
public class UserPinRepositoryTests : LibraryDatabaseTests
{
    private const int MaxAttempts = 3;

    private static readonly DateTimeOffset _now = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _lockoutEnd = _now.AddMinutes(5);

    protected override string DatabaseName => "wavemp_tests_data_user_pins";

    [Test]
    public async Task BeginPinAttemptAsync_BelowTheLimit_CountsWithoutLockout()
    {
        User user = await AddUserAsync();

        PinAttempt? first = await BeginAsync(user.Id);
        PinAttempt? second = await BeginAsync(user.Id);

        first.Should().Be(new PinAttempt(true, 1, null));
        second.Should().Be(new PinAttempt(true, 2, null));
    }

    [Test]
    public async Task BeginPinAttemptAsync_AttemptThatReachesTheLimit_StartsTheLockout()
    {
        User user = await AddUserAsync();
        await BeginAsync(user.Id);
        await BeginAsync(user.Id);

        PinAttempt? last = await BeginAsync(user.Id);
        PinAttempt? locked = await BeginAsync(user.Id, _now.AddMinutes(1));

        last.Should().Be(new PinAttempt(true, MaxAttempts, _lockoutEnd));
        locked.Should().Be(new PinAttempt(false, MaxAttempts, _lockoutEnd));
    }

    [Test]
    public async Task BeginPinAttemptAsync_AfterTheLockout_StartsCountingAgain()
    {
        User user = await AddUserAsync();
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            await BeginAsync(user.Id);
        }

        PinAttempt? afterwards = await BeginAsync(user.Id, _lockoutEnd);

        afterwards.Should().Be(new PinAttempt(true, 1, null));
    }

    [Test]
    public async Task BeginPinAttemptAsync_ParallelAttempts_NeverGetPastTheLimit()
    {
        User user = await AddUserAsync();

        PinAttempt?[] attempts = await Task.WhenAll(
            Enumerable.Range(0, 12).Select(_ => BeginAsync(user.Id)));

        attempts.Count(attempt => attempt!.IsAllowed).Should().Be(MaxAttempts);
    }

    [Test]
    public async Task BeginPinAttemptAsync_UnknownUser_ReturnsNull()
    {
        (await BeginAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Test]
    public async Task ResetPinAttemptsAsync_Always_ForgetsTheAttemptsAndLiftsTheLockout()
    {
        User user = await AddUserAsync();
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            await BeginAsync(user.Id);
        }

        await CreateRepository().ResetPinAttemptsAsync(user.Id);

        (await BeginAsync(user.Id)).Should().Be(new PinAttempt(true, 1, null));
    }

    [Test]
    public async Task SetPinHashAsync_ExistingUser_SetsTheHashAndLiftsTheLockout()
    {
        User user = await AddUserAsync();
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            await BeginAsync(user.Id);
        }

        bool updated = await CreateRepository().SetPinHashAsync(user.Id, "hash");

        updated.Should().BeTrue();
        User stored = await LoadAsync(user.Id);
        stored.PinHash.Should().Be("hash");
        stored.PinFailedAttempts.Should().Be(0);
        stored.PinLockoutEnd.Should().BeNull();
    }

    [Test]
    public async Task SetPinHashAsync_Null_RemovesTheHash()
    {
        User user = await AddUserAsync();
        await CreateRepository().SetPinHashAsync(user.Id, "hash");

        await CreateRepository().SetPinHashAsync(user.Id, null);

        (await LoadAsync(user.Id)).PinHash.Should().BeNull();
    }

    [Test]
    public async Task SetPinHashAsync_UnknownUser_ReturnsFalse()
    {
        (await CreateRepository().SetPinHashAsync(Guid.NewGuid(), "hash")).Should().BeFalse();
    }

    [Test]
    public async Task DeleteForUserAsync_WithExceptedSession_KeepsOnlyItAndOtherUsers()
    {
        User user = await AddUserAsync();
        User other = await AddUserAsync();
        Session kept = await AddSessionAsync(user.Id);
        await AddSessionAsync(user.Id);
        await AddSessionAsync(user.Id);
        Session ofOther = await AddSessionAsync(other.Id);

        int deleted = await new SessionRepository(CreateDbContext()).DeleteForUserAsync(user.Id, kept.Id);

        deleted.Should().Be(2);
        WaveMpDbContext dbContext = CreateDbContext();
        (await dbContext.Sessions
                .Where(session => session.UserId == user.Id || session.UserId == other.Id)
                .Select(session => session.Id)
                .ToListAsync())
            .Should().BeEquivalentTo([kept.Id, ofOther.Id]);
    }

    [Test]
    public async Task DeleteForUserAsync_WithoutExceptedSession_DeletesAllSessionsOfTheUser()
    {
        User user = await AddUserAsync();
        await AddSessionAsync(user.Id);
        await AddSessionAsync(user.Id);

        int deleted = await new SessionRepository(CreateDbContext()).DeleteForUserAsync(user.Id);

        deleted.Should().Be(2);
    }

    [Test]
    public async Task FindBuiltInAdminAsync_WithoutBuiltInAdmin_ReturnsNull()
    {
        await AddUserAsync();

        (await CreateRepository().FindBuiltInAdminAsync()).Should().BeNull();
    }

    private Task<PinAttempt?> BeginAsync(Guid userId, DateTimeOffset? now = null)
    {
        return CreateRepository().BeginPinAttemptAsync(userId, now ?? _now, MaxAttempts, _lockoutEnd);
    }

    private UserRepository CreateRepository()
    {
        return new UserRepository(CreateDbContext());
    }

    private async Task<User> LoadAsync(Guid userId)
    {
        return await CreateDbContext().Users.AsNoTracking().SingleAsync(user => user.Id == userId);
    }

    private async Task<User> AddUserAsync()
    {
        LibrarySeed seed = new();
        User user = seed.User();
        await SaveAsync(seed);
        return user;
    }

    private async Task<Session> AddSessionAsync(Guid userId)
    {
        Session session = new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CreatedAt = _now,
            ExpiresAt = _now.AddDays(1),
        };
        await new SessionRepository(CreateDbContext()).AddAsync(session);
        return session;
    }
}
