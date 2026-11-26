using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    /// <summary>
    /// Trigram index of the name for the fault-tolerant search (pg_trgm).
    /// </summary>
    public const string NameTrigramIndex = "ix_genres_name_trgm";

    public void Configure(EntityTypeBuilder<Genre> builder)
    {
        builder.ToTable("genres");
        builder.HasKey(genre => genre.Id);

        builder.Property(genre => genre.Id).ValueGeneratedNever();
        builder.Property(genre => genre.Name).HasMaxLength(LibraryColumns.GenreMaxLength);
        builder.Property(genre => genre.NormalizedName).HasMaxLength(LibraryColumns.GenreMaxLength);

        builder.HasIndex(genre => genre.NormalizedName).IsUnique();
        builder.HasIndex(genre => genre.Name)
            .HasDatabaseName(NameTrigramIndex)
            .HasMethod(LibraryColumns.TrigramIndexMethod)
            .HasOperators(LibraryColumns.TrigramOperators);
    }
}
