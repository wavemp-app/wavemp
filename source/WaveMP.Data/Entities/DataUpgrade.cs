namespace WaveMP.Data.Entities;

/// <summary>
/// A change of the stored data that a migration cannot make in SQL alone, because it needs the rules of the service
/// (e.g. the normalized names of <see cref="Texts.NameKeys"/>). The migration that needs it adds the row; the service
/// carries the change out on its next start, right after the migrations, and records when it finished. A change that
/// fails is rolled back and repeated on the next start.
/// </summary>
public class DataUpgrade
{
    /// <summary>
    /// Name of the normalized names of artists, albums and genres (see <see cref="Texts.NameKeys"/>): existing entries
    /// with equal normalized names are joined and rules of smart playlists that name an artist, album or genre get its
    /// id.
    /// </summary>
    public const string NameKeys = "name-keys";

    public required string Name { get; set; }

    /// <summary>
    /// When the service finished the change; <c>null</c> while it is pending.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }
}
