using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class ArtistPictureConfiguration : IEntityTypeConfiguration<ArtistPicture>
{
    public const string ProviderCheckConstraint = "ck_artist_pictures_provider";

    public const int AuthorMaxLength = 500;
    public const int LicenseMaxLength = 200;
    public const int UrlMaxLength = 2048;

    public void Configure(EntityTypeBuilder<ArtistPicture> builder)
    {
        // An online picture names its service, an uploaded one none. Raw SQL, so it names the columns and the stored
        // enum names itself.
        builder.ToTable("artist_pictures", table => table.HasCheckConstraint(
            ProviderCheckConstraint,
            $"(kind = '{nameof(ArtistPictureKind.Online)}') = (provider IS NOT NULL)"));
        builder.HasKey(picture => picture.Id);

        builder.Property(picture => picture.Id).ValueGeneratedNever();
        builder.Property(picture => picture.Kind)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(picture => picture.Provider)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(picture => picture.Author).HasMaxLength(AuthorMaxLength);
        builder.Property(picture => picture.License).HasMaxLength(LicenseMaxLength);
        builder.Property(picture => picture.LicenseUrl).HasMaxLength(UrlMaxLength);
        builder.Property(picture => picture.SourceUrl).HasMaxLength(UrlMaxLength);

        // One picture per artist and kind.
        builder.HasIndex(picture => new { picture.ArtistId, picture.Kind }).IsUnique();

        // The scan deletes artists with ExecuteDelete; the database removes their pictures itself.
        builder.HasOne(picture => picture.Artist)
            .WithMany(artist => artist.Pictures)
            .HasForeignKey(picture => picture.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
