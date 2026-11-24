namespace WaveMP.Core.Organizing.Files;

/// <summary>
/// The outcome of a move or copy by <see cref="INoReplaceFileMover"/>.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="ErrorNumber">The error number of the operating system, if one is known; for the log only.</param>
public sealed record FileMoveResult(FileMoveStatus Status, int? ErrorNumber = null)
{
    public static FileMoveResult Moved { get; } = new(FileMoveStatus.Moved);

    public static FileMoveResult TargetExists { get; } = new(FileMoveStatus.TargetExists);

    public static FileMoveResult SourceMissing { get; } = new(FileMoveStatus.SourceMissing);

    public bool IsMoved => Status == FileMoveStatus.Moved;
}

/// <summary>
/// What a move or copy by <see cref="INoReplaceFileMover"/> did.
/// </summary>
public enum FileMoveStatus
{
    /// <summary>
    /// The file is at the target now (for a copy: also still at the source).
    /// </summary>
    Moved,

    /// <summary>
    /// The target already exists; nothing was changed.
    /// </summary>
    TargetExists,

    /// <summary>
    /// The source does not exist; nothing was changed.
    /// </summary>
    SourceMissing,

    /// <summary>
    /// Another error; the source is unchanged and the target does not exist (a partial target was removed).
    /// </summary>
    Failed,
}
