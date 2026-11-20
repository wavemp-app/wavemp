using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class RecapShareConfiguration : IEntityTypeConfiguration<RecapShare>
{
    public void Configure(EntityTypeBuilder<RecapShare> builder)
    {
        builder.ToTable("recap_shares");
        builder.HasKey(share => share.Id);

        // Shares are created with an explicit UUIDv7.
        builder.Property(share => share.Id).ValueGeneratedNever();
        builder.Property(share => share.Period)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(share => share.TimeZone).HasMaxLength(RecapShare.TimeZoneMaxLength);
        builder.Property(share => share.Design).HasMaxLength(RecapShare.DesignMaxLength);

        // A user shares each period once; a share from the first play on has no first day, which counts as equal.
        builder.HasIndex(share => new { share.UserId, share.Period, share.FromDate, share.ToDate, share.TimeZone })
            .IsUnique()
            .AreNullsDistinct(false);

        // The shares of all users, the newest first.
        builder.HasIndex(share => new { share.UpdatedAt, share.Id }).IsDescending(true, true);

        builder.HasOne(share => share.User)
            .WithMany()
            .HasForeignKey(share => share.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
