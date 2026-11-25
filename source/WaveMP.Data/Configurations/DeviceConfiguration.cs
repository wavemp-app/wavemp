using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        // Raw SQL registers devices, so it names the columns itself.
        builder.ToTable("devices");
        builder.HasKey(device => new { device.UserId, device.DeviceId });

        builder.Property(device => device.DeviceId).HasMaxLength(Device.IdMaxLength);
        builder.Property(device => device.ReportedName).HasMaxLength(Device.NameMaxLength);
        builder.Property(device => device.CustomName).HasMaxLength(Device.NameMaxLength);
        builder.Property(device => device.Type)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);

        builder.HasOne(device => device.User)
            .WithMany()
            .HasForeignKey(device => device.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
