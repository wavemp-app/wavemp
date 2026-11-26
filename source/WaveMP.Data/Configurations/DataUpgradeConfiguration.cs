using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class DataUpgradeConfiguration : IEntityTypeConfiguration<DataUpgrade>
{
    public const int NameMaxLength = 64;

    public void Configure(EntityTypeBuilder<DataUpgrade> builder)
    {
        builder.ToTable("data_upgrades");
        builder.HasKey(upgrade => upgrade.Name);

        builder.Property(upgrade => upgrade.Name).HasMaxLength(NameMaxLength);
    }
}
