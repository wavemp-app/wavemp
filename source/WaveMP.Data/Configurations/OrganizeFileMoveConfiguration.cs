using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class OrganizeFileMoveConfiguration : IEntityTypeConfiguration<OrganizeFileMove>
{
    public void Configure(EntityTypeBuilder<OrganizeFileMove> builder)
    {
        builder.ToTable("organize_file_moves");
        builder.HasKey(move => move.Id);

        builder.Property(move => move.Id).ValueGeneratedNever();
        builder.Property(move => move.FileKind)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(move => move.Status)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);

        builder.HasIndex(move => new { move.ItemId, move.Sequence });
        builder.HasIndex(move => move.MediaFileId);

        // The scan deletes files with ExecuteDelete; the log keeps the move with its paths.
        builder.HasOne(move => move.MediaFile)
            .WithMany()
            .HasForeignKey(move => move.MediaFileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
