using AwesomeAssertions;
using WaveMP.Core.Analysis.Detection;
using WaveMP.Core.Analysis.Features;
using WaveMP.Core.Analysis.Tests.Sounds;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Analysis.Tests.Detection;

[TestFixture]
public class RecordingAlignerTests
{
    /// <summary>
    /// Positions may differ by this much from the exact cut (a few frames).
    /// </summary>
    private const int Tolerance = 120;

    private static readonly float[] _album = SyntheticSound.Melody(60, 7);

    [Test]
    public void Align_SameRecording_IsTheWholeFile()
    {
        RecordingAlignment? alignment = RecordingAligner.Align(
            SyntheticSound.Features(_album),
            SyntheticSound.Features(_album));

        alignment.Should().NotBeNull();
        alignment!.Matches.Should().BeTrue();
        alignment.Confidence.Should().BeGreaterThan(0.9);
        alignment.Pieces.Should().ContainSingle();
        ExpectPiece(alignment.Pieces[0], 0, 0, 60_000);
    }

    [Test]
    public void Align_VideoWithIntroAndOutro_FindsTheSongInIt()
    {
        float[] video = SyntheticSound.Concat(
            SyntheticSound.Noise(8, 1),
            _album,
            SyntheticSound.Noise(6, 2));

        RecordingAlignment alignment = RecordingAligner.Align(
            SyntheticSound.Features(_album),
            SyntheticSound.Features(video))!;

        alignment.Matches.Should().BeTrue();
        alignment.Confidence.Should().BeGreaterThan(0.6);
        alignment.Pieces.Should().ContainSingle();
        ExpectPiece(alignment.Pieces[0], 8000, 0, 60_000);
    }

    [Test]
    public void Align_LouderVideoWithIntroOutroAndACutPart_MapsBothPieces()
    {
        // The video leaves out the song from 30 to 40 seconds and is louder.
        float[] video = SyntheticSound.Gain(
            SyntheticSound.Concat(
                SyntheticSound.Noise(6, 3),
                SyntheticSound.Slice(_album, 0, 30),
                SyntheticSound.Slice(_album, 40, 60),
                SyntheticSound.Noise(5, 4)),
            1.8f);

        RecordingAlignment alignment = RecordingAligner.Align(
            SyntheticSound.Features(_album),
            SyntheticSound.Features(video))!;

        alignment.Matches.Should().BeTrue();
        alignment.Pieces.Should().HaveCount(2);
        ExpectPiece(alignment.Pieces[0], 6000, 0, 30_000);
        ExpectPiece(alignment.Pieces[1], 36_000, 40_000, 20_000);
    }

    [Test]
    public void Align_VideoWithAnAddedScene_MapsBothPieces()
    {
        // The video stops the song after 25 seconds for a scene of 7 seconds.
        float[] video = SyntheticSound.Concat(
            SyntheticSound.Slice(_album, 0, 25),
            SyntheticSound.Noise(7, 5),
            SyntheticSound.Slice(_album, 25, 60));

        RecordingAlignment alignment = RecordingAligner.Align(
            SyntheticSound.Features(_album),
            SyntheticSound.Features(video))!;

        alignment.Matches.Should().BeTrue();
        alignment.Pieces.Should().HaveCount(2);
        ExpectPiece(alignment.Pieces[0], 0, 0, 25_000);
        ExpectPiece(alignment.Pieces[1], 32_000, 25_000, 35_000);
    }

    [Test]
    public void Align_OtherRecording_IsAMismatch()
    {
        // Another version: other notes, e.g. a live version or a remix.
        float[] other = SyntheticSound.Concat(SyntheticSound.Noise(5, 6), SyntheticSound.Melody(60, 99));

        RecordingAlignment alignment = RecordingAligner.Align(
            SyntheticSound.Features(_album),
            SyntheticSound.Features(other))!;

        alignment.Matches.Should().BeFalse();
        alignment.Pieces.Should().BeEmpty();
        alignment.Confidence.Should().BeLessThan(0.3);
    }

    [Test]
    public void Align_TooShort_ReturnsNull()
    {
        SoundFeatures shortSound = SyntheticSound.Features(SyntheticSound.Melody(3, 8));

        RecordingAligner.Align(shortSound, SyntheticSound.Features(_album)).Should().BeNull();
        RecordingAligner.Align(SyntheticSound.Features(_album), shortSound).Should().BeNull();
    }

    [Test]
    public void Align_Silence_ReturnsNull()
    {
        SoundFeatures silence = SyntheticSound.Features(SyntheticSound.Silence(30));

        RecordingAligner.Align(silence, SyntheticSound.Features(_album)).Should().BeNull();
    }

    private static void ExpectPiece(SongPiece piece, int fileStart, int songStart, int duration)
    {
        piece.FileStartMilliseconds.Should().BeCloseTo(fileStart, Tolerance);
        piece.SongStartMilliseconds.Should().BeCloseTo(songStart, Tolerance);
        piece.DurationMilliseconds.Should().BeCloseTo(duration, (uint)(2 * Tolerance));
        (piece.FileStartMilliseconds - piece.SongStartMilliseconds).Should().BeCloseTo(fileStart - songStart, 30);
    }
}
