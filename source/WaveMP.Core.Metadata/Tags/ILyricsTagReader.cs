namespace WaveMP.Core.Metadata.Tags;

/// <summary>
/// Reads the lyrics in the tags of audio and video files. Never throws for damaged or unsupported files.
/// </summary>
public interface ILyricsTagReader
{
    /// <summary>
    /// The lyrics of the file, or <c>null</c> if its tags have none or the file cannot be read.
    /// </summary>
    TagLyrics? Read(string path);
}
