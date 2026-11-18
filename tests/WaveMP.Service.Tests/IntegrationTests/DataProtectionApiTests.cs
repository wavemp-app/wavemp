using AwesomeAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using WaveMP.Testing.Files;

namespace WaveMP.Service.Tests.IntegrationTests;

[TestFixture]
[Category("Integration")]
public class DataProtectionApiTests
{
    private const string Purpose = "WaveMP.Tests";

    [Test]
    public async Task Start_Always_KeepsTheDataProtectionKeysInTheDataFolder()
    {
        await using WaveMpWebApplicationFactory factory = new();

        factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect("secret");

        string keys = Path.Combine(factory.DataPath, "keys");
        Directory.Exists(keys).Should().BeTrue();
        Directory.GetFiles(keys, "key-*.xml").Should().NotBeEmpty();
        factory.ErrorLog.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task Restart_WithTheSameDataFolder_ReadsWhatWasProtectedBefore()
    {
        using TemporaryDirectory data = new();
        Dictionary<string, string?> settings = new() { ["DATA_PATH"] = data.Path };
        string protectedValue;
        await using (WaveMpWebApplicationFactory first = new(settings: settings))
        {
            protectedValue = first.Services.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector(Purpose)
                .Protect("secret");
        }

        await using WaveMpWebApplicationFactory second = new(settings: settings);
        string value = second.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(Purpose)
            .Unprotect(protectedValue);

        value.Should().Be("secret", "the keys are kept in the data folder, not in memory");
    }
}
