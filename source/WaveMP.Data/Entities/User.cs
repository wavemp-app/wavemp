namespace WaveMP.Data.Entities;

/// <summary>
/// A profile of WaveMP. Profiles are selected without a password; the built-in admin profile is created on the first
/// start and can be neither deleted nor demoted.
/// </summary>
public class User
{
    public Guid Id { get; set; }

    /// <summary>
    /// Display name, unique without regard to case (see <see cref="NormalizedName"/>).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Upper-case form of <see cref="Name"/>; carries the unique index.
    /// </summary>
    public required string NormalizedName { get; set; }

    /// <summary>
    /// Short form of the name shown on the profile tile, by default derived from the name.
    /// </summary>
    public required string Initials { get; set; }

    /// <summary>
    /// Color of the profile as <c>#rrggbb</c> in lower case.
    /// </summary>
    public required string Color { get; set; }

    public required UserRole Role { get; set; }

    /// <summary>
    /// <c>true</c> for the built-in admin profile. A filtered unique index allows only one.
    /// </summary>
    public required bool IsBuiltIn { get; set; }

    /// <summary>
    /// Hash of the optional PIN, never the PIN itself; <c>null</c> if the profile is not protected.
    /// </summary>
    public string? PinHash { get; set; }

    /// <summary>
    /// Wrong PIN attempts since the last correct one or since the last lockout ended. Every attempt is counted before
    /// the PIN is checked, so parallel attempts cannot get past the limit; a correct PIN resets the count.
    /// </summary>
    public int PinFailedAttempts { get; set; }

    /// <summary>
    /// End of the temporary lockout after too many wrong PINs; <c>null</c> without a lockout. An ended lockout stays
    /// until the next attempt, which starts counting again.
    /// </summary>
    public DateTimeOffset? PinLockoutEnd { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    public UserSettings? Settings { get; set; }

    public ICollection<Session> Sessions { get; set; } = [];
}
