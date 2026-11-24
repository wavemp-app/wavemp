using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Machine-readable code of a problem, part of every Problem Details response. Clients translate it.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<ProblemCodeDto>))]
public enum ProblemCodeDto
{
    /// <summary>
    /// The request is malformed or failed validation (400).
    /// </summary>
    [JsonStringEnumMemberName("request.invalid")]
    RequestInvalid,

    /// <summary>
    /// The requested resource or API route does not exist (404).
    /// </summary>
    [JsonStringEnumMemberName("request.not_found")]
    RequestNotFound,

    /// <summary>
    /// Any other client error without a more specific code (4xx).
    /// </summary>
    [JsonStringEnumMemberName("request.failed")]
    RequestFailed,

    /// <summary>
    /// An unexpected error occurred on the server (500 and any other 5xx without a more specific code).
    /// </summary>
    [JsonStringEnumMemberName("server.error")]
    ServerError,

    /// <summary>
    /// The service is temporarily unavailable (503).
    /// </summary>
    [JsonStringEnumMemberName("service.unavailable")]
    ServiceUnavailable,

    /// <summary>
    /// At least one health check failed, e.g. the database is unreachable (503).
    /// </summary>
    [JsonStringEnumMemberName("health.unhealthy")]
    HealthUnhealthy,

    /// <summary>
    /// No valid session token, or the session has ended or expired (401).
    /// </summary>
    [JsonStringEnumMemberName("auth.unauthorized")]
    AuthUnauthorized,

    /// <summary>
    /// The profile is not allowed to do this, e.g. an endpoint for admins only (403).
    /// </summary>
    [JsonStringEnumMemberName("auth.forbidden")]
    AuthForbidden,

    /// <summary>
    /// The profile to select does not exist (404).
    /// </summary>
    [JsonStringEnumMemberName("profile.not_found")]
    ProfileNotFound,

    /// <summary>
    /// The user does not exist (404).
    /// </summary>
    [JsonStringEnumMemberName("user.not_found")]
    UserNotFound,

    /// <summary>
    /// Another user already has this name; names are compared without regard to case (409).
    /// </summary>
    [JsonStringEnumMemberName("user.name_taken")]
    UserNameTaken,

    /// <summary>
    /// The built-in admin profile cannot be deleted and always keeps the admin role (409).
    /// </summary>
    [JsonStringEnumMemberName("user.built_in_admin")]
    UserBuiltInAdmin,

    /// <summary>
    /// The profile to select is protected by a PIN, but the request has none; the client asks for it (401).
    /// </summary>
    [JsonStringEnumMemberName("profile.pin_required")]
    ProfilePinRequired,

    /// <summary>
    /// The PIN of the profile to select is wrong (401). Wrong PINs are counted; too many lock the profile out.
    /// </summary>
    [JsonStringEnumMemberName("profile.pin_invalid")]
    ProfilePinInvalid,

    /// <summary>
    /// Too many wrong PINs: the profile is locked out for a while; try again after Retry-After seconds (429).
    /// </summary>
    [JsonStringEnumMemberName("profile.pin_locked")]
    ProfilePinLocked,

    /// <summary>
    /// The PIN of the built-in admin profile is set by the environment variable ADMIN_PIN and cannot be changed or
    /// removed through the API (409).
    /// </summary>
    [JsonStringEnumMemberName("pin.managed_by_environment")]
    PinManagedByEnvironment,

    /// <summary>
    /// A scan is already queued or running (409).
    /// </summary>
    [JsonStringEnumMemberName("scan.already_running")]
    ScanAlreadyRunning,

    /// <summary>
    /// No scan is queued or running (404).
    /// </summary>
    [JsonStringEnumMemberName("scan.not_running")]
    ScanNotRunning,

    /// <summary>
    /// The scan does not exist (404).
    /// </summary>
    [JsonStringEnumMemberName("scan.not_found")]
    ScanNotFound,

    /// <summary>
    /// The job does not exist (404).
    /// </summary>
    [JsonStringEnumMemberName("job.not_found")]
    JobNotFound,

    /// <summary>
    /// The job is already queued or running (409).
    /// </summary>
    [JsonStringEnumMemberName("job.already_running")]
    JobAlreadyRunning,

    /// <summary>
    /// The run does not exist or belongs to another job (404).
    /// </summary>
    [JsonStringEnumMemberName("job.run_not_found")]
    JobRunNotFound,

