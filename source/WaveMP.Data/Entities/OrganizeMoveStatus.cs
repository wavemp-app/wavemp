namespace WaveMP.Data.Entities;

/// <summary>
/// State of one file move of an organize operation. Stored as its name.
/// </summary>
public enum OrganizeMoveStatus
{
    Pending,
    Moved,
    RolledBack,
}
