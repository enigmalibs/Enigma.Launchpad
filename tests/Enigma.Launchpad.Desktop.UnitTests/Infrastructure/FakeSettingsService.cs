using System;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Settings;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// Settings kept in memory, announcing changes the way the real service does.
/// </summary>
internal sealed class FakeSettingsService : ISettingsService
{
    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public string FilePath => "settings.json";

    public AppSettings Current { get; private set; } = AppSettings.Defaults;

    public int Updates { get; private set; }

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public AppSettings Update(Func<AppSettings, AppSettings> change)
    {
        AppSettings updated = change(Current).Normalised();

        if (updated == Current)
        {
            return Current;
        }

        Current = updated;
        Updates++;
        Changed?.Invoke(this, new SettingsChangedEventArgs(updated));
        return updated;
    }

    public Task FlushAsync() => Task.CompletedTask;
}
