namespace WaveMP.Core.Library.Checks;

/// <summary>
/// One run of the library check.
/// </summary>
public interface ILibraryCheckRunner
{
    /// <summary>
    /// Checks all tracks and files of the library and replaces the stored problems with those it found.
    /// </summary>
    Task<LibraryCheckJobResult> RunAsync(CancellationToken cancellationToken = default);
}
