using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Configurations;

internal sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public const int ReplayGainMaxLength = 16;
    public const int PlayerViewModeMaxLength = 16;
    public const int EqualizerPresetMaxLength = 16;
    public const int VisualizationModuleMaxLength = 32;
    public const int VisualizationQualityMaxLength = 16;
    public const int VisualizationIntensityMaxLength = 16;
    public const int ThemeAppearanceMaxLength = 16;

    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("user_settings");
        builder.HasKey(settings => settings.UserId);

        // A BCP 47 language tag; rows from before kept the name of the former enum ("En"), the migration
        // ChangeLanguageToTag turned them into tags.
        builder.Property(settings => settings.Language)
            .HasMaxLength(UserSettings.LanguageMaxLength);

        // The column defaults give existing rows the defaults of a new profile; the entity always writes its values
        // (ValueGeneratedNever), so the default of a column never replaces a value set in the code.
        builder.Property(settings => settings.ThemeId)
            .HasMaxLength(UserSettings.ThemeIdMaxLength)
            .HasDefaultValue(UserSettings.DefaultThemeId)
            .ValueGeneratedNever();

        builder.Property(settings => settings.ThemeAppearance)
            .HasConversion<string>()
            .HasMaxLength(ThemeAppearanceMaxLength)
            .HasDefaultValue(ThemeAppearance.System)
            .ValueGeneratedNever();

        // The column default gives existing rows "Off". The entity always writes its value (ValueGeneratedNever),
        // so the default of the column never replaces a value set in the code.
        builder.Property(settings => settings.ReplayGain)
            .HasConversion<string>()
            .HasMaxLength(ReplayGainMaxLength)
            .HasDefaultValue(ReplayGainMode.Off)
            .ValueGeneratedNever();

        // The column default gives existing rows "Video". The default of the enum (Cover) differs from it, so the
        // entity always writes its value (ValueGeneratedNever); otherwise EF would leave out "Cover" on insert.
        builder.Property(settings => settings.PlayerViewMode)
            .HasConversion<string>()
            .HasMaxLength(PlayerViewModeMaxLength)
            .HasDefaultValue(PlayerViewMode.Video)
            .ValueGeneratedNever();

        builder.Property(settings => settings.SidebarCollapsed)
            .HasDefaultValue(false)
            .ValueGeneratedNever();

        // The columns below give existing rows the defaults of a new profile. The entity always writes its values
        // (ValueGeneratedNever), so the default of a column never replaces a value set in the code. For
        // CrossfadeSeconds, CrossfadeAlbumsGapless and SleepTimerFadeSeconds the defaults of the CLR types (0, false)
        // differ from the column defaults; EF would otherwise leave them out on insert.
        builder.Property(settings => settings.EqualizerEnabled)
            .HasDefaultValue(false)
            .ValueGeneratedNever();

        builder.Property(settings => settings.EqualizerPreset)
            .HasConversion<string>()
            .HasMaxLength(EqualizerPresetMaxLength)
            .HasDefaultValue(EqualizerPreset.Flat)
            .ValueGeneratedNever();

        builder.Property(settings => settings.EqualizerPreampDb)
            .HasDefaultValue(0d)
            .ValueGeneratedNever();

        builder.Property(settings => settings.EqualizerCustomGainsDb)
            .HasColumnType("double precision[]")
            .HasDefaultValueSql("'{0,0,0,0,0,0,0,0,0,0}'::double precision[]")
            .ValueGeneratedNever();

        builder.Property(settings => settings.CrossfadeEnabled)
            .HasDefaultValue(false)
            .ValueGeneratedNever();

        builder.Property(settings => settings.CrossfadeSeconds)
            .HasDefaultValue(5)
            .ValueGeneratedNever();

        builder.Property(settings => settings.CrossfadeAlbumsGapless)
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        builder.Property(settings => settings.SleepTimerFadeSeconds)
            .HasDefaultValue(10)
            .ValueGeneratedNever();

        // Autoplay is on for existing profiles as for new ones; the defaults (true) differ from the default of the CLR
        // type, so the entity always writes its values.
        builder.Property(settings => settings.AutoplayEnabled)
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        builder.Property(settings => settings.AutoplayIncludeVideoOnly)
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        // The ambient light of videos is on for existing profiles as for new ones, like autoplay.
        builder.Property(settings => settings.VideoAmbientMode)
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        // Skipping the intro and the outro of videos is on for existing profiles as for new ones.
        builder.Property(settings => settings.VideoSkipIntroAndOutro)
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        // Like the playback settings above: existing rows get the defaults of a new profile, and the entity always
        // writes its values (VisualizationAutoChange and VisualizationAutoChangeSeconds differ from the defaults of
        // their CLR types).
        builder.Property(settings => settings.VisualizationModule)
            .HasConversion<string>()
            .HasMaxLength(VisualizationModuleMaxLength)
            .HasDefaultValue(VisualizationModule.Milkdrop)
            .ValueGeneratedNever();

        builder.Property(settings => settings.VisualizationPreset)
            .HasMaxLength(UserSettings.VisualizationPresetMaxLength);

        builder.Property(settings => settings.VisualizationAutoChange)
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        builder.Property(settings => settings.VisualizationAutoChangeSeconds)
            .HasDefaultValue(30)
            .ValueGeneratedNever();

        builder.Property(settings => settings.VisualizationQuality)
            .HasConversion<string>()
            .HasMaxLength(VisualizationQualityMaxLength)
            .HasDefaultValue(VisualizationQuality.Auto)
            .ValueGeneratedNever();

        builder.Property(settings => settings.VisualizationColorScheme)
            .HasMaxLength(UserSettings.VisualizationColorSchemeMaxLength);

        builder.Property(settings => settings.VisualizationIntensity)
            .HasConversion<string>()
            .HasMaxLength(VisualizationIntensityMaxLength)
            .HasDefaultValue(VisualizationIntensity.Normal)
            .ValueGeneratedNever();

        // The default (50) differs from the default of the CLR type, so the entity always writes its value.
        builder.Property(settings => settings.VisualizationMilkdropSpeed)
            .HasDefaultValue(UserSettings.DefaultVisualizationMilkdropSpeed)
            .ValueGeneratedNever();
    }
}
