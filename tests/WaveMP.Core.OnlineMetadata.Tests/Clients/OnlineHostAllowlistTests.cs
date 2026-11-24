using AwesomeAssertions;
using WaveMP.Core.OnlineMetadata.Clients;

namespace WaveMP.Core.OnlineMetadata.Tests.Clients;

[TestFixture]
public class OnlineHostAllowlistTests
{
    private const string MusicBrainz = "https://musicbrainz.org";

    [TestCase("https://www.wikidata.org/w/api.php?action=wbgetentities")]
    [TestCase("https://commons.wikimedia.org/w/api.php")]
    [TestCase("https://upload.wikimedia.org/wikipedia/commons/a/ab/Picture.jpg")]
    [TestCase("https://webservice.fanart.tv/v3/music/5b11f4ce-a62d-471e-81fc-a69a8278c7da")]
    [TestCase("https://assets.fanart.tv/fanart/music/x/artistthumb/y.jpg")]
    [TestCase("https://UPLOAD.wikimedia.org./picture.jpg")]
    [TestCase("https://musicbrainz.org/ws/2/artist?query=x")]
    public void IsAllowed_HttpsAddressOfAnAllowedHost_IsTrue(string url)
    {
        OnlineHostAllowlist.IsAllowed(new Uri(url), MusicBrainz).Should().BeTrue();
    }

    [TestCase("http://upload.wikimedia.org/picture.jpg")]
    [TestCase("https://upload.wikimedia.org:8443/picture.jpg")]
    [TestCase("https://user:secret@upload.wikimedia.org/picture.jpg")]
    [TestCase("https://upload.wikimedia.org.example.com/picture.jpg")]
    [TestCase("https://evil.example/upload.wikimedia.org")]
    [TestCase("https://wikimedia.org/picture.jpg")]
    [TestCase("https://127.0.0.1/picture.jpg")]
    [TestCase("https://localhost/picture.jpg")]
    [TestCase("https://169.254.169.254/latest/meta-data")]
    [TestCase("http://musicbrainz.org/ws/2/artist")]
    [TestCase("file:///etc/passwd")]
    [TestCase("ftp://upload.wikimedia.org/picture.jpg")]
    public void IsAllowed_AnyOtherAddress_IsFalse(string url)
    {
        OnlineHostAllowlist.IsAllowed(new Uri(url), MusicBrainz).Should().BeFalse();
    }

    [TestCase("http://mirror.local:5000/ws/2/artist", true)]
    [TestCase("http://mirror.local:5001/ws/2/artist", false)]
    [TestCase("https://mirror.local:5000/ws/2/artist", false)]
    [TestCase("http://other.local:5000/ws/2/artist", false)]
    public void IsAllowed_ConfiguredMirror_AllowsExactlyItsOrigin(string url, bool allowed)
    {
        OnlineHostAllowlist.IsAllowed(new Uri(url), "http://mirror.local:5000").Should().Be(allowed);
    }

    [Test]
    public void IsAllowed_MirrorAddressThatIsNoWebAddress_AllowsOnlyTheFixedHosts()
    {
        OnlineHostAllowlist.IsAllowed(new Uri("file:///data/x"), "file:///data").Should().BeFalse();
        OnlineHostAllowlist.IsAllowed(new Uri("https://www.wikidata.org/w/api.php"), null).Should().BeTrue();
    }
}
