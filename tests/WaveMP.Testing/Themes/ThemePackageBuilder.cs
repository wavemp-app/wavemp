using System.IO.Compression;
using System.Text;

namespace WaveMP.Testing.Themes;

/// <summary>
/// Builds theme packages (ZIP files) for tests. A new builder holds a small valid theme: <c>manifest.json</c>,
/// <c>theme.css</c> that uses <c>images/glass.svg</c>, and a PNG preview per variant. Tests change, add or remove files
/// to build invalid or malicious packages. The pictures are tiny files with the right signature only.
/// </summary>
public sealed class ThemePackageBuilder
{
    /// <summary>
    /// The first bytes of a PNG picture.
    /// </summary>
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, .. "IHDR"u8];

    /// <summary>
    /// The first bytes of a WebP picture.
    /// </summary>
    public static readonly byte[] WebP = [.. "RIFF"u8, 4, 0, 0, 0, .. "WEBPVP8 "u8];

    public const string GlassSvg =
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect width="10" height="10" fill="#7fb2e5"/></svg>""";

    public const string Stylesheet =
        """
        :root {
            --wmp-color-accent: #1f6fd6;
            --wmp-image-play-button: url('images/glass.svg') center / 100% 100% no-repeat;
        }

        [data-part='transport'] {
            background: url(images/glass.svg);
        }
        """;

    private readonly List<KeyValuePair<string, byte[]>> _files = [];

    public ThemePackageBuilder(string id = "aurora", int themeApi = 5)
    {
        WithManifest(Manifest(id, themeApi));
        With("theme.css", Stylesheet);
        With("images/glass.svg", GlassSvg);
        With("preview-light.png", Png);
        With("preview-dark.png", Png);
    }

    /// <summary>
    /// A manifest of a theme with a light and a dark variant and PNG previews.
    /// </summary>
    public static string Manifest(string id = "aurora", int themeApi = 5, string name = "Aurora")
    {
        return $$"""
            {
              "id": "{{id}}",
              "name": "{{name}}",
              "author": "Test Author",
              "version": "1.0.0",
              "themeApi": {{themeApi}},
              "variants": ["light", "dark"],
              "previews": { "light": "preview-light.png", "dark": "preview-dark.png" }
            }
            """;
    }

    public ThemePackageBuilder WithManifest(string json)
    {
        return With("manifest.json", json);
    }

    /// <summary>
    /// Adds a text file in UTF-8 or replaces the file with that path.
    /// </summary>
    public ThemePackageBuilder With(string path, string content)
    {
        return With(path, Encoding.UTF8.GetBytes(content));
    }

    /// <summary>
    /// Adds a file or replaces the file with that path.
    /// </summary>
    public ThemePackageBuilder With(string path, byte[] content)
    {
        Without(path);
        _files.Add(new KeyValuePair<string, byte[]>(path, content));
        return this;
    }

    /// <summary>
    /// Adds a further entry, even with a path that is already there.
    /// </summary>
    public ThemePackageBuilder WithDuplicate(string path, string content)
    {
        _files.Add(new KeyValuePair<string, byte[]>(path, Encoding.UTF8.GetBytes(content)));
        return this;
    }

    public ThemePackageBuilder Without(string path)
    {
        _files.RemoveAll(file => file.Key == path);
        return this;
    }

    /// <summary>
    /// Puts every file into the folder <paramref name="folder"/>, as when a folder is zipped.
    /// </summary>
    public ThemePackageBuilder InFolder(string folder)
    {
        List<KeyValuePair<string, byte[]>> files = [.. _files];
        _files.Clear();
        _files.AddRange(files.Select(file => new KeyValuePair<string, byte[]>($"{folder}/{file.Key}", file.Value)));
        return this;
    }

    public byte[] Build()
    {
        using MemoryStream output = new();
        using (ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string path, byte[] content) in _files)
            {
                ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
                using Stream stream = entry.Open();
                stream.Write(content);
            }
        }

        return output.ToArray();
    }

    public MemoryStream BuildStream()
    {
        return new MemoryStream(Build());
    }
}
