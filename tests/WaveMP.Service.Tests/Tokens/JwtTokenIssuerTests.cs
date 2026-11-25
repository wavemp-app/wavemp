using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using WaveMP.Core.Users.Sessions;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Tests.Tokens;

[TestFixture]
public class JwtTokenIssuerTests
{
    private static readonly DateTimeOffset _createdAt = DateTimeOffset.UtcNow.AddMinutes(-5);

    private SigningKeyStore _keys = null!;
    private ActiveSession _session = null!;

    [SetUp]
    public async Task SetUp()
    {
        _keys = await CreateKeyStoreAsync(1);
        User user = new()
        {
            Id = Guid.NewGuid(),
            Name = "Anna",
            NormalizedName = "ANNA",
            Initials = "A",
            Color = "#1f6fd6",
            Role = UserRole.Admin,
            IsBuiltIn = false,
            CreatedAt = _createdAt,
        };
        Session session = new()
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            CreatedAt = _createdAt,
            ExpiresAt = _createdAt.AddDays(365),
        };
        _session = new ActiveSession(session, user);
    }

    [Test]
    public void Issue_Always_NamesProfileAndSessionOnly()
    {
        string token = new JwtTokenIssuer(_keys, TimeProvider.System).Issue(_session);

        JsonWebToken jwt = new(token);
        jwt.Alg.Should().Be(SecurityAlgorithms.HmacSha256);
        jwt.Issuer.Should().Be("wavemp");
        jwt.Audiences.Should().Equal("wavemp");
        jwt.Subject.Should().Be(_session.User.Id.ToString());
        jwt.GetClaim("sid").Value.Should().Be(_session.Session.Id.ToString());
        jwt.ValidTo.Should().BeCloseTo(_session.Session.ExpiresAt.UtcDateTime, TimeSpan.FromSeconds(1));
        jwt.IssuedAt.Should().BeCloseTo(_session.Session.CreatedAt.UtcDateTime, TimeSpan.FromSeconds(1));
        jwt.Claims.Select(claim => claim.Type).Should().NotContain(["role", "name"]);
    }

    [Test]
    public async Task Issue_ValidatedWithTheSameKey_IsValid()
    {
        string token = new JwtTokenIssuer(_keys, TimeProvider.System).Issue(_session);

        TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(
            token,
            CreateParameters(_keys.Key));

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public async Task Issue_ValidatedWithAnotherKey_IsInvalid()
    {
        string token = new JwtTokenIssuer(_keys, TimeProvider.System).Issue(_session);
        SigningKeyStore otherKeys = await CreateKeyStoreAsync(2);

        TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(
            token,
            CreateParameters(otherKeys.Key));

        result.IsValid.Should().BeFalse();
    }

    [Test]
    public async Task IssueDeviceHubTicket_Always_IsShortLivedAndOnlyForTheHub()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(now);

        DeviceHubTicket ticket = new JwtTokenIssuer(_keys, clock)
            .IssueDeviceHubTicket(_session.User.Id, _session.Session.Id);

        ticket.ExpiresAt.Should().Be(now.AddSeconds(60));
        JsonWebToken jwt = new(ticket.Ticket);
        jwt.Issuer.Should().Be("wavemp");
        jwt.Audiences.Should().Equal("wavemp-device-hub");
        jwt.Subject.Should().Be(_session.User.Id.ToString());
        jwt.GetClaim("sid").Value.Should().Be(_session.Session.Id.ToString());
        jwt.Id.Should().NotBeNullOrEmpty();
        jwt.ValidTo.Should().BeCloseTo(now.AddSeconds(60).UtcDateTime, TimeSpan.FromSeconds(1));
        jwt.Claims.Select(claim => claim.Type).Should().NotContain(["role", "name"]);
        (await new JsonWebTokenHandler().ValidateTokenAsync(ticket.Ticket, CreateParameters(_keys.Key)))
            .IsValid.Should().BeFalse("a ticket is not a session token");
        (await new JsonWebTokenHandler().ValidateTokenAsync(
            ticket.Ticket,
            CreateParameters(_keys.Key, TokenDefaults.DeviceHubAudience))).IsValid.Should().BeTrue();
    }

    [Test]
    public void IssueDeviceHubTicket_TwoTickets_HaveDifferentIds()
    {
        JwtTokenIssuer issuer = new(_keys, TimeProvider.System);

        DeviceHubTicket first = issuer.IssueDeviceHubTicket(_session.User.Id, _session.Session.Id);
        DeviceHubTicket second = issuer.IssueDeviceHubTicket(_session.User.Id, _session.Session.Id);

        new JsonWebToken(first.Ticket).Id.Should().NotBe(new JsonWebToken(second.Ticket).Id);
    }

    private static TokenValidationParameters CreateParameters(
        SecurityKey key,
        string audience = TokenDefaults.Audience)
    {
        return new TokenValidationParameters
        {
            ValidIssuer = TokenDefaults.Issuer,
            ValidAudience = audience,
            ValidAlgorithms = [TokenDefaults.Algorithm],
            IssuerSigningKey = key,
        };
    }

    private static async Task<SigningKeyStore> CreateKeyStoreAsync(byte seed)
    {
        IServerSecretRepository secrets = Substitute.For<IServerSecretRepository>();
        secrets.FindAsync(ServerSecretNames.JwtSigningKey, Arg.Any<CancellationToken>())
            .Returns([.. Enumerable.Repeat(seed, SigningKeyStore.KeySize)]);
        ServiceProvider services = new ServiceCollection().AddScoped(_ => secrets).BuildServiceProvider();
        SigningKeyStore store = new(
            services.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            NullLogger<SigningKeyStore>.Instance);
        await store.InitializeAsync();
        return store;
    }
}
