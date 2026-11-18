using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using WaveMP.Core.Streaming.Signatures;
using WaveMP.Service.Signatures;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Tests.Signatures;

[TestFixture]
public class MediaUrlFactoryTests
{
    [Test]
    public void ToQuery_Always_CarriesUserSessionExpiryAndSignature()
    {
        var userId = Guid.Parse("0195f3a0-0000-7000-8000-000000000002");
        var sessionId = Guid.Parse("0195f3a0-0000-7000-8000-000000000003");
        SignedMediaUrl signed = new(
            new MediaUrlClaims(
                MediaResource.TrackAudio,
                Guid.NewGuid(),
                userId,
                sessionId,
                DateTimeOffset.FromUnixTimeSeconds(1_800_000_000)),
            "a-b_c");

        string query = MediaUrlFactory.ToQuery(signed).Value!;

        query.Should().Be(
            "?uid=0195f3a0-0000-7000-8000-000000000002&sid=0195f3a0-0000-7000-8000-000000000003"
            + "&exp=1800000000&sig=a-b_c");
    }

    [Test]
    public void SignedQueryFor_RequestWithSignedUrl_ReusesItsSignature()
    {
        IMediaUrlIssuer issuer = Substitute.For<IMediaUrlIssuer>();
        MediaUrlFactory factory = new(issuer, Substitute.For<LinkGenerator>());
        DefaultHttpContext context = Context(MediaSignatureDefaults.AuthenticationScheme);
        context.Request.QueryString = new QueryString("?media=Video&uid=u&sid=s&exp=1800000000&sig=a-b_c");

        QueryString query = factory.SignedQueryFor(context, MediaResource.TrackHls, Guid.NewGuid(), null);

        query.Value.Should().Be("?uid=u&sid=s&exp=1800000000&sig=a-b_c");
        issuer.DidNotReceiveWithAnyArgs().Issue(default, default, default, default);
    }

    [Test]
    public void SignedQueryFor_RequestWithSessionToken_IssuesANewSignature()
    {
        var trackId = Guid.NewGuid();
        var userId = Guid.Parse("0195f3a0-0000-7000-8000-000000000002");
        var sessionId = Guid.Parse("0195f3a0-0000-7000-8000-000000000003");
        IMediaUrlIssuer issuer = Substitute.For<IMediaUrlIssuer>();
        issuer.Issue(MediaResource.TrackHls, trackId, userId, sessionId, TimeSpan.FromMinutes(3))
            .Returns(new SignedMediaUrl(
                new MediaUrlClaims(
                    MediaResource.TrackHls,
                    trackId,
                    userId,
                    sessionId,
                    DateTimeOffset.FromUnixTimeSeconds(1_800_000_000)),
                "new"));
        MediaUrlFactory factory = new(issuer, Substitute.For<LinkGenerator>());
        DefaultHttpContext context = Context("Bearer", userId, sessionId);
        context.Request.QueryString = new QueryString("?uid=u&sid=s&exp=1&sig=forged");

        QueryString query = factory.SignedQueryFor(context, MediaResource.TrackHls, trackId, TimeSpan.FromMinutes(3));

        query.Value.Should().Be(
            "?uid=0195f3a0-0000-7000-8000-000000000002&sid=0195f3a0-0000-7000-8000-000000000003"
            + "&exp=1800000000&sig=new");
    }

    [Test]
    public void CreateCoverUrl_Always_SignsTheCoverForACastingDeviceInTheLargeSize()
    {
        var trackId = Guid.Parse("0195f3a0-0000-7000-8000-000000000001");
        var userId = Guid.Parse("0195f3a0-0000-7000-8000-000000000002");
        var sessionId = Guid.Parse("0195f3a0-0000-7000-8000-000000000003");
        var expires = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        IMediaUrlIssuer issuer = Substitute.For<IMediaUrlIssuer>();
        issuer.Issue(
                MediaResource.TrackCover,
                trackId,
                userId,
                sessionId,
                TimeSpan.FromMinutes(3),
                MediaUrlTarget.Cast)
            .Returns(new SignedMediaUrl(
                new MediaUrlClaims(MediaResource.TrackCover, trackId, userId, sessionId, expires),
                "cover"));
        LinkGenerator links = Links($"/api/v1/tracks/{trackId}/cover");
        MediaUrlFactory factory = new(issuer, links);

        MediaUrl url = factory.CreateCoverUrl(Context("Bearer", userId, sessionId), trackId, TimeSpan.FromMinutes(3));

        url.Url.Should().Be(
            $"/api/v1/tracks/{trackId}/cover?size=1000&uid=0195f3a0-0000-7000-8000-000000000002"
            + "&sid=0195f3a0-0000-7000-8000-000000000003&exp=1800000000&sig=cover");
        url.ExpiresAt.Should().Be(expires);
    }

    [Test]
    public void CreateAudioStreamUrl_ForACastingDevice_PassesTheTargetToTheIssuer()
    {
        var trackId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        IMediaUrlIssuer issuer = Substitute.For<IMediaUrlIssuer>();
        issuer.Issue(default, default, default, default, default, default)
            .ReturnsForAnyArgs(new SignedMediaUrl(
                new MediaUrlClaims(
                    MediaResource.TrackAudio,
                    trackId,
                    userId,
                    sessionId,
                    DateTimeOffset.FromUnixTimeSeconds(1_800_000_000)),
                "audio"));
        MediaUrlFactory factory = new(issuer, Links($"/api/v1/tracks/{trackId}/stream"));

        factory.CreateAudioStreamUrl(Context("Bearer", userId, sessionId), trackId, null, MediaUrlTarget.Cast);

        issuer.Received(1).Issue(MediaResource.TrackAudio, trackId, userId, sessionId, null, MediaUrlTarget.Cast);
    }

    private static LinkGenerator Links(string path)
    {
        LinkGenerator links = Substitute.For<LinkGenerator>();
        links.GetPathByAddress(
                Arg.Any<HttpContext>(),
                Arg.Any<string>(),
                Arg.Any<RouteValueDictionary>(),
                Arg.Any<RouteValueDictionary?>(),
                Arg.Any<PathString?>(),
                Arg.Any<FragmentString>(),
                Arg.Any<LinkOptions?>())
            .Returns(path);
        return links;
    }

    private static DefaultHttpContext Context(string scheme, Guid? userId = null, Guid? sessionId = null)
    {
        ClaimsIdentity identity = new(
            [
                new Claim(WaveMpClaimTypes.Subject, (userId ?? Guid.NewGuid()).ToString()),
                new Claim(WaveMpClaimTypes.SessionId, (sessionId ?? Guid.NewGuid()).ToString()),
            ],
            scheme);
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }
}
