namespace WaveMP.Data.Repositories;

/// <summary>
/// Thrown when data of a user is written while the user does not exist (any more), e.g. because the profile was
/// deleted while the request ran. Nothing of the change is saved.
/// </summary>
public sealed class UserNotFoundException(Guid userId, Exception? innerException = null)
    : Exception("The user does not exist.", innerException)
{
    /// <summary>
    /// Id of the missing user.
    /// </summary>
    public Guid UserId { get; } = userId;
}
