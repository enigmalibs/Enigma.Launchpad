using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Persistence;
using Enigma.Launchpad.Core.Settings;
using Enigma.Launchpad.Core.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.Launchpad.Core.UnitTests.Settings;

public sealed class SettingsServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 8, 30, 15, TimeSpan.Zero);

    private readonly TempDirectory _temp = new();
    private readonly SettingsService _settings;

    public SettingsServiceTests() => _settings = NewService();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Defaults_FollowTheSystem_AndDoNotMinimize()
    {
        Assert.Equal(ThemePreference.System, AppSettings.Defaults.Theme);
        Assert.False(AppSettings.Defaults.MinimizeAfterLaunch);
        Assert.Equal(AppSettings.CurrentVersion, AppSettings.Defaults.Version);
    }

    [Fact]
    public void FilePath_IsBesideTheLaunchpad()
        => Assert.Equal(Path.Combine(_temp.Path, SettingsService.FileName), _settings.FilePath);

    [Fact]
    public async Task LoadAsync_WithoutAFile_KeepsTheDefaults()
    {
        await _settings.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AppSettings.Defaults, _settings.Current);
        Assert.False(File.Exists(_settings.FilePath));
    }

    [Fact]
    public async Task Update_IsSaved_AndReadBackByTheNextStart()
    {
        _settings.Update(current => current with { Theme = ThemePreference.Light, MinimizeAfterLaunch = true });
        await _settings.FlushAsync();

        SettingsService next = NewService();
        await next.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ThemePreference.Light, next.Current.Theme);
        Assert.True(next.Current.MinimizeAfterLaunch);
    }

    [Fact]
    public async Task Update_WritesReadableJson_WithTheThemeByName()
    {
        _settings.Update(current => current with { Theme = ThemePreference.Dark });
        await _settings.FlushAsync();

        string json = await File.ReadAllTextAsync(_settings.FilePath, TestContext.Current.CancellationToken);

        Assert.Contains("\"version\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"theme\": \"dark\"", json, StringComparison.Ordinal);
        Assert.Contains("\"minimizeAfterLaunch\": false", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Update_RaisesChangedOnce_WithTheNewSettings()
    {
        List<AppSettings> raised = [];
        _settings.Changed += (_, e) => raised.Add(e.Settings);

        AppSettings result = _settings.Update(current => current with { Theme = ThemePreference.Dark });

        Assert.Equal(ThemePreference.Dark, Assert.Single(raised).Theme);
        Assert.Same(result, _settings.Current);
    }

    [Fact]
    public async Task Update_ThatChangesNothing_NeitherWritesNorRaises()
    {
        int raised = 0;
        _settings.Changed += (_, _) => raised++;

        _settings.Update(current => current with { Theme = ThemePreference.System });
        await _settings.FlushAsync();

        Assert.Equal(0, raised);
        Assert.False(File.Exists(_settings.FilePath));
    }

    [Fact]
    public async Task Update_PutsAnUnknownThemeBackToTheDefault()
    {
        _settings.Update(current => current with { Theme = ThemePreference.Dark });
        _settings.Update(current => current with { Theme = (ThemePreference)42 });
        await _settings.FlushAsync();

        Assert.Equal(ThemePreference.System, _settings.Current.Theme);
    }

    [Fact]
    public async Task ManyUpdates_LeaveTheLatestSettingsOnDisk()
    {
        ThemePreference[] themes = [ThemePreference.Dark, ThemePreference.Light, ThemePreference.System, ThemePreference.Dark];

        foreach (ThemePreference theme in themes)
        {
            _settings.Update(current => current with { Theme = theme, MinimizeAfterLaunch = !current.MinimizeAfterLaunch });
        }

        await _settings.FlushAsync();

        SettingsService next = NewService();
        await next.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_settings.Current, next.Current);
        Assert.Equal([_settings.FilePath], Directory.GetFiles(_temp.Path));
    }

    [Fact]
    public async Task LoadAsync_ReadsAFileMissingFields_WithTheirDefaults()
    {
        _temp.CreateFile(SettingsService.FileName, "{ \"theme\": \"light\" }");

        await _settings.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ThemePreference.Light, _settings.Current.Theme);
        Assert.False(_settings.Current.MinimizeAfterLaunch);
    }

    [Fact]
    public async Task LoadAsync_PutsANumericUnknownThemeBackToTheDefault()
    {
        _temp.CreateFile(SettingsService.FileName, "{ \"version\": 1, \"theme\": 7 }");

        await _settings.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ThemePreference.System, _settings.Current.Theme);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{ \"theme\": \"purple\" }")]
    [InlineData("[]")]
    public async Task LoadAsync_MovesAnUnreadableFileAside_AndUsesTheDefaults(string contents)
    {
        _temp.CreateFile(SettingsService.FileName, contents);

        await _settings.LoadAsync(TestContext.Current.CancellationToken);

        string movedTo = Path.Combine(_temp.Path, "settings.corrupt-20261012083015.json");
        Assert.Equal(AppSettings.Defaults, _settings.Current);
        Assert.False(File.Exists(_settings.FilePath));
        Assert.Equal(contents, await File.ReadAllTextAsync(movedTo, TestContext.Current.CancellationToken));
    }

    private SettingsService NewService()
        => new(new AppPaths(_temp.Path), new FixedTimeProvider(Now), NullLogger<SettingsService>.Instance);
}
