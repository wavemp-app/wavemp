using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;
using WaveMP.Data.Texts;

namespace WaveMP.Data.Configurations;

internal sealed class AlbumConfiguration : IEntityTypeConfiguration<Album>
{
    /// <summary>
    /// Trigram index of the title for the fault-tolerant search (pg_trgm).
    /// </summary>
    public const string TitleTrigramIndex = "ix_albums_title_trgm";

    /// <summary>
    /// Trigram index of the search key (see <see cref="Texts.SearchKeys"/>) for the fault-tolerant search (pg_trgm).
    /// </summary>
    public const string SearchKeyTrigramIndex = "ix_albums_search_key_trgm";

    public void Configure(EntityTypeBuilder<Album> builder)
    {
        builder.ToTable("albums");
        builder.HasKey(album => album.Id);

        builder.Property(album => album.Id).ValueGeneratedNever();
        builder.Property(album => album.Title).HasMaxLength(LibraryColumns.TextMaxLength);
        builder.Property(album => album.NormalizedTitle).HasMaxLength(LibraryColumns.TextMaxLength);
        builder.Property(album => album.SearchKey).HasMaxLength(SearchKeys.MaxLength);
        builder.Property(album => album.AlbumKey).HasMaxLength(LibraryColumns.AlbumKeyLength);
        builder.Property(album => album.CoverPriority)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(album => album.CoverFallback)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);

        builder.HasIndex(album => album.AlbumKey).IsUnique();
        builder.HasIndex(album => album.AlbumArtistId);
        builder.HasIndex(album => album.MusicBrainzReleaseId);
        builder.HasIndex(album => album.Title)
            .HasDatabaseName(TitleTrigramIndex)
            .HasMethod(LibraryColumns.TrigramIndexMethod)
            .HasOperators(LibraryColumns.TrigramOperators);
        builder.HasIndex(album => album.SearchKey)
            .HasDatabaseName(SearchKeyTrigramIndex)
            .HasMethod(LibraryColumns.TrigramIndexMethod)
            .HasOperators(LibraryColumns.TrigramOperators);

        builder.HasOne(album => album.AlbumArtist)
            .WithMany()
            .HasForeignKey(album => album.AlbumArtistId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
