using AwesomeAssertions;
using WaveMP.Core.Organizing.Operations;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public class OrganizeOperationMappingExtensionsTests
{
    private static readonly DateTimeOffset _created = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The enums are mapped by name, so every member needs a counterpart of the same name in both directions.
    /// </summary>
    [TestCase(typeof(OrganizeOperationKind), typeof(OrganizeOperationKindDto))]
    [TestCase(typeof(OrganizeOperationStatus), typeof(OrganizeOperationStatusDto))]
    [TestCase(typeof(OrganizeErrorCode), typeof(OrganizeErrorCodeDto))]
    [TestCase(typeof(OrganizeItemKind), typeof(OrganizeItemKindDto))]
    [TestCase(typeof(OrganizeItemStatus), typeof(OrganizeItemStatusDto))]
    [TestCase(typeof(OrganizeConflict), typeof(OrganizeConflictDto))]
    [TestCase(typeof(OrganizeWarning), typeof(OrganizeWarningDto))]
    [TestCase(typeof(OrganizeFileKind), typeof(OrganizeFileKindDto))]
    [TestCase(typeof(OrganizeMoveStatus), typeof(OrganizeMoveStatusDto))]
    [TestCase(typeof(OrganizeTagField), typeof(OrganizeTagFieldDto))]
    [TestCase(typeof(OrganizeItemFilter), typeof(OrganizeItemFilterDto))]
    [TestCase(typeof(SourceAvailabilityStatus), typeof(SourceAvailabilityStatusDto))]
    public void Enums_AndTheirDtos_HaveTheSameMembers(Type entity, Type dto)
    {
        Enum.GetNames(dto).Should().BeEquivalentTo(Enum.GetNames(entity));
    }

    [Test]
    public void ToDto_FinishedOperation_CanBeUndoneOnlyIfItChangedFilesAndWasNotUndone()
    {
        OrganizeOperation operation = new()
        {
            Id = Guid.NewGuid(),
            Kind = OrganizeOperationKind.Library,
            Status = OrganizeOperationStatus.Stopped,
            Template = "{Title}",
            ErrorCode = OrganizeErrorCode.ItemFailed,
            DoneItems = 2,
            FailedItems = 1,
            TotalItems = 5,
            CreatedAt = _created,
        };

        OrganizeOperationDto dto = operation.ToDto();

        (dto.Kind, dto.Status, dto.ErrorCode, dto.CanUndo)
            .Should().Be((OrganizeOperationKindDto.Library, OrganizeOperationStatusDto.Stopped, OrganizeErrorCodeDto.ItemFailed, true));
        operation.UndoneById = Guid.NewGuid();
        operation.ToDto().CanUndo.Should().BeFalse();
        operation.UndoneById = null;
        operation.DoneItems = 0;
        operation.ToDto().CanUndo.Should().BeFalse();
        operation.DoneItems = 1;
        operation.Status = OrganizeOperationStatus.Previewed;
        operation.ToDto().CanUndo.Should().BeFalse();
    }

    [Test]
    public void ToDto_Item_CarriesItsMovesInOrderWithConflictAndWarnings()
    {
        OrganizeItem item = new()
        {
            Id = Guid.NewGuid(),
            OperationId = Guid.NewGuid(),
            Sequence = 3,
            Kind = OrganizeItemKind.Track,
            Status = OrganizeItemStatus.Conflict,
            SourceId = Guid.NewGuid(),
            FromDirectoryPath = "In",
            FromBaseName = "a",
            ToDirectoryPath = "Band/Record",
            ToBaseName = "01 A",
            Conflict = OrganizeConflict.TargetExists,
            Warnings = [OrganizeWarning.MetadataChanges],
            DisplayTitle = "A",
            TagChanges = new OrganizeTagValues { Fields = [OrganizeTagField.Album], Album = "Record" },
        };
        foreach (int sequence in new[] { 1, 0 })
        {
            item.Moves.Add(new OrganizeFileMove
            {
                Id = Guid.NewGuid(),
                ItemId = item.Id,
                Sequence = sequence,
                SourceId = item.SourceId,
                FileKind = sequence == 0 ? OrganizeFileKind.Audio : OrganizeFileKind.Lyrics,
                FromPath = "In/a." + (sequence == 0 ? "mp3" : "lrc"),
                ToPath = "Band/Record/01 A." + (sequence == 0 ? "mp3" : "lrc"),
                Size = 100,
                ModifiedAt = _created,
                Status = OrganizeMoveStatus.Pending,
            });
        }

        OrganizeItemDto dto = item.ToDto();

        dto.Conflict.Should().Be(OrganizeConflictDto.TargetExists);
        dto.Warnings.Should().Equal(OrganizeWarningDto.MetadataChanges);
        dto.Moves.Select(move => move.FileKind).Should().Equal(OrganizeFileKindDto.Audio, OrganizeFileKindDto.Lyrics);
        dto.TagChanges!.Fields.Should().Equal(OrganizeTagFieldDto.Album);
        dto.TagsBefore.Should().BeNull();
    }

    [Test]
    public void ToDto_Availability_IsAvailableWithOneWritableSource()
    {
        OrganizeAvailability availability = new(
            [
                new SourceAvailability(Guid.NewGuid(), "Music", SourceAvailabilityStatus.ReadOnly),
                new SourceAvailability(Guid.NewGuid(), "Videos", SourceAvailabilityStatus.Available),
            ],
            null);

        OrganizeAvailabilityDto dto = availability.ToDto();

        dto.IsAvailable.Should().BeTrue();
        dto.Sources.Select(source => source.Status)
            .Should().Equal(SourceAvailabilityStatusDto.ReadOnly, SourceAvailabilityStatusDto.Available);
        (availability with { Sources = [availability.Sources[0]] }).ToDto().IsAvailable.Should().BeFalse();
    }
}
