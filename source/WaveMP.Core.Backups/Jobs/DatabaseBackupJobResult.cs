namespace WaveMP.Core.Backups.Jobs;

/// <summary>
/// Result of a backup run in the run history.
/// </summary>
/// <param name="FileName">Name of the new export.</param>
/// <param name="SizeBytes">Its size.</param>
/// <param name="DeletedCount">How many old exports were deleted.</param>
public sealed record DatabaseBackupJobResult(string FileName, long SizeBytes, int DeletedCount);