    /// <summary>
    /// There is no export of the database with this id (404).
    /// </summary>
    [JsonStringEnumMemberName("backup.not_found")]
    BackupNotFound,

    /// <summary>
    /// The artist does not exist (404).
    /// </summary>
    [JsonStringEnumMemberName("artist.not_found")]
    ArtistNotFound,

    /// <summary>
    /// The album does not exist (404).
    /// </summary>
    [JsonStringEnumMemberName("album.not_found")]
    AlbumNotFound,

    /// <summary>
    /// The track does not exist (404).
    /// </summary>
    [JsonStringEnumMemberName("track.not_found")]
    TrackNotFound,

    /// <summary>
    /// The track has no audio file: writing tags (409; tags are only written to audio files, never to videos) or
    /// playing its audio (404).
    /// </summary>
    [JsonStringEnumMemberName("track.no_audio")]
    TrackNoAudio,

    /// <summary>
    /// The audio file of the track cannot be written: a read-only file or media folder, missing permissions,
    /// unreadable tags or a format whose tags cannot be written (409).
    /// </summary>
    [JsonStringEnumMemberName("track.not_writable")]
    TrackNotWritable,

    /// <summary>
    /// The track or album exists, but has no cover that can be delivered (404).
    /// </summary>
    [JsonStringEnumMemberName("cover.not_found")]
    CoverNotFound,

    /// <summary>
    /// The client supports none of the formats the audio file of the track could be streamed in; it has to play the
    /// transcoded HLS stream that GetTrackPlayback offers instead (406).
    /// </summary>
    [JsonStringEnumMemberName("stream.transcoding_required")]
    StreamTranscodingRequired,

    /// <summary>
    /// The audio file or video of the track is gone since the last scan (404).
    /// </summary>
    [JsonStringEnumMemberName("stream.file_missing")]
    StreamFileMissing,

    /// <summary>
    /// The track has no video (404).
    /// </summary>
    [JsonStringEnumMemberName("track.no_video")]
    TrackNoVideo,

    /// <summary>
    /// Nothing can be transcoded: ffmpeg cannot be started or the cache cannot be written (503).
    /// </summary>
    [JsonStringEnumMemberName("transcode.unavailable")]
    TranscodeUnavailable,

    /// <summary>
    /// All transcode slots are in use; try again after Retry-After seconds (503).
    /// </summary>
    [JsonStringEnumMemberName("transcode.busy")]
    TranscodeBusy,

    /// <summary>
    /// The segment was not ready in time; try again after Retry-After seconds (503).
    /// </summary>
    [JsonStringEnumMemberName("transcode.timeout")]
    TranscodeTimeout,

    /// <summary>
    /// The file cannot be transcoded: ffmpeg failed or the duration of the file is unknown (500).
    /// </summary>
    [JsonStringEnumMemberName("transcode.failed")]
    TranscodeFailed,

    /// <summary>
    /// The HLS playlist belongs to an older version of the file or of the transcoding settings; the client loads the
    /// master playlist again (404).
    /// </summary>
    [JsonStringEnumMemberName("transcode.variant_outdated")]
    TranscodeVariantOutdated,

    /// <summary>
    /// The HLS variant has no such segment (404).
    /// </summary>
    [JsonStringEnumMemberName("transcode.segment_not_found")]
    TranscodeSegmentNotFound,

    /// <summary>
    /// No transcode with the id is running (404).
    /// </summary>
    [JsonStringEnumMemberName("transcode.job_not_found")]
    TranscodeJobNotFound,

    /// <summary>
    /// The entry of the play history does not exist or belongs to another profile (404).
    /// </summary>
    [JsonStringEnumMemberName("history.not_found")]
    HistoryNotFound,

    /// <summary>
    /// The profile has no saved playback state yet (404).
    /// </summary>
    [JsonStringEnumMemberName("playback.state_not_found")]
    PlaybackStateNotFound,

    /// <summary>
    /// Another device saved a playback state that changed later than the one sent; the saved state stays (409).
    /// Clients load it (GetMyPlaybackState) to continue from there.
    /// </summary>
    [JsonStringEnumMemberName("playback.state_outdated")]
    PlaybackStateOutdated,

    /// <summary>
    /// The playlist does not exist or belongs to another profile (404).
    /// </summary>
    [JsonStringEnumMemberName("playlist.not_found")]
    PlaylistNotFound,

    /// <summary>
    /// The entry does not exist in this playlist (404).
    /// </summary>
    [JsonStringEnumMemberName("playlist.entry_not_found")]
    PlaylistEntryNotFound,

