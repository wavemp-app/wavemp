using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class AlbumArtistCreditConfiguration : IEntityTypeConfiguration<AlbumArtistCredit>
{
    public void Configure(EntityTypeBuilder<AlbumArtistCredit> builder)
    {
        builder.ToTable("album_artist_credits");
        builder.HasKey(credit => new { credit.AlbumId, credit.ArtistId });

        // The albums of an artist.
        builder.HasIndex(credit => credit.ArtistId);

        // A removed album or artist takes its credits with it; an artist is only removed once nothing refers to it.
        builder.HasOne(credit => credit.Album)
            .WithMany(album => album.ArtistCredits)
            .HasForeignKey(credit => credit.AlbumId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(credit => credit.Artist)
            .WithMany()
            .HasForeignKey(credit => credit.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
