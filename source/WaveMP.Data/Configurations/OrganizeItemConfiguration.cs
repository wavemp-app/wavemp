using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class OrganizeItemConfiguration : IEntityTypeConfiguration<OrganizeItem>
{
    /// <summary>
    /// The tag values as JSON with the field names as text, e.g. <c>{"Fields":["Album"],"Album":"Hits",...}</c>.
    /// </summary>
    private static readonly JsonSerializerOptions _tagsJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public void Configure(EntityTypeBuilder<OrganizeItem> builder)
    {
        builder.ToTable("organize_items");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Kind)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(item => item.Status)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(item => item.Conflict)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.PrimitiveCollection(item => item.Warnings)
            .ElementType(element => element.HasConversion<string>());
        builder.Property(item => item.DisplayTitle).HasMaxLength(LibraryColumns.TextMaxLength);
        builder.Property(item => item.DisplayArtist).HasMaxLength(LibraryColumns.TextMaxLength);

        // Only read and written as a whole; the database never looks into it.
        ConfigureTags(builder.Property(item => item.TagChanges));
        ConfigureTags(builder.Property(item => item.TagsBefore));

        builder.HasIndex(item => new { item.OperationId, item.Sequence });
        builder.HasIndex(item => new { item.OperationId, item.Status });
        builder.HasIndex(item => item.TrackId);

        // The scan deletes tracks with ExecuteDelete; the log keeps the item without the song.
        builder.HasOne(item => item.Track)
            .WithMany()
            .HasForeignKey(item => item.TrackId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(item => item.TagBackup)
            .WithMany()
            .HasForeignKey(item => item.TagBackupId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<OrganizeItem>()
            .WithMany()
            .HasForeignKey(item => item.UndoOfItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(item => item.Moves)
            .WithOne(move => move.Item)
            .HasForeignKey(move => move.ItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureTags(PropertyBuilder<OrganizeTagValues?> property)
    {
        property
            .HasColumnType("jsonb")
            .HasConversion(
                values => JsonSerializer.Serialize(values, _tagsJsonOptions),
                json => JsonSerializer.Deserialize<OrganizeTagValues>(json, _tagsJsonOptions),
                new ValueComparer<OrganizeTagValues?>(
                    (left, right) => JsonSerializer.Serialize(left, _tagsJsonOptions)
                        == JsonSerializer.Serialize(right, _tagsJsonOptions),
                    values => JsonSerializer.Serialize(values, _tagsJsonOptions).GetHashCode(StringComparison.Ordinal),
                    values => values == null
                        ? null
                        : JsonSerializer.Deserialize<OrganizeTagValues>(
                            JsonSerializer.Serialize(values, _tagsJsonOptions),
                            _tagsJsonOptions)));
    }
}