    /// <summary>
    /// The playlist would have more entries than allowed (409).
    /// </summary>
    [JsonStringEnumMemberName("playlist.full")]
    PlaylistFull,

    /// <summary>
    /// The smart playlist does not exist or belongs to another profile (404).
    /// </summary>
    [JsonStringEnumMemberName("playlist.smart_not_found")]
    SmartPlaylistNotFound,

    /// <summary>
    /// The uploaded picture is not a JPEG, PNG or WebP (415).
    /// </summary>
    [JsonStringEnumMemberName("cover.unsupported_format")]
    CoverUnsupportedFormat,

    /// <summary>
    /// The uploaded picture is larger than allowed (413).
    /// </summary>
    [JsonStringEnumMemberName("cover.too_large")]
    CoverTooLarge,

    /// <summary>
    /// The uploaded picture is damaged and cannot be read (422).
    /// </summary>
    [JsonStringEnumMemberName("cover.unreadable")]
    CoverUnreadable,

    /// <summary>
    /// The profile has no avatar, or the built-in avatar does not exist (404).
    /// </summary>
    [JsonStringEnumMemberName("avatar.not_found")]
    AvatarNotFound,

    /// <summary>
    /// The uploaded avatar is not a JPEG, PNG or WebP picture (415).
    /// </summary>
    [JsonStringEnumMemberName("avatar.unsupported_format")]
    AvatarUnsupportedFormat,

    /// <summary>
    /// The uploaded avatar is larger than allowed (413).
    /// </summary>
    [JsonStringEnumMemberName("avatar.too_large")]
    AvatarTooLarge,

    /// <summary>
    /// The uploaded avatar is damaged and cannot be read (422).
    /// </summary>
    [JsonStringEnumMemberName("avatar.unreadable")]
    AvatarUnreadable,

    /// <summary>
    /// The artist has no picture of its own (clients show the collage of its album covers instead), or no stored
    /// picture of the requested kind (404).
    /// </summary>
    [JsonStringEnumMemberName("artist.picture_not_found")]
    ArtistPictureNotFound,

    /// <summary>
    /// The uploaded artist picture is larger than allowed (413).
    /// </summary>
    [JsonStringEnumMemberName("artist.picture_too_large")]
    ArtistPictureTooLarge,

    /// <summary>
    /// The uploaded artist picture is not a JPEG, PNG or WebP picture (415).
    /// </summary>
    [JsonStringEnumMemberName("artist.picture_unsupported_format")]
    ArtistPictureUnsupportedFormat,

    /// <summary>
    /// The uploaded artist picture is damaged and cannot be read (422).
    /// </summary>
    [JsonStringEnumMemberName("artist.picture_unreadable")]
    ArtistPictureUnreadable,

    /// <summary>
    /// The online lookup of artist pictures is switched off in its settings; no request was sent (409).
    /// </summary>
    [JsonStringEnumMemberName("artist.picture_lookup_disabled")]
    ArtistPictureLookupDisabled,

    /// <summary>
    /// MusicBrainz, Wikidata, Wikimedia Commons or fanart.tv cannot be reached, failed, kept refusing requests or
    /// answered something that cannot be used (502).
    /// </summary>
    [JsonStringEnumMemberName("artist.picture_lookup_unavailable")]
    ArtistPictureLookupUnavailable,

    /// <summary>
    /// The candidate of the online lookup does not exist, expired or belongs to another artist (404); the client
    /// looks the pictures up again.
    /// </summary>
    [JsonStringEnumMemberName("artist.picture_candidate_not_found")]
    ArtistPictureCandidateNotFound,

    /// <summary>
    /// The track has no lyrics: no lyrics file, no downloaded lyrics and none in its tags (404).
    /// </summary>
    [JsonStringEnumMemberName("lyrics.not_found")]
    LyricsNotFound,

    /// <summary>
    /// The download of lyrics is switched off in the library settings (409).
    /// </summary>
    [JsonStringEnumMemberName("lyrics.download_disabled")]
    LyricsDownloadDisabled,

    /// <summary>
    /// The track already has a lyrics file, which a download never replaces (409).
    /// </summary>
    [JsonStringEnumMemberName("lyrics.file_exists")]
    LyricsFileExists,

    /// <summary>
    /// The lyrics service has no lyrics for the track (404).
    /// </summary>
    [JsonStringEnumMemberName("lyrics.download_not_found")]
    LyricsDownloadNotFound,

