using System;
using System.IO;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;
using Enigma.Launchpad.Core.Settings;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class ThemeApplierTests
{
    public static TheoryData<ThemePreference, PlatformThemeVariant?, string> Resolutions => new()
    {
        { ThemePreference.Light, PlatformThemeVariant.Dark, "Light" },
        { ThemePreference.Dark, PlatformThemeVariant.Light, "Dark" },
        { ThemePreference.System, PlatformThemeVariant.Light, "Light" },
        { ThemePreference.System, PlatformThemeVariant.Dark, "Dark" },
        { ThemePreference.System, null, "Dark" },
    };

    [Theory]
    [MemberData(nameof(Resolutions))]
    public void Resolve_NeverAnswersTheDefaultVariant(ThemePreference preference, PlatformThemeVariant? platform, string expected)
        => Assert.Equal(expected, ThemeApplier.Resolve(preference, platform).Key);
}

[Collection(HeadlessCollection.Name)]
public sealed class ThemeApplierHeadlessTests
{
    private readonly HeadlessAvaloniaFixture _fixture;

    public ThemeApplierHeadlessTests(HeadlessAvaloniaFixture fixture) => _fixture = fixture;

    [Fact]
    public void TheApplication_FollowsTheThemePreference_AsItChanges()
        => _fixture.Run(() =>
        {
            using WindowHarness harness = new();
            ISettingsService settings = harness.Provider.GetRequiredService<ISettingsService>();
            Application application = Application.Current ?? throw new InvalidOperationException("No application.");

            settings.Update(current => current with { Theme = ThemePreference.Light });
            Assert.Equal(ThemeVariant.Light, application.RequestedThemeVariant);

            settings.Update(current => current with { Theme = ThemePreference.Dark });
            Assert.Equal(ThemeVariant.Dark, application.RequestedThemeVariant);

            settings.Update(current => current with { Theme = ThemePreference.System });
            PlatformThemeVariant? platform = application.PlatformSettings?.GetColorValues().ThemeVariant;
            Assert.Equal(ThemeApplier.Resolve(ThemePreference.System, platform), application.RequestedThemeVariant);
        });

    [Fact]
    public void TheStoredPreference_IsAppliedBeforeTheWindowIsBuilt()
        => _fixture.Run(() =>
        {
            using TempDirectory data = new();
            File.WriteAllText(Path.Combine(data.Path, SettingsService.FileName), "{ \"theme\": \"light\" }");

            using ServiceProvider provider = CompositionRootTests.BuildProvider(data.Path);
            Application application = Application.Current ?? throw new InvalidOperationException("No application.");
            application.RequestedThemeVariant = ThemeVariant.Dark;

            App.LoadSettings(provider, application);

            Assert.Equal(ThemeVariant.Light, application.RequestedThemeVariant);
        });

    [Fact]
    public void Attach_Twice_Throws()
        => _fixture.Run(() =>
        {
            using TempDirectory data = new();
            using ServiceProvider provider = CompositionRootTests.BuildProvider(data.Path);
            Application application = Application.Current ?? throw new InvalidOperationException("No application.");
            ThemeApplier applier = provider.GetRequiredService<ThemeApplier>();

            applier.Attach(application);

            Assert.Throws<InvalidOperationException>(() => applier.Attach(application));
        });
}
