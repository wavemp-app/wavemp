using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WaveMP.Core.Caching.Covers;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Users.Avatars;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Users.Tests.Avatars;

[TestFixture]
public class AvatarServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private IUserRepository _users = null!;
    private IAvatarStore _store = null!;
    private TimeProvider _timeProvider = null!;
    private AvatarService _service = null!;
    private User _user = null!;

    [SetUp]
    public void SetUp()
    {
        _users = Substitute.For<IUserRepository>();
        _store = Substitute.For<IAvatarStore>();
        _timeProvider = Substitute.For<TimeProvider>();
        _timeProvider.GetUtcNow().Returns(_now);
        _service = new AvatarService(_users, _store, _timeProvider, NullLogger<AvatarService>.Instance);
        _user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Anna",
            NormalizedName = "ANNA",
            Initials = "A",
            Color = "#1f6fd6",
            Role = UserRole.User,
            IsBuiltIn = false,
            CreatedAt = _now.AddDays(-1),
        };
    }

    [Test]
    public async Task UploadAsync_Saved_SetsANewVersionWithoutPreset()
    {
        GivenUser(presetId: "leaf", updatedAt: _now.AddHours(-1));
        _store.SaveAsync(_user.Id, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(AvatarSaveStatus.Saved);

        AvatarResult result = await _service.UploadAsync(_user.Id, Stream.Null);

        result.Status.Should().Be(AvatarStatus.Saved);
        result.User.Should().BeSameAs(_user);
        _user.AvatarUpdatedAt.Should().Be(_now);
        _user.AvatarPresetId.Should().BeNull();
        await _users.Received(1).UpdateAsync(_user, Arg.Any<CancellationToken>());
    }

    [TestCase(AvatarSaveStatus.UnsupportedFormat, AvatarStatus.UnsupportedFormat)]
    [TestCase(AvatarSaveStatus.Unreadable, AvatarStatus.Unreadable)]
    [TestCase(AvatarSaveStatus.Unavailable, AvatarStatus.Unavailable)]
    public async Task UploadAsync_NotSaved_ReturnsWhyAndKeepsTheProfile(AvatarSaveStatus saved, AvatarStatus expected)
    {
        GivenUser();
        _store.SaveAsync(_user.Id, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(saved);

        AvatarResult result = await _service.UploadAsync(_user.Id, Stream.Null);

        result.Should().Be(AvatarResult.Failed(expected));
        _user.AvatarUpdatedAt.Should().BeNull();
        await _users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Test]
    public async Task UploadAsync_MissingProfile_StoresNothing()
    {
        AvatarResult result = await _service.UploadAsync(Guid.NewGuid(), Stream.Null);

        result.Status.Should().Be(AvatarStatus.UserNotFound);
        await _store.DidNotReceiveWithAnyArgs().SaveAsync(default, default!);
    }

    [Test]
    public async Task UploadAsync_ProfileDeletedDuringTheUpload_RemovesTheFiles()
    {
        GivenUser();
        _store.SaveAsync(_user.Id, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(AvatarSaveStatus.Saved);
        _users.UpdateAsync(_user, Arg.Any<CancellationToken>()).ThrowsAsync(new UserNotFoundException(_user.Id));

        AvatarResult result = await _service.UploadAsync(_user.Id, Stream.Null);

        result.Status.Should().Be(AvatarStatus.UserNotFound);
        _store.Received(1).Delete(_user.Id);
    }

    [Test]
    public async Task UploadAsync_TwiceInTheSameMicrosecond_GetsANewerVersion()
    {
        GivenUser(updatedAt: _now);
        _store.SaveAsync(_user.Id, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(AvatarSaveStatus.Saved);

        await _service.UploadAsync(_user.Id, Stream.Null);

        _user.AvatarUpdatedAt.Should().Be(_now.AddTicks(TimeSpan.TicksPerMicrosecond));
    }

    [Test]
    public async Task ChoosePresetAsync_KnownPreset_SetsItAndRemovesAnUploadedPicture()
    {
        GivenUser(updatedAt: _now.AddHours(-1));

        AvatarResult result = await _service.ChoosePresetAsync(_user.Id, "bubble");

        result.Status.Should().Be(AvatarStatus.Saved);
        _user.AvatarPresetId.Should().Be("bubble");
        _user.AvatarUpdatedAt.Should().Be(_now);
        _store.Received(1).Delete(_user.Id);
    }

    [TestCase("unknown")]
    [TestCase("Bubble")]
    [TestCase("")]
    public async Task ChoosePresetAsync_UnknownPreset_ReturnsPresetNotFound(string presetId)
    {
        GivenUser();

        AvatarResult result = await _service.ChoosePresetAsync(_user.Id, presetId);

        result.Status.Should().Be(AvatarStatus.PresetNotFound);
        await _users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Test]
    public async Task ChoosePresetAsync_MissingProfile_ReturnsUserNotFound()
    {
        AvatarResult result = await _service.ChoosePresetAsync(Guid.NewGuid(), "leaf");

        result.Status.Should().Be(AvatarStatus.UserNotFound);
    }

    [Test]
    public async Task RemoveAsync_WithAvatar_ClearsItAndRemovesTheFiles()
    {
        GivenUser(presetId: "sun", updatedAt: _now);

        bool removed = await _service.RemoveAsync(_user.Id);

        removed.Should().BeTrue();
        _user.AvatarUpdatedAt.Should().BeNull();
        _user.AvatarPresetId.Should().BeNull();
        await _users.Received(1).UpdateAsync(_user, Arg.Any<CancellationToken>());
        _store.Received(1).Delete(_user.Id);
    }

    [Test]
    public async Task RemoveAsync_WithoutAvatar_SucceedsWithoutSaving()
    {
        GivenUser();

        bool removed = await _service.RemoveAsync(_user.Id);

        removed.Should().BeTrue();
        await _users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Test]
    public async Task RemoveAsync_MissingProfile_ReturnsFalse()
    {
        (await _service.RemoveAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Test]
    public async Task GetAsync_UploadedAvatar_DeliversTheStoredSizeWithVersionedTag()
    {
        GivenUser(updatedAt: _now);
        Stream picture = new MemoryStream([1, 2, 3]);
        _store.Open(_user.Id, 300).Returns(picture);

        CoverDeliveryResult result = await _service.GetAsync(_user.Id, 120);

        result.Status.Should().Be(CoverStatus.Found);
        result.Delivery!.Content.Should().BeSameAs(picture);
        result.Delivery.ContentType.Should().Be("image/webp");
        result.Delivery.ETag.Should().Be($"{AvatarVersions.Of(_now)}-300");
        result.Delivery.LastModified.Should().Be(_now);
    }

    [Test]
    public async Task GetAsync_WithoutSize_DeliversTheLargestPicture()
    {
        GivenUser(updatedAt: _now);
        _store.Open(_user.Id, 1000).Returns(new MemoryStream([1]));

        CoverDeliveryResult result = await _service.GetAsync(_user.Id, null);

        result.Delivery!.ETag.Should().EndWith("-1000");
    }

    [Test]
    public async Task GetAsync_PresetAvatar_DeliversTheBuiltInPicture()
    {
        GivenUser(presetId: "fish", updatedAt: _now);

        CoverDeliveryResult result = await _service.GetAsync(_user.Id, 96);

        result.Status.Should().Be(CoverStatus.Found);
        await using Stream content = result.Delivery!.Content;
        (await ReadHeaderAsync(content)).Should().Be(ImageFormats.WebP);
        result.Delivery.ETag.Should().StartWith($"{AvatarVersions.Of(_now)}-fish-").And.EndWith("-96");
        _store.DidNotReceiveWithAnyArgs().Open(default, default);
    }

    [Test]
    public async Task GetAsync_UnknownPresetOfAnOlderVersion_ReturnsNotFound()
    {
        GivenUser(presetId: "gone", updatedAt: _now);

        CoverDeliveryResult result = await _service.GetAsync(_user.Id, 96);

        result.Should().Be(CoverDeliveryResult.NotFound);
    }

    [Test]
    public async Task GetAsync_WithoutAvatar_ReturnsNotFound()
    {
        GivenUser();

        (await _service.GetAsync(_user.Id, 96)).Should().Be(CoverDeliveryResult.NotFound);
    }

    [Test]
    public async Task GetAsync_MissingFile_ReturnsNotFound()
    {
        GivenUser(updatedAt: _now);

        (await _service.GetAsync(_user.Id, 96)).Should().Be(CoverDeliveryResult.NotFound);
    }

    [Test]
    public async Task GetAsync_MissingProfile_ReturnsOwnerNotFound()
    {
        (await _service.GetAsync(Guid.NewGuid(), 96)).Should().Be(CoverDeliveryResult.OwnerNotFound);
    }

    [Test]
    public async Task GetPreset_KnownPreset_DeliversItWithoutLastModified()
    {
        CoverDeliveryResult result = _service.GetPreset("star", 300);

        result.Status.Should().Be(CoverStatus.Found);
        await using Stream content = result.Delivery!.Content;
        (await ReadHeaderAsync(content)).Should().Be(ImageFormats.WebP);
        result.Delivery.ETag.Should().StartWith("preset-star-").And.EndWith("-300");
        result.Delivery.LastModified.Should().BeNull();
    }

    [Test]
    public void GetPreset_UnknownPreset_ReturnsNotFound()
    {
        _service.GetPreset("unknown", 300).Should().Be(CoverDeliveryResult.NotFound);
    }

    [Test]
    public async Task RemoveOrphansAsync_Always_RemovesFoldersOfProfilesWithoutUploadedAvatar()
    {
        var kept = Guid.NewGuid();
        var orphan = Guid.NewGuid();
        _store.ListUserIds().Returns([kept, orphan]);
        _users.ListUploadedAvatarIdsAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2),
                Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { kept });

        await _service.RemoveOrphansAsync();

        _store.Received(1).Delete(orphan);
        _store.DidNotReceive().Delete(kept);
    }

    private void GivenUser(string? presetId = null, DateTimeOffset? updatedAt = null)
    {
        _user.AvatarPresetId = presetId;
        _user.AvatarUpdatedAt = updatedAt;
        _users.FindAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
    }

    private static async Task<string?> ReadHeaderAsync(Stream content)
    {
        byte[] header = new byte[ImageFormats.HeaderLength];
        int read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        return ImageFormats.Detect(header.AsSpan(0, read));
    }
}
