using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class OnlineCoverConfiguration : IEntityTypeConfiguration<OnlineCover>
{
    public const string OwnerCheckConstraint = "ck_online_covers_owner";

    public void Configure(EntityTypeBuilder<OnlineCover> builder)
    {
        // Like covers: an album or a track, never both. Raw SQL, so it names the columns itself.
        builder.ToTable("online_covers", table => table.HasCheckConstraint(
            OwnerCheckConstraint,
            "(album_id IS NULL) <> (track_id IS NULL)"));
        builder.HasKey(cover => cover.Id);

        builder.Property(cover => cover.Id).ValueGeneratedNever();
        builder.Property(cover => cover.FileName).HasMaxLength(LibraryColumns.TechnicalMaxLength);
        builder.Property(cover => cover.MimeType).HasMaxLength(LibraryColumns.TechnicalMaxLength);

        // One online cover per album and per track; the unique indexes allow many rows without the owner (NULL).
        builder.HasIndex(cover => cover.AlbumId).IsUnique();
        builder.HasIndex(cover => cover.TrackId).IsUnique();

        // The scan deletes albums and tracks with ExecuteDelete; the database removes their online covers itself.
        builder.HasOne(cover => cover.Album)
            .WithMany()
            .HasForeignKey(cover => cover.AlbumId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cover => cover.Track)
            .WithMany()
            .HasForeignKey(cover => cover.TrackId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
