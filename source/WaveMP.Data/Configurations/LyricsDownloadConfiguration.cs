using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

/// <summary>
/// One row per looked up track; the lyrics are text without a length limit.
/// </summary>
internal sealed class LyricsDownloadConfiguration : IEntityTypeConfiguration<LyricsDownload>
{
    public void Configure(EntityTypeBuilder<LyricsDownload> builder)
    {
        builder.ToTable("lyrics_downloads");
        builder.HasKey(download => download.TrackId);

        builder.Property(download => download.Status)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);

        // The scan deletes tracks with ExecuteDelete; the database removes their results itself.
        builder.HasOne(download => download.Track)
            .WithOne()
            .HasForeignKey<LyricsDownload>(download => download.TrackId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
