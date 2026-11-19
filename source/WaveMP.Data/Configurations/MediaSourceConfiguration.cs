using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class MediaSourceConfiguration : IEntityTypeConfiguration<MediaSource>
{
    /// <summary>
    /// Name of the unique index on <see cref="MediaSource.NormalizedName"/>; a violation means the name is taken.
    /// </summary>
    public const string NormalizedNameIndex = "ix_media_sources_normalized_name";

    public const int NameMaxLength = 100;
    public const int PathMaxLength = 1000;

    public void Configure(EntityTypeBuilder<MediaSource> builder)
    {
        builder.ToTable("media_sources");
        builder.HasKey(source => source.Id);

        builder.Property(source => source.Id).ValueGeneratedNever();
        builder.Property(source => source.Name).HasMaxLength(NameMaxLength);
        builder.Property(source => source.NormalizedName).HasMaxLength(NameMaxLength);
        builder.Property(source => source.Path).HasMaxLength(PathMaxLength);

        builder.HasIndex(source => source.NormalizedName)
            .IsUnique()
            .HasDatabaseName(NormalizedNameIndex);
    }
}
