using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class LibraryCheckConfiguration : IEntityTypeConfiguration<LibraryCheck>
{
    public void Configure(EntityTypeBuilder<LibraryCheck> builder)
    {
        builder.ToTable("library_checks");
        builder.HasKey(check => check.Id);

        // The only row has a fixed id.
        builder.Property(check => check.Id).ValueGeneratedNever();
    }
}
