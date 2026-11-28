using WaveMP.Testing.Media;

namespace WaveMP.Core.Analysis.Tests.Sounds;

/// <summary>
/// Synthetic media files made with ffmpeg for the song analysis: the album version of a song as a sequence of tones,
/// and a music video of the same song that is longer and louder, with an intro, an outro and a part cut out.
/// </summary>
internal static class SongMedia
{
    /// <summary>
    /// Length of the album version in seconds.
    /// </summary>
    public const int AlbumSeconds = 60;

    /// <summary>
    /// Length of the intro of the video in seconds.
    /// </summary>
    public const int IntroSeconds = 6;

    /// <summary>
    /// Length of the outro of the video in seconds.
    /// </summary>
    public const int OutroSeconds = 5;

    /// <summary>
    /// The video leaves out the song from here ...
    /// </summary>
    public const int CutFromSeconds = 30;

    /// <summary>
    /// ... to here.
    /// </summary>
    public const int CutToSeconds = 40;

    /// <summary>
    /// Length of the video in seconds.
    /// </summary>
    public const int VideoSeconds = IntroSeconds + AlbumSeconds - (CutToSeconds - CutFromSeconds) + OutroSeconds;

    /// <summary>
    /// Two voices of tones whose pitch and loudness change several times a second in sequences of different lengths,
    /// so no part of the song repeats.
    /// </summary>
    private const string Tones = "0.3*sin(2*PI*(220+40*mod(floor(t*4)*7\\,11))*t)"
        + "+0.25*sin(2*PI*(700+90*mod(floor(t*3)*5\\,13))*t)*(0.3+0.7*mod(floor(t*5)*3\\,17)/17)";

    private const string Format = "aformat=sample_fmts=fltp:sample_rates=44100:channel_layouts=mono";

    /// <summary>
    /// The album version as FLAC.
    /// </summary>
    public static async Task CreateAlbumAsync(string path)
    {
        await FfmpegTool.RunAsync(
            "-f", "lavfi",
            "-i", $"aevalsrc=exprs={Tones}:s=44100:d={AlbumSeconds}",
            "-c:a", "flac",
            path);
    }

    /// <summary>
    /// The music video (MPEG-4 picture, AAC sound in Matroska) made from the album version: noise as intro and outro,
    /// the part between <see cref="CutFromSeconds"/> and <see cref="CutToSeconds"/> cut out, 5 dB louder.
    /// </summary>
    public static async Task CreateVideoAsync(string albumPath, string path)
    {
        string filter = $"[0:a]{Format},atrim=0:{CutFromSeconds},asetpts=PTS-STARTPTS[first];"
            + $"[0:a]{Format},atrim={CutToSeconds}:{AlbumSeconds},asetpts=PTS-STARTPTS[second];"
            + $"[1:a]{Format}[intro];[2:a]{Format}[outro];"
            + "[intro][first][second][outro]concat=n=4:v=0:a=1,volume=5dB[sound]";
        await FfmpegTool.RunAsync(
            "-i", albumPath,
            "-f", "lavfi", "-i", $"anoisesrc=d={IntroSeconds}:c=pink:a=0.15:r=44100",
            "-f", "lavfi", "-i", $"anoisesrc=d={OutroSeconds}:c=brown:a=0.3:r=44100",
            "-f", "lavfi", "-i", $"testsrc=size=160x90:rate=10:duration={VideoSeconds}",
            "-filter_complex", filter,
            "-map", "3:v", "-map", "[sound]",
            "-c:v", "mpeg4", "-c:a", "aac", "-b:a", "128k",
            "-shortest",
            path);
    }
}
