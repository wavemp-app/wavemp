using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class OwnAlbumConfiguration : IEntityTypeConfiguration<OwnAlbum>
{
    public const int FileNameMaxLength = 128;

    public void Configure(EntityTypeBuilder<OwnAlbum> builder)
    {
        builder.ToTable("own_albums");
        builder.HasKey(album => album.Id);

        // Own albums are created with an explicit UUIDv7.
        builder.Property(album => album.Id).ValueGeneratedNever();
        builder.Property(album => album.Title).HasMaxLength(LibraryColumns.TextMaxLength);
        builder.Property(album => album.NormalizedTitle).HasMaxLength(LibraryColumns.TextMaxLength);
        builder.Property(album => album.AlbumArtist).HasMaxLength(LibraryColumns.TextMaxLength);
        builder.Property(album => album.Genre).HasMaxLength(LibraryColumns.GenreMaxLength);
        builder.Property(album => album.CoverFileName).HasMaxLength(FileNameMaxLength);
        builder.Property(album => album.CoverContentType).HasMaxLength(LibraryColumns.EnumMaxLength);

        builder.HasIndex(album => new { album.NormalizedTitle, album.Id });

        builder.HasOne(album => album.CreatedBy)
            .WithMany()
            .HasForeignKey(album => album.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(album => album.Tracks)
            .WithOne(track => track.OwnAlbum)
            .HasForeignKey(track => track.OwnAlbumId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
