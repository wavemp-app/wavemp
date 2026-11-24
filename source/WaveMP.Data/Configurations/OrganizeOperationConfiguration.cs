using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class OrganizeOperationConfiguration : IEntityTypeConfiguration<OrganizeOperation>
{
    public const int TemplateMaxLength = 512;

    public void Configure(EntityTypeBuilder<OrganizeOperation> builder)
    {
        builder.ToTable("organize_operations");
        builder.HasKey(operation => operation.Id);

        // Operations are created with an explicit UUIDv7.
        builder.Property(operation => operation.Id).ValueGeneratedNever();
        builder.Property(operation => operation.Kind)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(operation => operation.Status)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(operation => operation.ErrorCode)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(operation => operation.Template).HasMaxLength(TemplateMaxLength);
        builder.Property(operation => operation.DisplayTitle).HasMaxLength(LibraryColumns.TextMaxLength);

        builder.HasIndex(operation => operation.Status);
        builder.HasIndex(operation => operation.CreatedAt).IsDescending();

        builder.HasOne(operation => operation.OwnAlbum)
            .WithMany()
            .HasForeignKey(operation => operation.OwnAlbumId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(operation => operation.UndoOf)
            .WithMany()
            .HasForeignKey(operation => operation.UndoOfId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<OrganizeOperation>()
            .WithMany()
            .HasForeignKey(operation => operation.UndoneById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<MediaSource>()
            .WithMany()
            .HasForeignKey(operation => operation.SourceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(operation => operation.CreatedBy)
            .WithMany()
            .HasForeignKey(operation => operation.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(operation => operation.Items)
            .WithOne(item => item.Operation)
            .HasForeignKey(item => item.OperationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
