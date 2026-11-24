using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class OwnAlbumTrackConfiguration : IEntityTypeConfiguration<OwnAlbumTrack>
{
    public void Configure(EntityTypeBuilder<OwnAlbumTrack> builder)
    {
        builder.ToTable("own_album_tracks");
        builder.HasKey(track => new { track.OwnAlbumId, track.TrackId });

        builder.Property(track => track.State)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);

        // A song is a member of at most one own album; removed songs do not count.
        builder.HasIndex(track => track.TrackId)
            .IsUnique()
            .HasFilter("state = 'Member'");

        // The scan deletes tracks with ExecuteDelete; the database removes their rows itself.
        builder.HasOne(track => track.Track)
            .WithMany()
            .HasForeignKey(track => track.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(track => track.OriginalTagBackup)
            .WithMany()
            .HasForeignKey(track => track.OriginalTagBackupId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