    /// <summary>
    /// The lyrics service lists the track as instrumental, without lyrics (404).
    /// </summary>
    [JsonStringEnumMemberName("lyrics.instrumental")]
    LyricsInstrumental,

    /// <summary>
    /// The lyrics service cannot be reached, e.g. without an internet connection (503).
    /// </summary>
    [JsonStringEnumMemberName("lyrics.service_unavailable")]
    LyricsServiceUnavailable,

    /// <summary>
    /// The lyrics service refuses requests for a while; try again later (503, with Retry-After if the service names a
    /// time).
    /// </summary>
    [JsonStringEnumMemberName("lyrics.rate_limited")]
    LyricsRateLimited,

    /// <summary>
    /// The track has no stored result of the online lyrics lookup: it was never looked up, or its result was forgotten
    /// (404).
    /// </summary>
    [JsonStringEnumMemberName("lyrics.lookup_not_found")]
    LyricsLookupNotFound,

    /// <summary>
    /// The online lyrics lookup of the track found lyrics or the track already had lyrics; only results without lyrics
    /// (not found or instrumental) can be forgotten (409).
    /// </summary>
    [JsonStringEnumMemberName("lyrics.lookup_found_lyrics")]
    LyricsLookupFoundLyrics,

    /// <summary>
    /// The scrobbling service cannot be connected because the server has no API account for it: Last.fm needs the
    /// environment variables LASTFM_API_KEY and LASTFM_SHARED_SECRET (409).
    /// </summary>
    [JsonStringEnumMemberName("scrobbling.not_configured")]
    ScrobblingNotConfigured,

    /// <summary>
    /// The scrobbling service did not accept the token: wrong, expired or not authorized by the user (422).
    /// </summary>
    [JsonStringEnumMemberName("scrobbling.token_invalid")]
    ScrobblingTokenInvalid,

    /// <summary>
    /// The scrobbling service cannot be reached, e.g. without an internet connection; try again later (503).
    /// </summary>
    [JsonStringEnumMemberName("scrobbling.service_unavailable")]
    ScrobblingServiceUnavailable,

    /// <summary>
    /// The profile has not connected this scrobbling service (404).
    /// </summary>
    [JsonStringEnumMemberName("scrobbling.not_connected")]
    ScrobblingNotConnected,

    /// <summary>
    /// There is no uploaded theme with this id (404).
    /// </summary>
    [JsonStringEnumMemberName("theme.not_found")]
    ThemeNotFound,

    /// <summary>
    /// The theme has no file with this name (404).
    /// </summary>
    [JsonStringEnumMemberName("theme.file_not_found")]
    ThemeFileNotFound,

    /// <summary>
    /// The theme package was rejected; the member <c>errors</c> lists every reason (422).
    /// </summary>
    [JsonStringEnumMemberName("theme.invalid")]
    ThemeInvalid,

    /// <summary>
    /// The theme package is larger than allowed (413).
    /// </summary>
    [JsonStringEnumMemberName("theme.too_large")]
    ThemeTooLarge,

    /// <summary>
    /// The upload is no ZIP file (415).
    /// </summary>
    [JsonStringEnumMemberName("theme.unsupported_format")]
    ThemeUnsupportedFormat,

    /// <summary>
    /// A theme with the id of the manifest is already installed; replace it with PUT instead (409).
    /// </summary>
    [JsonStringEnumMemberName("theme.already_installed")]
    ThemeAlreadyInstalled,

    /// <summary>
    /// The media source does not exist (404).
    /// </summary>
    [JsonStringEnumMemberName("source.not_found")]
    SourceNotFound,

    /// <summary>
    /// Another media source already has this name; names are compared without regard to case (409).
    /// </summary>
    [JsonStringEnumMemberName("source.name_taken")]
    SourceNameTaken,

    /// <summary>
    /// The folder does not exist in the container (422).
    /// </summary>
    [JsonStringEnumMemberName("source.path_not_found")]
    SourcePathNotFound,

    /// <summary>
    /// The folder is the folder of another media source, lies in it or contains it (409).
    /// </summary>
    [JsonStringEnumMemberName("source.path_overlaps")]
    SourcePathOverlaps,

    /// <summary>
    /// The online search for album info is switched off in its settings (409).
    /// </summary>
    [JsonStringEnumMemberName("lookup.disabled")]
    LookupDisabled,

