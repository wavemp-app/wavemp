using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class ArtistMoveConfiguration : IEntityTypeConfiguration<ArtistMove>
{
    public const string DistinctArtistsCheckConstraint = "ck_artist_moves_distinct_artists";

    public void Configure(EntityTypeBuilder<ArtistMove> builder)
    {
        // A song that stays with its artist is no move. Raw SQL, so it names the columns itself.
        builder.ToTable("artist_moves", table => table.HasCheckConstraint(
            DistinctArtistsCheckConstraint,
            "from_artist_id <> to_artist_id"));
        builder.HasKey(move => new { move.FromArtistId, move.ToArtistId });

        // The moves into an artist, to pass them on when that artist is left empty as well.
        builder.HasIndex(move => move.ToArtistId);

        // A pending move means nothing once either artist is gone: the database removes it with the artist, also when
        // the scan or the removal of a media source deletes artists with ExecuteDelete.
        builder.HasOne(move => move.FromArtist)
            .WithMany()
            .HasForeignKey(move => move.FromArtistId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(move => move.ToArtist)
            .WithMany()
            .HasForeignKey(move => move.ToArtistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
