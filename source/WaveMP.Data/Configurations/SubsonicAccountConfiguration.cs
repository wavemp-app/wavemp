using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

/// <summary>
/// One row per user; the encrypted password is text without a length limit, the hash of the password is unique, so an
/// API key finds at most one user.
/// </summary>
internal sealed class SubsonicAccountConfiguration : IEntityTypeConfiguration<SubsonicAccount>
{
    /// <summary>
    /// Length of a SHA-256 hash as hex.
    /// </summary>
    public const int PasswordHashLength = 64;

    public void Configure(EntityTypeBuilder<SubsonicAccount> builder)
    {
        builder.ToTable("subsonic_accounts");
        builder.HasKey(account => account.UserId);

        builder.Property(account => account.PasswordHash)
            .HasMaxLength(PasswordHashLength)
            .IsFixedLength();

        builder.HasIndex(account => account.PasswordHash).IsUnique();

        builder.HasOne(account => account.User)
            .WithOne()
            .HasForeignKey<SubsonicAccount>(account => account.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
