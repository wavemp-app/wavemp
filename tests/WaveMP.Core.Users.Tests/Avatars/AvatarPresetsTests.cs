using AwesomeAssertions;
using WaveMP.Core.Caching.Covers;
using WaveMP.Core.Users.Avatars;

namespace WaveMP.Core.Users.Tests.Avatars;

[TestFixture]
public class AvatarPresetsTests
{
    private static IEnumerable<TestCaseData> PresetSizes()
    {
        foreach (string id in AvatarPresets.Ids)
        {
            foreach (int size in CoverSizes.All)
            {
                yield return new TestCaseData(id, size);
            }
        }
    }

    [Test]
    public void Ids_Always_AreUniqueLowerCaseAndFitTheColumn()
    {
        AvatarPresets.Ids.Should().NotBeEmpty().And.OnlyHaveUniqueItems();
        AvatarPresets.Ids.Should().AllSatisfy(id =>
        {
            id.Should().MatchRegex("^[a-z][a-z0-9-]*$");
            // The column users.avatar_preset_id holds 32 characters.
            id.Length.Should().BeLessThanOrEqualTo(32);
        });
    }

    [TestCaseSource(nameof(PresetSizes))]
    [Category("Media")]
    public async Task Open_EveryPresetAndSize_IsASquareWebPOfThatSize(string id, int size)
    {
        await using Stream? content = AvatarPresets.Open(id, size);

        content.Should().NotBeNull();
        using MemoryStream copy = new();
        await content!.CopyToAsync(copy);
        byte[] bytes = copy.ToArray();
        bytes.Should().StartWith("RIFF"u8.ToArray());
        (await Testing.Media.ImageProbe.GetSizeAsync(bytes)).Should().Be((size, size));
    }

    [Test]
    public void Open_RequestedSize_RoundsItUp()
    {
        using Stream? small = AvatarPresets.Open("leaf", 40);
        using Stream? exact = AvatarPresets.Open("leaf", 96);

        small.Should().NotBeNull();
        small!.Length.Should().Be(exact!.Length);
    }

    [TestCase("unknown")]
    [TestCase("LEAF")]
    [TestCase(null)]
    public void UnknownId_HasNoPictureAndNoHash(string? id)
    {
        AvatarPresets.Contains(id).Should().BeFalse();
        if (id is not null)
        {
            AvatarPresets.Open(id, 96).Should().BeNull();
            AvatarPresets.HashOf(id, 96).Should().BeNull();
        }
    }

    [Test]
    public void HashOf_Always_IsStableAndDiffersBetweenSizes()
    {
        string? first = AvatarPresets.HashOf("sun", 300);

        first.Should().MatchRegex("^[0-9a-f]{12}$");
        AvatarPresets.HashOf("sun", 300).Should().Be(first);
        AvatarPresets.HashOf("sun", 96).Should().NotBe(first);
    }
}
