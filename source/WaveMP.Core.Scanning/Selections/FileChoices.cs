namespace WaveMP.Core.Scanning.Selections;

/// <summary>
/// The files the admin selected for a track instead of the automatic choice by quality; <c>null</c> leaves the choice
/// to the quality rule.
/// </summary>
/// <param name="Audio">Id of the audio file to play.</param>
/// <param name="Video">Id of the video file to play.</param>
/// <param name="Cover">Id of the file whose picture is the cover of the track: a picture file, or an audio or video
/// file with an embedded picture.</param>
public sealed record FileChoices(Guid? Audio = null, Guid? Video = null, Guid? Cover = null)
{
    /// <summary>
    /// No selection: the quality rule chooses every file.
    /// </summary>
    public static FileChoices None { get; } = new();
}
