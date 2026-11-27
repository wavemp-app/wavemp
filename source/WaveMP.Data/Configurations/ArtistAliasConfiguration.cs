using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;
using WaveMP.Data.Texts;

namespace WaveMP.Data.Configurations;

internal sealed class ArtistAliasConfiguration : IEntityTypeConfiguration<ArtistAlias>
{
    /// <summary>
    /// Trigram index of the alias for the fault-tolerant search (pg_trgm).
    /// </summary>
    public const string NameTrigramIndex = "ix_artist_aliases_name_trgm";

    /// <summary>
    /// Trigram index of the search key of the alias (pg_trgm).
    /// </summary>
    public const string SearchKeyTrigramIndex = "ix_artist_aliases_search_key_trgm";

    public void Configure(EntityTypeBuilder<ArtistAlias> builder)
    {
        builder.ToTable("artist_aliases");
        builder.HasKey(alias => alias.Id);

        builder.Property(alias => alias.Id).ValueGeneratedNever();
        builder.Property(alias => alias.Name).HasMaxLength(LibraryColumns.TextMaxLength);
        builder.Property(alias => alias.NormalizedName).HasMaxLength(LibraryColumns.TextMaxLength);
        builder.Property(alias => alias.SearchKey).HasMaxLength(SearchKeys.MaxLength);
        builder.Property(alias => alias.Source)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);

        // Every normalized name once per artist.
        builder.HasIndex(alias => new { alias.ArtistId, alias.NormalizedName }).IsUnique();
        builder.HasIndex(alias => alias.Name)
            .HasDatabaseName(NameTrigramIndex)
            .HasMethod(LibraryColumns.TrigramIndexMethod)
            .HasOperators(LibraryColumns.TrigramOperators);
        builder.HasIndex(alias => alias.SearchKey)
            .HasDatabaseName(SearchKeyTrigramIndex)
            .HasMethod(LibraryColumns.TrigramIndexMethod)
            .HasOperators(LibraryColumns.TrigramOperators);

        // The scan deletes artists with ExecuteDelete; the database removes their aliases itself.
        builder.HasOne(alias => alias.Artist)
            .WithMany(artist => artist.Aliases)
            .HasForeignKey(alias => alias.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
