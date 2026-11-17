using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using WaveMP.Core.Users.Pins;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Users.Tests.Pins;

[TestFixture]
public class PinServiceTests
{
    private const string StoredHash = "hash-of-1234";

    private static readonly DateTimeOffset _now = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    private IUserRepository _users = null!;
    private ISessionRepository _sessions = null!;
    private IPinHasher _hasher = null!;
    private PinOptions _options = null!;
    private PinService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _users = Substitute.For<IUserRepository>();
        _sessions = Substitute.For<ISessionRepository>();
        _hasher = Substitute.For<IPinHasher>();
        _hasher.Verify(StoredHash, "1234").Returns(true);
        _hasher.Hash(Arg.Any<string>()).Returns(call => $"hash-of-{call.Arg<string>()}");
        _users.SetPinHashAsync(Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(true);
        TimeProvider timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(_now);
        _options = new PinOptions { MaxFailedAttempts = 5, LockoutDuration = TimeSpan.FromMinutes(5) };
        _service = new PinService(_users, _sessions, _hasher, timeProvider, Options.Create(_options));
    }

    [Test]
    public void PinOptions_ByDefault_LockOutForFiveMinutesAfterFiveWrongPins()
    {
        PinOptions options = new();

        options.MaxFailedAttempts.Should().Be(5);
        options.LockoutDuration.Should().Be(TimeSpan.FromMinutes(5));
        options.HasAdminPin.Should().BeFalse();
    }

    [Test]
    public async Task CheckAsync_ProfileWithoutPin_PassesWithoutCounting()
    {
        User user = CreateUser(pinHash: null);

        PinCheck result = await _service.CheckAsync(user, null);

        result.Should().Be(new PinCheck(PinCheckStatus.Passed));
        await _users.DidNotReceiveWithAnyArgs().BeginPinAttemptAsync(default, default, default, default, default);
    }

    [TestCase(null)]
    [TestCase("")]
    public async Task CheckAsync_ProtectedProfileWithoutPin_IsRequiredWithoutCounting(string? pin)
    {
        User user = CreateUser();

        PinCheck result = await _service.CheckAsync(user, pin);

        result.Should().Be(new PinCheck(PinCheckStatus.Required));
        await _users.DidNotReceiveWithAnyArgs().BeginPinAttemptAsync(default, default, default, default, default);
    }

