namespace WaveMP.Data.Entities;

/// <summary>
/// The file the admin selected for a track in one category, instead of the automatic choice by quality. A track has at
/// most one selection per category; without one, the quality rule applies. The selection refers to the file by id and
/// remembers its path, so it survives rescans: a file that is deleted leaves the selection missing (the track falls
/// back to the automatic choice), and a file that appears again under the same path is selected again. When a rescan
/// finds the track at a new place (folder renamed or moved, files renamed), the selection goes with it: the file the
/// track played keeps its id and gets its new path, and another selected file is mapped to the file of the track with
/// the same name, or with the old base name replaced by the new one; without exactly one such file the selection is
/// missing.
/// </summary>
public class TrackFileSelection
{
    public required Guid TrackId { get; set; }

    public Track? Track { get; set; }

    public required FileSelectionCategory Category { get; set; }

    /// <summary>
    /// Id of the selected file: the audio or video file, or for the cover the picture file or the audio or video file
    /// the picture is embedded in. Not a foreign key, so the selection stays when the file is deleted.
    /// </summary>
    public required Guid MediaFileId { get; set; }

    /// <summary>
    /// Path of the selected file relative to the media folder, as it was when it was selected.
    /// </summary>
    public required string RelativePath { get; set; }

    /// <summary>
    /// Whether the selected file is gone or no longer fits the category; the track then uses the automatic choice.
    /// Kept up to date whenever the folder of the track is written.
    /// </summary>
    public required bool IsMissing { get; set; }

    /// <summary>
    /// When the admin selected the file.
    /// </summary>
    public required DateTimeOffset SelectedAt { get; set; }
}
