using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

/// <summary>
/// One row per analyzed media file, keyed by the file.
/// </summary>
internal sealed class MediaFileAnalysisConfiguration : IEntityTypeConfiguration<MediaFileAnalysis>
{
    private static readonly JsonSerializerOptions _piecesJsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<MediaFileAnalysis> builder)
    {
        builder.ToTable("media_file_analyses");
        builder.HasKey(analysis => analysis.MediaFileId);

        builder.Property(analysis => analysis.Status)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(analysis => analysis.Method)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);

        // Only read and written as a whole; the database never looks into them.
        builder.Property(analysis => analysis.Pieces)
            .HasColumnType("jsonb")
            .HasConversion(
                pieces => JsonSerializer.Serialize(pieces, _piecesJsonOptions),
                json => JsonSerializer.Deserialize<List<SongPiece>>(json, _piecesJsonOptions) ?? new(),
                new ValueComparer<List<SongPiece>>(
                    (left, right) => left!.SequenceEqual(right!),
                    pieces => pieces.Aggregate(0, (hash, piece) => HashCode.Combine(hash, piece)),
                    pieces => pieces.ToList()));

        // The scan deletes files with ExecuteDelete; the database removes their analysis itself.
        builder.HasOne(analysis => analysis.MediaFile)
            .WithOne(file => file.Analysis)
            .HasForeignKey<MediaFileAnalysis>(analysis => analysis.MediaFileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