    [Test]
    public async Task CheckAsync_CorrectPin_CountsTheAttemptFirstAndThenResetsIt()
    {
        User user = CreateUser();
        AllowAttempt(user, failedAttempts: 3);

        PinCheck result = await _service.CheckAsync(user, "1234");

        result.Should().Be(new PinCheck(PinCheckStatus.Passed));
        Received.InOrder(() =>
        {
            _users.BeginPinAttemptAsync(user.Id, _now, 5, _now.AddMinutes(5), Arg.Any<CancellationToken>());
            _users.ResetPinAttemptsAsync(user.Id, Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task CheckAsync_WrongPinBelowTheLimit_IsInvalid()
    {
        User user = CreateUser();
        AllowAttempt(user, failedAttempts: 4);

        PinCheck result = await _service.CheckAsync(user, "9999");

        result.Should().Be(new PinCheck(PinCheckStatus.Invalid));
        await _users.DidNotReceive().ResetPinAttemptsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CheckAsync_WrongPinThatReachesTheLimit_IsLockedOut()
    {
        User user = CreateUser();
        DateTimeOffset lockoutEnd = _now.AddMinutes(5);
        AllowAttempt(user, failedAttempts: 5, lockoutEnd);

        PinCheck result = await _service.CheckAsync(user, "9999");

        result.Should().Be(new PinCheck(PinCheckStatus.LockedOut, lockoutEnd));
    }

    [Test]
    public async Task CheckAsync_CorrectPinThatReachesTheLimit_PassesAndLiftsTheLockout()
    {
        User user = CreateUser();
        AllowAttempt(user, failedAttempts: 5, _now.AddMinutes(5));

        PinCheck result = await _service.CheckAsync(user, "1234");

        result.Should().Be(new PinCheck(PinCheckStatus.Passed));
        await _users.Received(1).ResetPinAttemptsAsync(user.Id, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CheckAsync_WhileLockedOut_RejectsEvenTheCorrectPinWithoutCheckingIt()
    {
        User user = CreateUser();
        DateTimeOffset lockoutEnd = _now.AddMinutes(3);
        _users.BeginPinAttemptAsync(user.Id, _now, 5, _now.AddMinutes(5), Arg.Any<CancellationToken>())
            .Returns(new PinAttempt(false, 5, lockoutEnd));

        PinCheck result = await _service.CheckAsync(user, "1234");

        result.Should().Be(new PinCheck(PinCheckStatus.LockedOut, lockoutEnd));
        _hasher.DidNotReceiveWithAnyArgs().Verify(default!, default!);
        await _users.DidNotReceive().ResetPinAttemptsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CheckAsync_ProfileDeletedInTheMeantime_IsNotFound()
    {
        User user = CreateUser();

        PinCheck result = await _service.CheckAsync(user, "1234");

        result.Should().Be(new PinCheck(PinCheckStatus.NotFound));
    }

    [TestCase("12a4")]
    [TestCase("123456789")]
    public async Task CheckAsync_MalformedPin_IsInvalidAndCounted(string pin)
    {
        User user = CreateUser();
        AllowAttempt(user, failedAttempts: 1);

        PinCheck result = await _service.CheckAsync(user, pin);

        result.Should().Be(new PinCheck(PinCheckStatus.Invalid));
        _hasher.DidNotReceiveWithAnyArgs().Verify(default!, default!);
    }

    [Test]
    public async Task CheckAsync_WithOwnLimits_UsesThem()
    {
        _options.MaxFailedAttempts = 3;
        _options.LockoutDuration = TimeSpan.FromMinutes(15);
        User user = CreateUser();

        await _service.CheckAsync(user, "9999");

        await _users.Received(1).BeginPinAttemptAsync(
            user.Id,
            _now,
            3,
            _now.AddMinutes(15),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetAsync_ValidPin_StoresOnlyTheHashAndEndsTheOtherSessions()
    {
        User user = CreateUser(pinHash: null);
        _users.FindAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var currentSession = Guid.NewGuid();

        PinChangeStatus result = await _service.SetAsync(user.Id, "482913", currentSession);

        result.Should().Be(PinChangeStatus.Success);
        await _users.Received(1).SetPinHashAsync(user.Id, "hash-of-482913", Arg.Any<CancellationToken>());
        await _sessions.Received(1).DeleteForUserAsync(user.Id, currentSession, Arg.Any<CancellationToken>());
    }

    [TestCase("123")]
    [TestCase("123456789")]
    [TestCase("abcd")]
    public async Task SetAsync_InvalidPin_ChangesNothing(string pin)
    {
        User user = CreateUser(pinHash: null);
        _users.FindAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        PinChangeStatus result = await _service.SetAsync(user.Id, pin);

        result.Should().Be(PinChangeStatus.InvalidPin);
        await _users.DidNotReceiveWithAnyArgs().SetPinHashAsync(default, default, default);
    }

    [Test]
    public async Task SetAsync_UnknownProfile_IsNotFound()
    {
        PinChangeStatus result = await _service.SetAsync(Guid.NewGuid(), "1234");

        result.Should().Be(PinChangeStatus.NotFound);
        await _sessions.DidNotReceiveWithAnyArgs().DeleteForUserAsync(default, default, default);
    }

    [Test]
    public async Task SetAsync_ProfileDeletedWhileSaving_IsNotFound()
    {
        User user = CreateUser();
        _users.FindAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _users.SetPinHashAsync(user.Id, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(false);

        PinChangeStatus result = await _service.SetAsync(user.Id, "1234");

        result.Should().Be(PinChangeStatus.NotFound);
        await _sessions.DidNotReceiveWithAnyArgs().DeleteForUserAsync(default, default, default);
    }

    [Test]
    public async Task SetAsync_BuiltInAdminWithAdminPin_IsManagedByEnvironment()
    {
        _options.AdminPin = "2468";
        User admin = CreateUser(isBuiltIn: true);
        _users.FindAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);

        PinChangeStatus result = await _service.SetAsync(admin.Id, "1357");

        result.Should().Be(PinChangeStatus.ManagedByEnvironment);
        await _users.DidNotReceiveWithAnyArgs().SetPinHashAsync(default, default, default);
    }

    [Test]
    public async Task SetAsync_OtherProfileWithAdminPin_IsAllowed()
    {
        _options.AdminPin = "2468";
        User user = CreateUser();
        _users.FindAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        PinChangeStatus result = await _service.SetAsync(user.Id, "1357");

        result.Should().Be(PinChangeStatus.Success);
    }

    [Test]
    public async Task RemoveAsync_ProfileWithPin_RemovesTheHashAndKeepsTheSessions()
    {
        User user = CreateUser();
        _users.FindAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        PinChangeStatus result = await _service.RemoveAsync(user.Id);

        result.Should().Be(PinChangeStatus.Success);
        await _users.Received(1).SetPinHashAsync(user.Id, null, Arg.Any<CancellationToken>());
        await _sessions.DidNotReceiveWithAnyArgs().DeleteForUserAsync(default, default, default);
    }

    [Test]
    public async Task RemoveAsync_UnknownProfile_IsNotFound()
    {
        PinChangeStatus result = await _service.RemoveAsync(Guid.NewGuid());

        result.Should().Be(PinChangeStatus.NotFound);
    }

    [Test]
    public async Task RemoveAsync_BuiltInAdminWithAdminPin_IsManagedByEnvironment()
    {
        _options.AdminPin = "2468";
        User admin = CreateUser(isBuiltIn: true);
        _users.FindAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);

        PinChangeStatus result = await _service.RemoveAsync(admin.Id);

        result.Should().Be(PinChangeStatus.ManagedByEnvironment);
        await _users.DidNotReceiveWithAnyArgs().SetPinHashAsync(default, default, default);
    }

    [Test]
    public async Task RemoveAsync_BuiltInAdminWithoutAdminPin_IsAllowed()
    {
        User admin = CreateUser(isBuiltIn: true);
        _users.FindAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);

        PinChangeStatus result = await _service.RemoveAsync(admin.Id);

        result.Should().Be(PinChangeStatus.Success);
    }

    [TestCase(true, null, false)]
    [TestCase(true, "2468", true)]
    [TestCase(false, "2468", false)]
    public async Task GetStatusAsync_Always_TellsWhetherThePinIsSetAndManaged(
        bool isBuiltIn,
        string? adminPin,
        bool managed)
    {
        _options.AdminPin = adminPin;
        User user = CreateUser(isBuiltIn: isBuiltIn);
        _users.FindAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        PinStatus? status = await _service.GetStatusAsync(user.Id);

        status.Should().Be(new PinStatus(true, managed));
    }

    [Test]
    public async Task GetStatusAsync_ProfileWithoutPin_IsNotSet()
    {
        User user = CreateUser(pinHash: null);
        _users.FindAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        PinStatus? status = await _service.GetStatusAsync(user.Id);

        status.Should().Be(new PinStatus(false, false));
    }

    [Test]
    public async Task GetStatusAsync_UnknownProfile_IsNull()
    {
        (await _service.GetStatusAsync(Guid.NewGuid())).Should().BeNull();
    }

    private void AllowAttempt(User user, int failedAttempts, DateTimeOffset? lockoutEnd = null)
    {
        _users.BeginPinAttemptAsync(user.Id, _now, 5, _now.AddMinutes(5), Arg.Any<CancellationToken>())
            .Returns(new PinAttempt(true, failedAttempts, lockoutEnd));
    }

    private static User CreateUser(string? pinHash = StoredHash, bool isBuiltIn = false)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Name = isBuiltIn ? "Admin" : "Anna",
            NormalizedName = isBuiltIn ? "ADMIN" : "ANNA",
            Initials = "A",
            Color = "#1f6fd6",
            Role = isBuiltIn ? UserRole.Admin : UserRole.User,
            IsBuiltIn = isBuiltIn,
            PinHash = pinHash,
            CreatedAt = _now,
        };
    }
}
