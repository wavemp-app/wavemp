using AwesomeAssertions;
using WaveMP.Core.Users.Pins;

namespace WaveMP.Core.Users.Tests.Pins;

[TestFixture]
public class PinHasherTests
{
    private readonly PinHasher _hasher = new();

    [Test]
    public void Hash_Always_DoesNotContainThePin()
    {
        string hash = _hasher.Hash("482913");

        hash.Should().NotBe("482913");
        hash.Should().NotContain("482913");
    }

    [Test]
    public void Hash_SamePinTwice_GivesDifferentHashes()
    {
        string first = _hasher.Hash("1234");
        string second = _hasher.Hash("1234");

        first.Should().NotBe(second, "every hash has its own salt");
    }

    [Test]
    public void Hash_Always_FitsTheColumn()
    {
        _hasher.Hash("12345678").Length.Should().BeLessThanOrEqualTo(256);
    }

    [Test]
    public void Verify_CorrectPin_IsTrue()
    {
        string hash = _hasher.Hash("1234");

        _hasher.Verify(hash, "1234").Should().BeTrue();
    }

    [TestCase("1235")]
    [TestCase("12345")]
    [TestCase("123")]
    [TestCase("")]
    public void Verify_WrongPin_IsFalse(string pin)
    {
        string hash = _hasher.Hash("1234");

        _hasher.Verify(hash, pin).Should().BeFalse();
    }

    [TestCase("")]
    [TestCase("not a hash")]
    [TestCase("AQAAAA==")]
    public void Verify_DamagedHash_IsFalse(string hash)
    {
        _hasher.Verify(hash, "1234").Should().BeFalse();
    }

    [Test]
    public void Verify_HashOfAnotherHasher_IsTrue()
    {
        string hash = new PinHasher().Hash("2580");

        _hasher.Verify(hash, "2580").Should().BeTrue();
    }
}
