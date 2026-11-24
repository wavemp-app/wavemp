using System.Runtime.Versioning;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WaveMP.Core.Organizing.Files;
using WaveMP.Testing.Files;

namespace WaveMP.Core.Organizing.Tests.Files;

public sealed class MediaWriteProbeTests
{
    [Test]
    public void A_writable_folder_is_writable_and_keeps_no_probe_file()
    {
        using TemporaryDirectory directory = new();
        MediaWriteProbe probe = new(NullLogger<MediaWriteProbe>.Instance);

        probe.Probe(directory.Path).Should().Be(MediaFolderState.Writable);
        Directory.EnumerateFileSystemEntries(directory.Path).Should().BeEmpty();
    }

    [Test]
    public void A_missing_folder_is_missing()
    {
        using TemporaryDirectory directory = new();
        MediaWriteProbe probe = new(NullLogger<MediaWriteProbe>.Instance);

        probe.Probe(directory.Combine("missing")).Should().Be(MediaFolderState.Missing);
    }

    [Test]
    [Platform(Exclude = "Win", Reason = "Folder permissions are checked through Unix file modes.")]
    [UnsupportedOSPlatform("windows")]
    public void A_folder_without_write_permission_is_read_only()
    {
        if (Environment.UserName == "root")
        {
            Assert.Ignore("root may write into every folder.");
        }

        using TemporaryDirectory directory = new();
        string folder = directory.CreateDirectory("readonly");
        File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            new MediaWriteProbe(NullLogger<MediaWriteProbe>.Instance).Probe(folder)
                .Should().Be(MediaFolderState.ReadOnly);
        }
        finally
        {
            File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
