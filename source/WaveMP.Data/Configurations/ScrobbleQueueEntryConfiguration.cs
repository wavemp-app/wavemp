using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class ScrobbleQueueEntryConfiguration : IEntityTypeConfiguration<ScrobbleQueueEntry>
{
    public void Configure(EntityTypeBuilder<ScrobbleQueueEntry> builder)
    {
        builder.ToTable("scrobble_queue");
        builder.HasKey(entry => entry.Id);

        // Entries are created with an explicit UUIDv7.
        builder.Property(entry => entry.Id).ValueGeneratedNever();

        builder.Property(entry => entry.Artist).HasMaxLength(LibraryColumns.TextMaxLength);
        builder.Property(entry => entry.Title).HasMaxLength(LibraryColumns.TextMaxLength);
        builder.Property(entry => entry.Album).HasMaxLength(LibraryColumns.TextMaxLength);
        builder.Property(entry => entry.AlbumArtist).HasMaxLength(LibraryColumns.TextMaxLength);

        // The entries of a connection in the order they were played; the due entries of all connections.
        builder.HasIndex(entry => new { entry.ConnectionId, entry.PlayedAt });
        builder.HasIndex(entry => entry.NextAttemptAt);

        builder.HasOne(entry => entry.Connection)
            .WithMany()
            .HasForeignKey(entry => entry.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
