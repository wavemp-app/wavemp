using AwesomeAssertions;
using WaveMP.Core.Lyrics.Downloads;

namespace WaveMP.Core.Lyrics.Tests.Downloads;

[TestFixture]
public class LrclibChallengeTests
{
    [TestCase("FFFF")]
    [TestCase("0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")]
    [TestCase("00FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")]
    public void Solve_Target_FindsANonceWhoseHashIsAtMostTheTarget(string target)
    {
        long nonce = LrclibChallenge.Solve("prefix", target);

        LrclibChallenge.IsSolution("prefix", target, nonce).Should().BeTrue();
    }

    [Test]
    public void IsSolution_HashAboveTheTarget_IsFalse()
    {
        LrclibChallenge.IsSolution("prefix", new string('0', 64), 0).Should().BeFalse();
    }

    [Test]
    public void Solve_Stopped_Throws()
    {
        using CancellationTokenSource stop = new();
        stop.Cancel();

        Action act = () => LrclibChallenge.Solve("prefix", new string('0', 64), stop.Token);

        act.Should().Throw<OperationCanceledException>();
    }
}
