using WaveMP.Core.Users.Equalizers;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Users.Settings;

internal sealed class UserSettingsService(IUserRepository users) : IUserSettingsService
{
    public async Task<UserSettings?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        User? user = await users.FindAsync(userId, cancellationToken);
        return user is null ? null : user.Settings ?? new UserSettings { UserId = user.Id };
    }

    public async Task<UserSettings?> UpdateAsync(
        Guid userId,
        UpdateUserSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        // The API checks the values before; this keeps invalid values out of the database for every other caller.
        Validate(command);

        User? user = await users.FindAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        UserSettings settings = user.Settings ??= new UserSettings { UserId = user.Id };
        if (command.Language is { } language)
        {
            settings.Language = LanguageTags.Normalize(language);
        }

        if (command.ReplayGain is { } replayGain)
        {
            settings.ReplayGain = replayGain;
        }

        if (command.PlayerViewMode is { } playerViewMode)
        {
            settings.PlayerViewMode = playerViewMode;
        }

        if (command.SidebarCollapsed is { } sidebarCollapsed)
        {
            settings.SidebarCollapsed = sidebarCollapsed;
        }

        if (command.ThemeId is { } themeId)
        {
            settings.ThemeId = themeId;
        }

        if (command.ThemeAppearance is { } appearance)
        {
            settings.ThemeAppearance = appearance;
        }

        ApplyPlayback(settings, command);
        ApplyVisualization(settings, command);

        await users.UpdateAsync(user, cancellationToken);
        return settings;
    }

    private static void ApplyVisualization(UserSettings settings, UpdateUserSettingsCommand command)
    {
        if (command.VisualizationModule is { } module && module != settings.VisualizationModule)
        {
            // The preset belongs to the module; another module starts without one, unless the command names one.
            settings.VisualizationModule = module;
            settings.VisualizationPreset = null;
        }

        if (command.VisualizationPreset is { } preset)
        {
            settings.VisualizationPreset = string.IsNullOrWhiteSpace(preset) ? null : preset;
        }

        if (command.VisualizationAutoChange is { } autoChange)
        {
            settings.VisualizationAutoChange = autoChange;
        }

        if (command.VisualizationAutoChangeSeconds is { } seconds)
        {
            settings.VisualizationAutoChangeSeconds = seconds;
        }

        if (command.VisualizationQuality is { } quality)
        {
            settings.VisualizationQuality = quality;
        }
    }

    private static void ApplyPlayback(UserSettings settings, UpdateUserSettingsCommand command)
    {
        if (command.EqualizerEnabled is { } enabled)
        {
            settings.EqualizerEnabled = enabled;
        }

        if (command.EqualizerPreset is { } preset)
        {
            settings.EqualizerPreset = preset;
        }

        if (command.EqualizerPreampDb is { } preamp)
        {
            settings.EqualizerPreampDb = EqualizerGains.Normalize(preamp);
        }

        if (command.EqualizerCustomGainsDb is { } gains)
        {
            // A new array, so the change tracking sees the changed column.
            settings.EqualizerCustomGainsDb = EqualizerGains.Normalize(gains);
        }

        if (command.CrossfadeEnabled is { } crossfadeEnabled)
        {
            settings.CrossfadeEnabled = crossfadeEnabled;
        }

        if (command.CrossfadeSeconds is { } crossfade)
        {
            settings.CrossfadeSeconds = crossfade;
        }

        if (command.CrossfadeAlbumsGapless is { } albumsGapless)
        {
            settings.CrossfadeAlbumsGapless = albumsGapless;
        }

        if (command.SleepTimerFadeSeconds is { } fade)
        {
            settings.SleepTimerFadeSeconds = fade;
        }

        if (command.AutoplayEnabled is { } autoplay)
        {
            settings.AutoplayEnabled = autoplay;
        }

        if (command.AutoplayIncludeVideoOnly is { } videoOnly)
        {
            settings.AutoplayIncludeVideoOnly = videoOnly;
        }
    }

    private static void Validate(UpdateUserSettingsCommand command)
    {
        if (command.Language is { } language && !LanguageTags.IsValid(language))
        {
            throw new ArgumentOutOfRangeException(nameof(command), language, "The language tag is not valid.");
        }

        if (command.EqualizerPreset is { } preset && !Enum.IsDefined(preset))
        {
            throw new ArgumentOutOfRangeException(nameof(command), preset, "Unknown equalizer preset.");
        }

        if (command.EqualizerPreampDb is { } preamp && !PlaybackSettingsLimits.IsValidPreamp(preamp))
        {
            throw new ArgumentOutOfRangeException(nameof(command), preamp, "The preamp is out of range.");
        }

        if (command.EqualizerCustomGainsDb is { } gains && !EqualizerGains.IsValid(gains))
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                gains,
                "The custom curve needs one gain within range per band.");
        }

        if (command.CrossfadeSeconds is { } crossfade && !PlaybackSettingsLimits.IsValidCrossfade(crossfade))
        {
            throw new ArgumentOutOfRangeException(nameof(command), crossfade, "The crossfade is out of range.");
        }

        if (command.SleepTimerFadeSeconds is { } fade && !PlaybackSettingsLimits.IsValidSleepTimerFade(fade))
        {
            throw new ArgumentOutOfRangeException(nameof(command), fade, "The fade-out is out of range.");
        }

        if (command.VisualizationModule is { } module && !Enum.IsDefined(module))
        {
            throw new ArgumentOutOfRangeException(nameof(command), module, "Unknown visualization module.");
        }

        if (command.VisualizationPreset is { } presetName && !VisualizationSettingsLimits.IsValidPreset(presetName))
        {
            throw new ArgumentOutOfRangeException(nameof(command), presetName, "The preset name is too long.");
        }

        if (command.VisualizationAutoChangeSeconds is { } seconds &&
            !VisualizationSettingsLimits.IsValidAutoChange(seconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                seconds,
                "The interval of the automatic change is out of range.");
        }

        if (command.ThemeId is { } themeId && !ThemeIds.IsValid(themeId))
        {
            throw new ArgumentOutOfRangeException(nameof(command), themeId, "The theme id is not valid.");
        }

        if (command.ThemeAppearance is { } appearance && !Enum.IsDefined(appearance))
        {
            throw new ArgumentOutOfRangeException(nameof(command), appearance, "Unknown theme appearance.");
        }

        if (command.VisualizationQuality is { } quality && !Enum.IsDefined(quality))
        {
            throw new ArgumentOutOfRangeException(nameof(command), quality, "Unknown visualization quality.");
        }
    }
}
