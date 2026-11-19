using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Data.Configurations;

internal sealed class LibraryIssueConfiguration : IEntityTypeConfiguration<LibraryIssue>
{
    /// <summary>
    /// The missing tags as JSON array of their names, e.g. <c>["Artist","Album"]</c>.
    /// </summary>
    private static readonly JsonSerializerOptions _tagsJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public void Configure(EntityTypeBuilder<LibraryIssue> builder)
    {
        builder.ToTable("library_issues");
        builder.HasKey(issue => issue.Id);

        // Problems are created with an explicit UUIDv7.
        builder.Property(issue => issue.Id).ValueGeneratedNever();
        builder.Property(issue => issue.Type)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(issue => issue.FileIssue)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);

        // Only read and written as a whole; the database never looks into it.
        builder.Property(issue => issue.MissingTags)
            .HasColumnType("jsonb")
            .HasConversion(
                tags => JsonSerializer.Serialize(tags, _tagsJsonOptions),
                json => JsonSerializer.Deserialize<List<MetadataTag>>(json, _tagsJsonOptions) ?? new(),
                new ValueComparer<List<MetadataTag>>(
                    (left, right) => left!.SequenceEqual(right!),
                    tags => tags.Aggregate(0, (hash, tag) => HashCode.Combine(hash, tag)),
                    tags => tags.ToList()));

        // The overview counts and filters by type.
        builder.HasIndex(issue => issue.Type);

        // The scan deletes tracks and files with ExecuteDelete; the database removes their problems itself.
        builder.HasOne(issue => issue.Track)
            .WithMany()
            .HasForeignKey(issue => issue.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(issue => issue.MediaFile)
            .WithMany()
            .HasForeignKey(issue => issue.MediaFileId)
            .OnDelete(DeleteBehavior.Cascade);

        // A duplicate stays a problem of its own track when the other track is removed, until the next run.
        builder.HasOne(issue => issue.RelatedTrack)
            .WithMany()
            .HasForeignKey(issue => issue.RelatedTrackId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
