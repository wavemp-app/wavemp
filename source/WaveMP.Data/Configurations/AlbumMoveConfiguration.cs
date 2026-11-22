using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class AlbumMoveConfiguration : IEntityTypeConfiguration<AlbumMove>
{
    public const string DistinctAlbumsCheckConstraint = "ck_album_moves_distinct_albums";

    public void Configure(EntityTypeBuilder<AlbumMove> builder)
    {
        // A song that stays on its album is no move. Raw SQL, so it names the columns itself.
        builder.ToTable("album_moves", table => table.HasCheckConstraint(
            DistinctAlbumsCheckConstraint,
            "from_album_id <> to_album_id"));
        builder.HasKey(move => new { move.FromAlbumId, move.ToAlbumId });

        // The moves into an album, to pass them on when that album is left empty as well.
        builder.HasIndex(move => move.ToAlbumId);

        // A pending move means nothing once either album is gone: the database removes it with the album, also when
        // the scan or the removal of a media source deletes albums with ExecuteDelete.
        builder.HasOne(move => move.FromAlbum)
            .WithMany()
            .HasForeignKey(move => move.FromAlbumId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(move => move.ToAlbum)
            .WithMany()
            .HasForeignKey(move => move.ToAlbumId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
