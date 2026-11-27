using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Seeds;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class SessionRepositoryTests : LibraryDatabaseTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    protected override string DatabaseName => "wavemp_tests_data_sessions";

    [Test]
    public async Task AddAsync_ExistingUser_AddsTheSession()
    {
        User user = await AddUserAsync();
        Session session = NewSession(user.Id);

        await CreateRepository().AddAsync(session);

        (await CreateRepository().FindActiveAsync(session.Id, _start))!.UserId.Should().Be(user.Id);
    }

    [Test]
    public async Task AddAsync_DeletedUser_ThrowsUserNotFoundAndAddsNothing()
    {
        User user = await AddUserAsync();
        await using (WaveMpDbContext dbContext = CreateDbContext())
        {
            await dbContext.Users.Where(candidate => candidate.Id == user.Id).ExecuteDeleteAsync();
        }

        Session session = NewSession(user.Id);
        Func<Task> add = () => CreateRepository().AddAsync(session);

        (await add.Should().ThrowAsync<UserNotFoundException>()).Which.UserId.Should().Be(user.Id);
        (await CreateRepository().FindActiveAsync(session.Id, _start)).Should().BeNull();
    }

    private SessionRepository CreateRepository()
    {
        return new SessionRepository(CreateDbContext());
    }

    private static Session NewSession(Guid userId)
    {
        return new Session
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CreatedAt = _start,
            ExpiresAt = _start.AddDays(1),
        };
    }

    private async Task<User> AddUserAsync()
    {
        LibrarySeed seed = new();
        User user = seed.User();
        await SaveAsync(seed);
        return user;
    }
}
