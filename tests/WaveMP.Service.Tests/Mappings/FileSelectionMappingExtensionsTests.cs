using AwesomeAssertions;
using WaveMP.Core.Scanning.Selections;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public class FileSelectionMappingExtensionsTests
{
    [Test]
    public void ToDto_Details_MapsEveryCategory()
    {
        var selected = Guid.NewGuid();
        var automatic = Guid.NewGuid();
        CategorySelection missing = new(
            FileSelectionState.Missing,
            selected,
            "A/Song.mp3",
            automatic,
            automatic,
            [automatic]);
        CategorySelection none = new(FileSelectionState.Automatic, null, null, null, null, []);
        var trackId = Guid.NewGuid();

        TrackFileSelectionDto dto = new FileSelectionDetails(trackId, missing, none, none).ToDto();

        dto.TrackId.Should().Be(trackId);
        dto.Audio.Should().BeEquivalentTo(new FileSelectionDto
        {
            State = FileSelectionStateDto.Missing,
            SelectedFileId = selected,
            SelectedPath = "A/Song.mp3",
            ActiveFileId = automatic,
            AutomaticFileId = automatic,
            CandidateFileIds = [automatic],
        });
        dto.Video.State.Should().Be(FileSelectionStateDto.Automatic);
        dto.Cover.CandidateFileIds.Should().BeEmpty();
    }

    [Test]
    public void ToDto_SummaryWithAlbum_MapsReferencesAndStates()
    {
        TrackFileSummaryRow row = new(
            Guid.NewGuid(),
            "Song",
            Guid.NewGuid(),
            "Band",
            Guid.NewGuid(),
            "Album",
            null,
            null,
            true,
            "Band/Album",
            "Song",
            2,
            1,
            3,
            FileSelectionState.Selected,
            FileSelectionState.Automatic,
            FileSelectionState.Missing);

        TrackFileSummaryDto dto = row.ToDto();

        dto.Should().BeEquivalentTo(new
        {
            row.TrackId,
            Title = "Song",
            Artist = new { Id = row.ArtistId!.Value, Name = "Band" },
            Album = new
            {
                Id = row.AlbumId!.Value,
                Title = "Album",
                Artist = (ArtistReferenceDto?)null,
                IsCompilation = true,
            },
            DirectoryPath = "Band/Album",
            BaseName = "Song",
            AudioCount = 2,
            VideoCount = 1,
            CoverCount = 3,
            Audio = FileSelectionStateDto.Selected,
            Video = FileSelectionStateDto.Automatic,
            Cover = FileSelectionStateDto.Missing,
        });
    }

    [Test]
    public void ToChoices_Request_TakesOverTheIds()
    {
        var audio = Guid.NewGuid();
        var cover = Guid.NewGuid();

        FileChoices choices = new UpdateTrackFileSelectionDto { AudioFileId = audio, CoverFileId = cover }.ToChoices();

        choices.Should().Be(new FileChoices(audio, null, cover));
    }

    [Test]
    public void Enums_AllValues_AreMapped()
    {
        Enum.GetValues<FileSelectionState>().Select(state => state.ToDto()).Should().OnlyHaveUniqueItems();
        Enum.GetValues<FileSelectionFilterDto>().Select(filter => filter.ToEntity())
            .Should().BeEquivalentTo(Enum.GetValues<FileSelectionFilter>());
        Enum.GetValues<FileSelectionSortDto>().Select(sort => sort.ToEntity())
            .Should().BeEquivalentTo(Enum.GetValues<FileSelectionSortField>());
    }
}
