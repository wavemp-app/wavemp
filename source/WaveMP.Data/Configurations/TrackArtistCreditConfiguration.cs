using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class TrackArtistCreditConfiguration : IEntityTypeConfiguration<TrackArtistCredit>
{
    public void Configure(EntityTypeBuilder<TrackArtistCredit> builder)
    {
        builder.ToTable("track_artist_credits");
        builder.HasKey(credit => new { credit.TrackId, credit.ArtistId });

        builder.Property(credit => credit.Role)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);

        // The songs of an artist.
        builder.HasIndex(credit => credit.ArtistId);

        // A removed song or artist takes its credits with it; an artist is only removed once nothing refers to it.
        builder.HasOne(credit => credit.Track)
            .WithMany(track => track.ArtistCredits)
            .HasForeignKey(credit => credit.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(credit => credit.Artist)
            .WithMany()
            .HasForeignKey(credit => credit.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
