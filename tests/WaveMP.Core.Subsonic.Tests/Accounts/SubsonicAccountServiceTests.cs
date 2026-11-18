using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Subsonic.Accounts;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Subsonic.Tests.Accounts;

[TestFixture]
public class SubsonicAccountServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _userId = Guid.NewGuid();

    private ISubsonicRepository _repository = null!;
    private SubsonicAccountService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<ISubsonicRepository>();
        TimeProvider time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(_now);
        _service = new SubsonicAccountService(_repository, new FakeProtector(), time);
    }

    [Test]
    public async Task GetAsync_NoAccount_IsOff()
    {
        SubsonicAccountState state = await _service.GetAsync(_userId);

        state.Should().Be(SubsonicAccountState.Off);
    }

    [Test]
    public async Task SetEnabledAsync_ReturnsTheNewState()
    {
        _repository.SetEnabledAsync(_userId, true, Arg.Any<CancellationToken>())
            .Returns(new SubsonicAccount { UserId = _userId, Enabled = true });

        SubsonicAccountState state = await _service.SetEnabledAsync(_userId, true);

        state.Should().Be(new SubsonicAccountState(true, false, null));
    }

    [Test]
    public async Task CreatePasswordAsync_StoresOnlyTheProtectedPasswordAndItsHash()
    {
        _repository.SetPasswordAsync(
                _userId,
                Arg.Any<string>(),
                Arg.Any<string>(),
                _now,
                Arg.Any<CancellationToken>())
            .Returns(call => new SubsonicAccount
            {
                UserId = _userId,
                ProtectedPassword = call.ArgAt<string>(1),
                PasswordHash = call.ArgAt<string>(2),
                PasswordCreatedAt = _now,
            });

        SubsonicPassword created = await _service.CreatePasswordAsync(_userId);

        created.Password.Should().HaveLength(SubsonicPasswords.Length);
        created.CreatedAt.Should().Be(_now);
        created.State.Should().Be(new SubsonicAccountState(false, true, _now));
        await _repository.Received(1).SetPasswordAsync(
            _userId,
            FakeProtector.Prefix + created.Password,
            SubsonicPasswords.Hash(created.Password),
            _now,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RevokePasswordAsync_RemovesThePassword()
    {
        _repository.FindAccountAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new SubsonicAccount { UserId = _userId, Enabled = true });

        SubsonicAccountState state = await _service.RevokePasswordAsync(_userId);

        await _repository.Received(1).RemovePasswordAsync(_userId, Arg.Any<CancellationToken>());
        state.Should().Be(new SubsonicAccountState(true, false, null));
    }
}
