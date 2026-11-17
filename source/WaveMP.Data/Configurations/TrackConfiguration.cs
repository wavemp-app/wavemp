using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    /// <summary>
    /// Trigram index of the title for the fault-tolerant search (pg_trgm).
    /// </summary>
    public const string TitleTrigramIndex = "ix_tracks_title_trgm";

    public void Configure(EntityTypeBuilder<Track> builder)
    {
        builder.ToTable("tracks");
        builder.HasKey(track => track.Id);

        builder.Property(track => track.Id).ValueGeneratedNever();
        builder.Property(track => track.Title).HasMaxLength(LibraryColumns.TextMaxLength);
        builder.Property(track => track.Genre).HasMaxLength(LibraryColumns.GenreMaxLength);
        builder.Property(track => track.MetadataSource)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(track => track.CoverPriority)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);

        // One track per base name and folder, without regard to the case of the base name.
        builder.HasIndex(track => new { track.DirectoryPath, track.NormalizedBaseName }).IsUnique();
        builder.HasIndex(track => track.FirstSeenAt);

        // The review inbox counts and lists the unreviewed tracks (reviewed_at IS NULL).
        builder.HasIndex(track => track.ReviewedAt);
        builder.HasIndex(track => track.Title)
            .HasDatabaseName(TitleTrigramIndex)
            .HasMethod(LibraryColumns.TrigramIndexMethod)
            .HasOperators(LibraryColumns.TrigramOperators);

        builder.HasOne(track => track.Artist)
            .WithMany()
            .HasForeignKey(track => track.ArtistId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(track => track.Album)
            .WithMany(album => album.Tracks)
            .HasForeignKey(track => track.AlbumId)
            .OnDelete(DeleteBehavior.SetNull);

        // A deleted profile leaves its reviews with the time but without the profile.
        builder.HasOne(track => track.ReviewedBy)
            .WithMany()
            .HasForeignKey(track => track.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
