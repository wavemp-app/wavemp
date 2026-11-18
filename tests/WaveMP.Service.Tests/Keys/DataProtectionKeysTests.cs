using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WaveMP.Service.Extensions;
using WaveMP.Service.Keys;
using WaveMP.Testing.Files;

namespace WaveMP.Service.Tests.Keys;

[TestFixture]
public class DataProtectionKeysTests
{
    private const string Purpose = "WaveMP.Tests";

    [Test]
    public void AddWaveMpDataProtection_WithDataPath_StoresTheKeysUnencryptedInItsFolderKeys()
    {
        using TemporaryDirectory data = new();
        using ServiceProvider provider = BuildProvider(data.Path);

        KeyManagementOptions options = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        options.XmlRepository.Should().BeOfType<FileSystemXmlRepository>()
            .Which.Directory.FullName.Should().Be(Path.Combine(data.Path, "keys"));
        options.XmlEncryptor.Should().BeOfType<NullXmlEncryptor>();
    }

    [Test]
    public void AddWaveMpDataProtection_WithoutDataPath_UsesTheDataVolume()
    {
        using ServiceProvider provider = BuildProvider(dataPath: null);

        KeyManagementOptions options = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        options.XmlRepository.Should().BeOfType<FileSystemXmlRepository>()
            .Which.Directory.FullName.Should().Be(Path.Combine(Path.GetFullPath("/data"), "keys"));
    }

    [Test]
    public void AddWaveMpDataProtection_Always_UsesTheApplicationNameWaveMP()
    {
        using ServiceProvider provider = BuildProvider(dataPath: null);

        DataProtectionOptions options = provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value;

        options.ApplicationDiscriminator.Should().Be("WaveMP");
    }

    [Test]
    public void Protect_FirstUse_WritesTheKeyIntoTheFolderWithoutWarnings()
    {
        using TemporaryDirectory data = new();
        WarningLogCollector warnings = new();
        using ServiceProvider provider = BuildProvider(data.Path, warnings);

        provider.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect("secret");

        Directory.GetFiles(data.Combine("keys"), "key-*.xml").Should().ContainSingle();
        warnings.Entries.Should().BeEmpty(
            "neither an in-memory key repository nor a missing XML encryptor is reported");
    }

    [Test]
    public void Unprotect_AfterARestartWithTheSameDataPath_ReadsWhatWasProtectedBefore()
    {
        using TemporaryDirectory data = new();
        string protectedValue;
        using (ServiceProvider first = BuildProvider(data.Path))
        {
            protectedValue = first.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector(Purpose)
                .Protect("secret");
        }

        using ServiceProvider second = BuildProvider(data.Path);
        string value = second.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(Purpose)
            .Unprotect(protectedValue);

        value.Should().Be("secret");
    }

    [Test]
    public void PrepareFolder_MissingFolder_CreatesItForTheCurrentUserOnly()
    {
        using TemporaryDirectory data = new();
        string folder = data.Combine("keys");

        bool prepared = DataProtectionKeys.PrepareFolder(folder, NullLogger.Instance);

        prepared.Should().BeTrue();
        Directory.Exists(folder).Should().BeTrue();
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(folder).Should()
                .Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Test]
    public void PrepareFolder_ExistingFolder_KeepsItAndItsFiles()
    {
        using TemporaryDirectory data = new();
        string folder = data.CreateDirectory("keys");
        string key = Path.Combine(folder, "key-existing.xml");
        File.WriteAllText(key, "<key />");

        bool prepared = DataProtectionKeys.PrepareFolder(folder, NullLogger.Instance);

        prepared.Should().BeTrue();
        File.ReadAllText(key).Should().Be("<key />");
    }

    [Test]
    public void PrepareFolder_PathIsAFile_LogsAWarningAndReturnsFalse()
    {
        using TemporaryDirectory data = new();
        string file = data.Combine("keys");
        File.WriteAllText(file, string.Empty);
        WarningLogCollector warnings = new();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(warnings));

        bool prepared = DataProtectionKeys.PrepareFolder(file, loggerFactory.CreateLogger("Test"));

        prepared.Should().BeFalse();
        warnings.Entries.Should().ContainSingle().Which.Should().Contain(file);
    }

    private static ServiceProvider BuildProvider(string? dataPath, WarningLogCollector? warnings = null)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DATA_PATH"] = dataPath })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddLogging(logging =>
        {
            if (warnings is not null)
            {
                logging.AddProvider(warnings);
            }
        });
        services.AddWaveMpDataProtection();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Collects the log entries from level <see cref="LogLevel.Warning"/> on.
    /// </summary>
    private sealed class WarningLogCollector : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _entries = new();

        public IReadOnlyCollection<string> Entries => [.. _entries];

        public ILogger CreateLogger(string categoryName)
        {
            return new Logger(categoryName, _entries);
        }

        public void Dispose()
        {
        }

        private sealed class Logger(string category, ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel >= LogLevel.Warning;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel))
                {
                    entries.Enqueue($"{category}: {formatter(state, exception)}");
                }
            }
        }
    }
}
