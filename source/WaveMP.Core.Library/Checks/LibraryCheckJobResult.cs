namespace WaveMP.Core.Library.Checks;

/// <summary>
/// Result of a run of the library check, stored as JSON with the run.
/// </summary>
/// <param name="TracksChecked">Tracks the run checked.</param>
/// <param name="Problems">Problems found, without the hints.</param>
/// <param name="Hints">Hints found (videos without audio, songs without video).</param>
/// <param name="Added">Problems and hints found for the first time.</param>
/// <param name="Removed">Problems and hints of the run before that are gone.</param>
public sealed record LibraryCheckJobResult(int TracksChecked, int Problems, int Hints, int Added, int Removed);
