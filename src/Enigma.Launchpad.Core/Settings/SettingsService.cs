using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Persistence;
using Microsoft.Extensions.Logging;

namespace Enigma.Launchpad.Core.Settings;

/// <summary>
/// Default <see cref="ISettingsService"/>: an indented JSON file, <c>settings.json</c>, beside the launchpad in
/// the user's data directory.
/// </summary>
/// <remarks>
/// <para>
/// Saves run one after the other on the thread pool and each writes the <em>latest</em> settings, so the file
/// never goes back in time and a burst of changes costs at most two writes.
/// </para>
/// <para>
/// A file that cannot be read is moved aside to <c>settings.corrupt-&lt;UTC stamp&gt;.json</c> and the defaults
/// are used. A file that cannot be written is only logged: the preference still holds for this session, and a
/// settings file is not worth interrupting the user for.
/// </para>
/// </remarks>
public sealed class SettingsService : ISettingsService
{
    /// <summary>
    /// The settings file's name.
    /// </summary>
    public const string FileName = "settings.json";

    /// <summary>
    /// How the settings are written and read: camelCase, indented, enums by name so the file reads like the
    /// settings page.
    /// </summary>
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly IAppPaths _paths;
    private readonly TimeProvider _time;
    private readonly ILogger<SettingsService> _logger;
    private readonly object _sync = new();

    private AppSettings _current = AppSettings.Defaults;
    private Task _saving = Task.CompletedTask;
    private bool _saveQueued;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="paths">Where the user's data lives.</param>
    /// <param name="time">The clock the name of a moved-aside file is stamped from.</param>
    /// <param name="logger">Where unreadable and unwritable files are reported.</param>
    public SettingsService(IAppPaths paths, TimeProvider time, ILogger<SettingsService> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);

        _paths = paths;
        _time = time;
        _logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler<SettingsChangedEventArgs>? Changed;

    /// <inheritdoc />
    public string FilePath => _paths.GetDataFile(FileName);

    /// <inheritdoc />
    public AppSettings Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        AppSettings loaded = await ReadAsync(cancellationToken).ConfigureAwait(false);

        lock (_sync)
        {
            _current = loaded;
        }
    }

    /// <inheritdoc />
    public AppSettings Update(Func<AppSettings, AppSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        AppSettings updated;

        lock (_sync)
        {
            updated = change(_current).Normalised() with { Version = AppSettings.CurrentVersion };

            if (updated == _current)
            {
                // A setter that assigns the value already there costs neither a write nor a notification.
                return _current;
            }

            _current = updated;

            if (!_saveQueued)
            {
                _saveQueued = true;
                _saving = _saving.ContinueWith(_ => SaveLatestAsync(), TaskScheduler.Default).Unwrap();
            }
        }

        Changed?.Invoke(this, new SettingsChangedEventArgs(updated));

        return updated;
    }

    /// <inheritdoc />
    public Task FlushAsync()
    {
        lock (_sync)
        {
            return _saving;
        }
    }

    /// <summary>
    /// Reads settings the way the service does, refusing anything that is not a settings document.
    /// </summary>
    /// <param name="json">The file's text.</param>
    /// <param name="settings">The settings, normalised, when they were read.</param>
    /// <returns><see langword="true"/> when the settings were read.</returns>
    internal static bool TryParse(string json, out AppSettings settings)
    {
        settings = AppSettings.Defaults;

        try
        {
            if (JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) is not { } parsed)
            {
                return false;
            }

            settings = parsed.Normalised();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<AppSettings> ReadAsync(CancellationToken cancellationToken)
    {
        string path = FilePath;

        if (!File.Exists(path))
        {
            return AppSettings.Defaults;
        }

        string json;

        try
        {
            json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "The settings file {Path} could not be read; the defaults are used", path);
            return AppSettings.Defaults;
        }

        if (TryParse(json, out AppSettings settings))
        {
            return settings;
        }

        MoveAside(path);
        return AppSettings.Defaults;
    }

    private void MoveAside(string path)
    {
        string directory = Path.GetDirectoryName(path) ?? _paths.DataDirectory;
        string stamp = _time.GetUtcNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        string target = Path.Combine(directory, $"settings.corrupt-{stamp}.json");

        for (int suffix = 2; File.Exists(target); suffix++)
        {
            target = Path.Combine(directory, $"settings.corrupt-{stamp}-{suffix}.json");
        }

        try
        {
            File.Move(path, target);
            _logger.LogWarning("The settings file could not be read; it was moved to {MovedTo} and the defaults are used", target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "The settings file {Path} could not be read nor moved aside; the defaults are used", path);
        }
    }

    private async Task SaveLatestAsync()
    {
        AppSettings snapshot;

        lock (_sync)
        {
            _saveQueued = false;
            snapshot = _current;
        }

        try
        {
            string json = JsonSerializer.Serialize(snapshot, SerializerOptions);
            await AtomicFile.WriteAllTextAsync(FilePath, json).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "The settings could not be saved to {Path}", FilePath);
        }
    }
}
