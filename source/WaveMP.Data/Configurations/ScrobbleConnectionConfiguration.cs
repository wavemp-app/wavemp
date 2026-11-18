using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

/// <summary>
/// One row per user and service; the encrypted credentials are text without a length limit.
/// </summary>
internal sealed class ScrobbleConnectionConfiguration : IEntityTypeConfiguration<ScrobbleConnection>
{
    public void Configure(EntityTypeBuilder<ScrobbleConnection> builder)
    {
        builder.ToTable("scrobble_connections");
        builder.HasKey(connection => connection.Id);

        // Connections are created with an explicit UUIDv7.
        builder.Property(connection => connection.Id).ValueGeneratedNever();

        builder.Property(connection => connection.Service)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(connection => connection.Status)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(connection => connection.AccountName).HasMaxLength(LibraryColumns.TextMaxLength);

        builder.HasIndex(connection => new { connection.UserId, connection.Service }).IsUnique();

        builder.HasOne(connection => connection.User)
            .WithMany()
            .HasForeignKey(connection => connection.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
