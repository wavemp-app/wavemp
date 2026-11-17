using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using WaveMP.Core.Users.Pins;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Users.Tests.Pins;

[TestFixture]
public class AdminPinInitializerTests
{
    private IUserRepository _users = null!;
    private ISessionRepository _sessions = null!;
    private IPinHasher _hasher = null!;
    private PinOptions _options = null!;
    private User _admin = null!;
    private AdminPinInitializer _initializer = null!;

    [SetUp]
    public void SetUp()
    {
        _users = Substitute.For<IUserRepository>();
        _sessions = Substitute.For<ISessionRepository>();
        _hasher = Substitute.For<IPinHasher>();
        _hasher.Hash(Arg.Any<string>()).Returns(call => $"hash-of-{call.Arg<string>()}");
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<string>(0) == $"hash-of-{call.ArgAt<string>(1)}");
        _admin = new User
        {
            Id = Guid.NewGuid(),
            Name = "Admin",
            NormalizedName = "ADMIN",
            Initials = "A",
            Color = "#1f6fd6",
            Role = UserRole.Admin,
            IsBuiltIn = true,
            CreatedAt = DateTimeOffset.UnixEpoch,
        };
        _users.FindBuiltInAdminAsync(Arg.Any<CancellationToken>()).Returns(_admin);
        _options = new PinOptions();
        _initializer = new AdminPinInitializer(
            _users,
            _sessions,
            _hasher,
            Options.Create(_options),
            NullLogger<AdminPinInitializer>.Instance);
    }

    [Test]
    public async Task ApplyAsync_WithoutAdminPin_ChangesNothing()
    {
        _admin.PinHash = "hash-of-1234";

        await _initializer.ApplyAsync();

        await _users.DidNotReceiveWithAnyArgs().SetPinHashAsync(default, default, default);
        await _sessions.DidNotReceiveWithAnyArgs().DeleteForUserAsync(default, default, default);
    }

    [Test]
    public async Task ApplyAsync_AdminWithoutPin_SetsTheHashAndEndsAllAdminSessions()
    {
        _options.AdminPin = "2468";

        await _initializer.ApplyAsync();

        await _users.Received(1).SetPinHashAsync(_admin.Id, "hash-of-2468", Arg.Any<CancellationToken>());
        await _sessions.Received(1).DeleteForUserAsync(_admin.Id, null, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyAsync_AdminWithAnotherPin_OverridesIt()
    {
        _options.AdminPin = "2468";
        _admin.PinHash = "hash-of-1111";

        await _initializer.ApplyAsync();

        await _users.Received(1).SetPinHashAsync(_admin.Id, "hash-of-2468", Arg.Any<CancellationToken>());
        await _sessions.Received(1).DeleteForUserAsync(_admin.Id, null, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyAsync_AdminWithTheSamePin_KeepsHashAndSessions()
    {
        _options.AdminPin = "2468";
        _admin.PinHash = "hash-of-2468";

        await _initializer.ApplyAsync();

        await _users.DidNotReceiveWithAnyArgs().SetPinHashAsync(default, default, default);
        await _sessions.DidNotReceiveWithAnyArgs().DeleteForUserAsync(default, default, default);
    }

    [Test]
    public async Task ApplyAsync_InvalidAdminPin_Throws()
    {
        _options.AdminPin = "12ab";

        Func<Task> apply = () => _initializer.ApplyAsync();

        await apply.Should().ThrowAsync<InvalidOperationException>();
        await _users.DidNotReceiveWithAnyArgs().SetPinHashAsync(default, default, default);
    }
}
