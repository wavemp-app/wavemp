using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class SmartPlaylistConfiguration : IEntityTypeConfiguration<SmartPlaylist>
{
    /// <summary>
    /// The rules as JSON: members in camel case, enums by their names, values that are not set left out, e.g.
    /// <c>[{"field":"Rating","operator":"AtLeast","number":4}]</c>.
    /// </summary>
    internal static readonly JsonSerializerOptions RulesJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public void Configure(EntityTypeBuilder<SmartPlaylist> builder)
    {
        builder.ToTable("smart_playlists");
        builder.HasKey(playlist => playlist.Id);

        // Smart playlists are created with an explicit UUIDv7.
        builder.Property(playlist => playlist.Id).ValueGeneratedNever();
        builder.Property(playlist => playlist.Name).HasMaxLength(PlaylistConfiguration.NameMaxLength);
        builder.Property(playlist => playlist.NormalizedName).HasMaxLength(PlaylistConfiguration.NameMaxLength);
        builder.Property(playlist => playlist.Match)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(playlist => playlist.Sort)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);
        builder.Property(playlist => playlist.Direction)
            .HasConversion<string>()
            .HasMaxLength(LibraryColumns.EnumMaxLength);

        // The rules are only read and written as a whole; the database never looks into them.
        builder.Property(playlist => playlist.Rules)
            .HasColumnType("jsonb")
            .HasConversion(
                rules => JsonSerializer.Serialize(rules, RulesJsonOptions),
                json => JsonSerializer.Deserialize<List<SmartPlaylistRule>>(json, RulesJsonOptions) ?? new(),
                new ValueComparer<List<SmartPlaylistRule>>(
                    (left, right) => left!.SequenceEqual(right!),
                    rules => rules.Aggregate(0, (hash, rule) => HashCode.Combine(hash, rule.GetHashCode())),
                    rules => rules.ToList()));

        // The smart playlists of a user by name.
        builder.HasIndex(playlist => new { playlist.UserId, playlist.NormalizedName, playlist.Id });

        builder.HasOne(playlist => playlist.User)
            .WithMany()
            .HasForeignKey(playlist => playlist.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