    /// <summary>
    /// MusicBrainz or the Cover Art Archive cannot be reached, e.g. without an internet connection, or failed (503).
    /// </summary>
    [JsonStringEnumMemberName("lookup.unavailable")]
    LookupUnavailable,

    /// <summary>
    /// The release has no cover in the Cover Art Archive (404).
    /// </summary>
    [JsonStringEnumMemberName("lookup.cover_not_found")]
    LookupCoverNotFound,

    /// <summary>
    /// The shared recap does not exist (404).
    /// </summary>
    [JsonStringEnumMemberName("recap.share_not_found")]
    RecapShareNotFound,

    /// <summary>
    /// The recorded video is larger than allowed (413).
    /// </summary>
    [JsonStringEnumMemberName("video.too_large")]
    VideoTooLarge,

    /// <summary>
    /// The content is no WebM, Matroska or MP4 video (415).
    /// </summary>
    [JsonStringEnumMemberName("video.unsupported_format")]
    VideoUnsupportedFormat,

    /// <summary>
    /// The video could not be converted, e.g. because it is damaged (422).
    /// </summary>
    [JsonStringEnumMemberName("video.unreadable")]
    VideoUnreadable,

    /// <summary>
    /// The own album does not exist (404).
    /// </summary>
    [JsonStringEnumMemberName("organize.own_album_not_found")]
    OwnAlbumNotFound,

    /// <summary>
    /// The own album has no songs, so there is nothing to apply (409).
    /// </summary>
    [JsonStringEnumMemberName("organize.own_album_empty")]
    OwnAlbumEmpty,

    /// <summary>
    /// A song is already part of another own album; a song belongs to at most one (409).
    /// </summary>
    [JsonStringEnumMemberName("organize.track_in_other_own_album")]
    OwnAlbumTrackInOtherAlbum,

    /// <summary>
    /// More songs than an own album may have (409).
    /// </summary>
    [JsonStringEnumMemberName("organize.own_album_too_many_tracks")]
    OwnAlbumTooManyTracks,

    /// <summary>
    /// The organize operation does not exist (404).
    /// </summary>
    [JsonStringEnumMemberName("organize.operation_not_found")]
    OrganizeOperationNotFound,

    /// <summary>
    /// A media folder of the operation cannot be written, e.g. a read-only mount; files cannot be moved (409).
    /// </summary>
    [JsonStringEnumMemberName("organize.media_read_only")]
    OrganizeMediaReadOnly,

    /// <summary>
    /// The saved layout template cannot be used (409).
    /// </summary>
    [JsonStringEnumMemberName("organize.template_invalid")]
    OrganizeTemplateInvalid,

    /// <summary>
    /// Another organize operation is queued or running (409).
    /// </summary>
    [JsonStringEnumMemberName("organize.busy")]
    OrganizeBusy,

    /// <summary>
    /// The operation cannot be undone: it is an undo itself, did not run or changed nothing (409).
    /// </summary>
    [JsonStringEnumMemberName("organize.not_undoable")]
    OrganizeNotUndoable,

    /// <summary>
    /// The operation was undone already (409).
    /// </summary>
    [JsonStringEnumMemberName("organize.already_undone")]
    OrganizeAlreadyUndone,

    /// <summary>
    /// The operation is no preview any more; only previews can be discarded (409).
    /// </summary>
    [JsonStringEnumMemberName("organize.not_preview")]
    OrganizeNotPreview,

    /// <summary>
    /// The operation is no preview waiting for its confirmation, e.g. it is still being planned or ran already (409).
    /// </summary>
    [JsonStringEnumMemberName("organize.not_previewed")]
    OrganizeNotPreviewed,

    /// <summary>
    /// The preview was not confirmed in time; make a new one (409).
    /// </summary>
    [JsonStringEnumMemberName("organize.preview_expired")]
    OrganizePreviewExpired,

    /// <summary>
    /// The preview has conflicts; confirm it with skipConflicts to leave those songs out (409).
    /// </summary>
    [JsonStringEnumMemberName("organize.conflicts")]
    OrganizeConflicts,

    /// <summary>
    /// The layout template, the own album or the undone operation changed since the preview; make a new one (409).
    /// </summary>
    [JsonStringEnumMemberName("organize.preview_outdated")]
    OrganizePreviewOutdated,

    /// <summary>
    /// The operation is not planning, queued or running, so it cannot be stopped (409).
    /// </summary>
    [JsonStringEnumMemberName("organize.not_cancelable")]
    OrganizeNotCancelable,
}
