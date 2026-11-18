using System.Text;
using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Subsonic.Accounts;
using WaveMP.Core.Subsonic.Authentication;
using WaveMP.Core.Subsonic.Responses;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Subsonic.Tests.Authentication;

[TestFixture]
public class SubsonicAuthenticatorTests
{
    private const string Password = "abcdEFGH2345jkmnPQRS6789";

    private readonly Guid _userId = Guid.NewGuid();

    private ISubsonicRepository _repository = null!;
    private SubsonicAuthenticator _authenticator = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<ISubsonicRepository>();
        Login(enabled: true);
        _authenticator = new SubsonicAuthenticator(_repository, new FakeProtector());
    }

    [Test]
    public async Task AuthenticateAsync_PlainPassword_SignsIn()
    {
        SubsonicAuthentication result = await AuthenticateAsync(("u", "Anna"), ("p", Password));

        result.User.Should().Be(new SubsonicUser(_userId, "Anna"));
        result.Error.Should().BeNull();
    }

    [Test]
    public async Task AuthenticateAsync_HexEncodedPassword_SignsIn()
    {
        string encoded = "enc:" + Convert.ToHexString(Encoding.UTF8.GetBytes(Password));

        SubsonicAuthentication result = await AuthenticateAsync(("u", "Anna"), ("p", encoded));

        result.IsAuthenticated.Should().BeTrue();
    }

    [Test]
    public async Task AuthenticateAsync_Token_SignsIn()
    {
        string token = SubsonicAuthenticator.CreateToken(Password, "c19b2d");

        SubsonicAuthentication result = await AuthenticateAsync(("u", "Anna"), ("t", token), ("s", "c19b2d"));

        result.IsAuthenticated.Should().BeTrue();
    }

    [Test]
    public void CreateToken_IsTheMd5OfPasswordAndSalt()
    {
        // The example of the Subsonic specification.
        SubsonicAuthenticator.CreateToken("sesame", "c19b2d").Should().Be("26719a1196d2a940705a59634eb18eab");
    }

    [Test]
    public async Task AuthenticateAsync_UserNameInOtherCase_SignsIn()
    {
        SubsonicAuthentication result = await AuthenticateAsync(("u", "ANNA"), ("p", Password));

        result.IsAuthenticated.Should().BeTrue();
        await _repository.Received(1).FindLoginByNameAsync("ANNA", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AuthenticateAsync_ApiKey_SignsIn()
    {
        SubsonicAuthentication result = await AuthenticateAsync(("apiKey", Password));

        result.User.Should().Be(new SubsonicUser(_userId, "Anna"));
    }

    [Test]
    public async Task AuthenticateAsync_WrongPassword_FailsWithWrongCredentials()
    {
        SubsonicAuthentication result = await AuthenticateAsync(("u", "Anna"), ("p", "wrong"));

        result.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(SubsonicErrorCode.WrongCredentials);
    }

    [Test]
    public async Task AuthenticateAsync_WrongToken_FailsWithWrongCredentials()
    {
        string token = SubsonicAuthenticator.CreateToken("wrong", "salt");

        SubsonicAuthentication result = await AuthenticateAsync(("u", "Anna"), ("t", token), ("s", "salt"));

        result.Error.Should().Be(SubsonicErrorCode.WrongCredentials);
    }

    [Test]
    public async Task AuthenticateAsync_UnknownUser_FailsWithWrongCredentials()
    {
        SubsonicAuthentication result = await AuthenticateAsync(("u", "Bob"), ("p", Password));

        result.Error.Should().Be(SubsonicErrorCode.WrongCredentials);
    }

    [Test]
    public async Task AuthenticateAsync_AccessOff_FailsLikeAWrongPassword()
    {
        Login(enabled: false);

        SubsonicAuthentication password = await AuthenticateAsync(("u", "Anna"), ("p", Password));
        SubsonicAuthentication apiKey = await AuthenticateAsync(("apiKey", Password));

        password.Error.Should().Be(SubsonicErrorCode.WrongCredentials);
        apiKey.Error.Should().Be(SubsonicErrorCode.InvalidApiKey);
    }

    [Test]
    public async Task AuthenticateAsync_NoAppPassword_Fails()
    {
        _repository.FindLoginByNameAsync("ANNA", Arg.Any<CancellationToken>())
            .Returns(new SubsonicLogin(_userId, "Anna", true, null));

        SubsonicAuthentication result = await AuthenticateAsync(("u", "Anna"), ("p", string.Empty));

        result.Error.Should().Be(SubsonicErrorCode.WrongCredentials);
    }

    [Test]
    public async Task AuthenticateAsync_UnreadablePassword_Fails()
    {
        // Another key of Data Protection, e.g. after the keys were lost.
        _repository.FindLoginByNameAsync("ANNA", Arg.Any<CancellationToken>())
            .Returns(new SubsonicLogin(_userId, "Anna", true, "unreadable"));

        SubsonicAuthentication result = await AuthenticateAsync(("u", "Anna"), ("p", Password));

        result.Error.Should().Be(SubsonicErrorCode.WrongCredentials);
    }

    [Test]
    public async Task AuthenticateAsync_UnknownApiKey_FailsWithInvalidApiKey()
    {
        SubsonicAuthentication result = await AuthenticateAsync(("apiKey", "unknown"));

        result.Error.Should().Be(SubsonicErrorCode.InvalidApiKey);
    }

    [Test]
    public async Task AuthenticateAsync_ApiKeyWithUserName_FailsWithConflictingAuthentication()
    {
        SubsonicAuthentication result = await AuthenticateAsync(("apiKey", Password), ("u", "Anna"));

        result.Error.Should().Be(SubsonicErrorCode.ConflictingAuthentication);
    }

    [Test]
    public async Task AuthenticateAsync_PasswordAndToken_FailsWithConflictingAuthentication()
    {
        SubsonicAuthentication result = await AuthenticateAsync(
            ("u", "Anna"),
            ("p", Password),
            ("t", "token"),
            ("s", "salt"));

        result.Error.Should().Be(SubsonicErrorCode.ConflictingAuthentication);
    }

    [Test]
    public async Task AuthenticateAsync_NoCredentials_FailsWithMissingParameter()
    {
        (await AuthenticateAsync()).Error.Should().Be(SubsonicErrorCode.MissingParameter);
        (await AuthenticateAsync(("u", "Anna"))).Error.Should().Be(SubsonicErrorCode.MissingParameter);
        (await AuthenticateAsync(("u", "Anna"), ("t", "token"))).Error
            .Should().Be(SubsonicErrorCode.MissingParameter);
        (await AuthenticateAsync(("p", Password))).Error.Should().Be(SubsonicErrorCode.MissingParameter);
    }

    private void Login(bool enabled)
    {
        SubsonicLogin login = new(_userId, "Anna", enabled, FakeProtector.Prefix + Password);
        _repository.FindLoginByNameAsync("ANNA", Arg.Any<CancellationToken>()).Returns(login);
        _repository.FindLoginByPasswordHashAsync(SubsonicPasswords.Hash(Password), Arg.Any<CancellationToken>())
            .Returns(login);
    }

    private Task<SubsonicAuthentication> AuthenticateAsync(params (string Name, string Value)[] values)
    {
        return _authenticator.AuthenticateAsync(SubsonicCredentials.From(TestParameters.Of(values)));
    }
}
