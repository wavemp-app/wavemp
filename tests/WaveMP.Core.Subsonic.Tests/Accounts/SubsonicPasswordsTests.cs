using AwesomeAssertions;
using WaveMP.Core.Subsonic.Accounts;

namespace WaveMP.Core.Subsonic.Tests.Accounts;

[TestFixture]
public class SubsonicPasswordsTests
{
    [Test]
    public void Generate_CreatesDifferentPasswordsWithoutLookalikes()
    {
        string[] passwords = [.. Enumerable.Range(0, 50).Select(_ => SubsonicPasswords.Generate())];

        passwords.Should().OnlyHaveUniqueItems();
        passwords.Should().AllSatisfy(password =>
        {
            password.Should().HaveLength(SubsonicPasswords.Length);
            password.Should().MatchRegex("^[A-Za-z0-9]+$");
            password.Should().NotContainAny("0", "O", "1", "l", "I");
        });
    }

    [Test]
    public void Hash_IsTheLowerCaseSha256()
    {
        SubsonicPasswords.Hash("abc").Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }
}
