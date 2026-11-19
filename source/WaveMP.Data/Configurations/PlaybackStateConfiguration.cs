using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class PlaybackStateConfiguration : IEntityTypeConfiguration<PlaybackState>
{
    public void Configure(EntityTypeBuilder<PlaybackState> builder)
    {
        // One row per user; raw SQL saves it, so it names the columns itself.
        builder.ToTable("playback_states");
        builder.HasKey(state => state.UserId);

        // The tracks are kept as ids without foreign keys: a scan may remove a track while it waits in the queue,
        // and reading the state skips it then.
        builder.Property(state => state.TrackIds).HasColumnType("uuid[]");
        builder.Property(state => state.AutoplayPositions).HasColumnType("integer[]");
        builder.Property(state => state.OriginalOrder).HasColumnType("integer[]");
        builder.Property(state => state.Repeat)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(state => state.SourceKind)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(state => state.DeviceId).HasMaxLength(PlaybackState.DeviceIdMaxLength);
        builder.Property(state => state.DeviceName).HasMaxLength(PlaybackState.DeviceNameMaxLength);

        builder.HasOne(state => state.User)
            .WithOne()
            .HasForeignKey<PlaybackState>(state => state.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
