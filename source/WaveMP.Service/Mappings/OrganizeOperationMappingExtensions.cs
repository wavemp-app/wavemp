using WaveMP.Core.Organizing.Operations;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

/// <summary>
/// Maps the organize operations. The entity enums and their DTOs have members of the same names, so they are mapped
/// by name; a test checks every member.
/// </summary>
public static class OrganizeOperationMappingExtensions
{
    public static OrganizeOperationDto ToDto(this OrganizeOperation operation)
    {
        return new OrganizeOperationDto
        {
            Id = operation.Id,
            Kind = MapEnum<OrganizeOperationKind, OrganizeOperationKindDto>(operation.Kind),
            Status = MapEnum<OrganizeOperationStatus, OrganizeOperationStatusDto>(operation.Status),
            OwnAlbumId = operation.OwnAlbumId,
            SourceId = operation.SourceId,
            UndoOfId = operation.UndoOfId,
            UndoneById = operation.UndoneById,
            Template = operation.Template,
            DisplayTitle = operation.DisplayTitle,
            ErrorCode = operation.ErrorCode is { } code ? MapEnum<OrganizeErrorCode, OrganizeErrorCodeDto>(code) : null,
            SkipConflicts = operation.SkipConflicts,
            TotalItems = operation.TotalItems,
            DoneItems = operation.DoneItems,
            SkippedItems = operation.SkippedItems,
            FailedItems = operation.FailedItems,
            ConflictItems = operation.ConflictItems,
            WarningItems = operation.WarningItems,
            CanUndo = operation.UndoneById is null
                && operation.Kind != OrganizeOperationKind.Undo
                && operation.DoneItems > 0
                && operation.Status is OrganizeOperationStatus.Completed or OrganizeOperationStatus.Stopped
                    or OrganizeOperationStatus.Canceled or OrganizeOperationStatus.Interrupted,
            CreatedAt = operation.CreatedAt,
            PreviewedAt = operation.PreviewedAt,
            ExpiresAt = operation.ExpiresAt,
            StartedAt = operation.StartedAt,
            FinishedAt = operation.FinishedAt,
        };
    }

    public static OrganizeItemDto ToDto(this OrganizeItem item)
    {
        return new OrganizeItemDto
        {
            Id = item.Id,
            Sequence = item.Sequence,
            Kind = MapEnum<OrganizeItemKind, OrganizeItemKindDto>(item.Kind),
            Status = MapEnum<OrganizeItemStatus, OrganizeItemStatusDto>(item.Status),
            TrackId = item.TrackId,
            SourceId = item.SourceId,
            FromDirectoryPath = item.FromDirectoryPath,
            FromBaseName = item.FromBaseName,
            ToDirectoryPath = item.ToDirectoryPath,
            ToBaseName = item.ToBaseName,
            Conflict = item.Conflict is { } conflict ? MapEnum<OrganizeConflict, OrganizeConflictDto>(conflict) : null,
            Warnings = [.. item.Warnings.Select(MapEnum<OrganizeWarning, OrganizeWarningDto>)],
            DisplayTitle = item.DisplayTitle,
            DisplayArtist = item.DisplayArtist,
            TagChanges = item.TagChanges?.ToDto(),
            TagsBefore = item.TagsBefore?.ToDto(),
            Moves = [.. item.Moves.OrderBy(move => move.Sequence).Select(move => new OrganizeFileMoveDto
            {
                Id = move.Id,
                FileKind = MapEnum<OrganizeFileKind, OrganizeFileKindDto>(move.FileKind),
                FromPath = move.FromPath,
                ToPath = move.ToPath,
                Status = MapEnum<OrganizeMoveStatus, OrganizeMoveStatusDto>(move.Status),
            })],
        };
    }

    public static OrganizeTagValuesDto ToDto(this OrganizeTagValues values)
    {
        return new OrganizeTagValuesDto
        {
            Fields = [.. values.Fields.Select(MapEnum<OrganizeTagField, OrganizeTagFieldDto>)],
            Album = values.Album,
            AlbumArtists = values.AlbumArtists,
            TrackNumber = values.TrackNumber,
            TrackCount = values.TrackCount,
            DiscNumber = values.DiscNumber,
            Year = values.Year,
            Genres = values.Genres,
        };
    }

    public static OrganizeAvailabilityDto ToDto(this OrganizeAvailability availability)
    {
        return new OrganizeAvailabilityDto
        {
            Sources = [.. availability.Sources.Select(source => new SourceAvailabilityDto
            {
                SourceId = source.SourceId,
                Name = source.Name,
                Status = MapEnum<SourceAvailabilityStatus, SourceAvailabilityStatusDto>(source.Status),
            })],
            IsAvailable = availability.Sources.Any(source => source.Status == SourceAvailabilityStatus.Available),
            ActiveOperationId = availability.ActiveOperationId,
        };
    }

    public static OrganizeOperationKind ToEntity(this OrganizeOperationKindDto kind)
    {
        return MapEnum<OrganizeOperationKindDto, OrganizeOperationKind>(kind);
    }

    public static OrganizeOperationStatus ToEntity(this OrganizeOperationStatusDto status)
    {
        return MapEnum<OrganizeOperationStatusDto, OrganizeOperationStatus>(status);
    }

    public static OrganizeItemFilter ToEntity(this OrganizeItemFilterDto filter)
    {
        return MapEnum<OrganizeItemFilterDto, OrganizeItemFilter>(filter);
    }

    /// <summary>
    /// The member of <typeparamref name="TTo"/> with the name of <paramref name="value"/>.
    /// </summary>
    internal static TTo MapEnum<TFrom, TTo>(TFrom value)
        where TFrom : struct, Enum
        where TTo : struct, Enum
    {
        return Enum.Parse<TTo>(value.ToString());
    }
}
