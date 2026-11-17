using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class TrackFileSelectionConfiguration : IEntityTypeConfiguration<TrackFileSelection>
{
    public void Configure(EntityTypeBuilder<TrackFileSelection> builder)
    {
        // A track without selection in a category has no row for it.
        builder.ToTable("track_file_selections");
        builder.HasKey(selection => new { selection.TrackId, selection.Category });

        builder.Property(selection => selection.Category)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);

        // The scan deletes tracks with ExecuteDelete; the database removes their selections itself.
        builder.HasOne(selection => selection.Track)
            .WithMany(track => track.FileSelections)
            .HasForeignKey(selection => selection.TrackId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
