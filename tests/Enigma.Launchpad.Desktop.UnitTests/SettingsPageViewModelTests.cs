using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Diagnostics;
using Enigma.Launchpad.Core.Persistence;
using Enigma.Launchpad.Core.Settings;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class SettingsPageViewModelTests : IDisposable
{
    private readonly TempDirectory _data = new();
    private readonly FakeSettingsService _settings = new();
    private readonly RecordingShortcutActions _actions = new();
    private readonly RecordingAboutDialog _about = new();
    private readonly SettingsPageViewModel _page;

    public SettingsPageViewModelTests() => _page = new SettingsPageViewModel(_settings, new AppPaths(_data.Path), _actions, _about);

    public void Dispose() => _data.Dispose();

    [Fact]
    public void Themes_OfferEveryPreference_SystemFirst()
        => Assert.Equal(
            [ThemePreference.System, ThemePreference.Light, ThemePreference.Dark],
            _page.Themes.Select(option => option.Value));

    [Fact]
    public void SelectedTheme_ShowsTheCurrentPreference()
    {
        Assert.Equal(ThemePreference.System, _page.SelectedTheme.Value);
        Assert.Equal("Follow the system", _page.SelectedTheme.Label);
    }

    [Fact]
    public void ChoosingATheme_UpdatesTheSettings()
    {
        _page.SelectedTheme = _page.Themes.Single(option => option.Value == ThemePreference.Light);

        Assert.Equal(ThemePreference.Light, _settings.Current.Theme);
    }

    [Fact]
    public void ANullTheme_FromAListBeingTornDown_ChangesNothing()
    {
        _page.SelectedTheme = null!;

        Assert.Equal(0, _settings.Updates);
    }

    [Fact]
    public void TogglingMinimize_UpdatesTheSettings()
    {
        _page.MinimizeAfterLaunch = true;

        Assert.True(_settings.Current.MinimizeAfterLaunch);
        Assert.True(_page.MinimizeAfterLaunch);
    }

    [Fact]
    public void AChangeMadeElsewhere_IsAnnounced()
    {
        List<string?> changed = [];
        _page.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _settings.Update(current => current with { Theme = ThemePreference.Dark, MinimizeAfterLaunch = true });

        Assert.Contains(nameof(SettingsPageViewModel.SelectedTheme), changed);
        Assert.Contains(nameof(SettingsPageViewModel.MinimizeAfterLaunch), changed);
        Assert.Equal(ThemePreference.Dark, _page.SelectedTheme.Value);
    }

    [Fact]
    public void OpenDataFolder_OpensTheDataDirectory()
    {
        _page.OpenDataFolderCommand.Execute(null);

        Assert.Equal([_data.Path], _actions.OpenedFolders);
        Assert.Equal(_data.Path, _page.DataDirectory);
    }

    [Fact]
    public void Version_IsTheRunningVersion_WithoutBuildMetadata()
    {
        Assert.Equal(ProductInformation.Version, _page.Version);
        Assert.DoesNotContain("+", _page.Version, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAbout_ShowsTheAboutDialog()
    {
        await _page.OpenAboutCommand.ExecuteAsync(null);

        Assert.Equal(1, _about.Shown);
    }
}
