using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using CryoChaos.Models;

namespace CryoChaos.Services;

public sealed class DestinyKeybindService : IDisposable
{
    private const string CvarsFileName = "cvars.xml";

    private readonly Dictionary<string, DestinyBinding> _bindings =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _bindingsLock = new();
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _reloadDebounce;

    public static string DefaultCvarsPath => Path.GetFullPath(
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData),
            "Bungie",
            "DestinyPC",
            "prefs",
            CvarsFileName));

    public ObservableCollection<DestinyBinding> DisplayBindings { get; } = [];

    public event EventHandler? BindingsChanged;
    public event EventHandler<string>? LoadFailed;

    public void StartWatching()
    {
        Reload();

        string? directory = Path.GetDirectoryName(DefaultCvarsPath);

        if (directory is null || !Directory.Exists(directory))
        {
            LoadFailed?.Invoke(
                this,
                $"Destiny preferences folder was not found: {directory}");
            return;
        }

        StopWatcher();

        _watcher = new FileSystemWatcher(directory)
        {
            Filter = "*",
            NotifyFilter =
                NotifyFilters.LastWrite |
                NotifyFilters.Size |
                NotifyFilters.FileName |
                NotifyFilters.CreationTime
        };

        _watcher.Changed += FileChanged;
        _watcher.Created += FileChanged;
        _watcher.Deleted += FileChanged;
        _watcher.Renamed += FileRenamed;
        _watcher.EnableRaisingEvents = true;
    }

    public void Reload()
    {
        try
        {
            ReplaceBindings(ReadBindings(DefaultCvarsPath));
        }
        catch (FileNotFoundException ex)
        {
            ClearBindings();
            LoadFailed?.Invoke(this, ex.Message);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            XmlException)
        {
            LoadFailed?.Invoke(this, ex.Message);
        }
    }

    public IReadOnlyList<InputBinding> ResolveActionBindings(
        params string[] aliases)
    {
        if (aliases is null || aliases.Length == 0)
        {
            return [];
        }

        DestinyBinding? action = FindAction(aliases);
        if (action is null)
        {
            return [];
        }

        List<InputBinding> results = [];
        AddIfUsable(action.Primary);
        AddIfUsable(action.Secondary);
        return results;

        void AddIfUsable(InputBinding? candidate)
        {
            if (candidate is null ||
                candidate.Kind == InputBindingKind.Unknown)
            {
                return;
            }

            bool duplicate = results.Any(existing =>
                BindingsAreEquivalent(existing, candidate));

            if (!duplicate)
            {
                results.Add(candidate);
            }
        }
    }

    public InputBinding? ResolveAction(params string[] aliases) =>
        ResolveActionBindings(aliases).FirstOrDefault();

    public InputBinding? ResolveActionForSimulation(
        params string[] aliases) =>
        ResolveActionBindings(aliases).FirstOrDefault();

    private DestinyBinding? FindAction(IReadOnlyList<string> aliases)
    {
        lock (_bindingsLock)
        {
            foreach (string alias in aliases)
            {
                if (_bindings.TryGetValue(
                        alias,
                        out DestinyBinding? exact))
                {
                    return exact;
                }
            }

            foreach (DestinyBinding binding in _bindings.Values)
            {
                string normalizedAction = NormalizeAction(binding.Action);

                foreach (string alias in aliases)
                {
                    string normalizedAlias = NormalizeAction(alias);
                    if (normalizedAlias.Length == 0)
                    {
                        continue;
                    }

                    if (normalizedAction.Equals(
                            normalizedAlias,
                            StringComparison.OrdinalIgnoreCase) ||
                        normalizedAction.Contains(
                            normalizedAlias,
                            StringComparison.OrdinalIgnoreCase) ||
                        normalizedAlias.Contains(
                            normalizedAction,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return binding;
                    }
                }
            }
        }

        return null;
    }

    private static bool BindingsAreEquivalent(
        InputBinding first,
        InputBinding second)
    {
        return first.Kind == second.Kind &&
               first.VirtualKey == second.VirtualKey &&
               first.MouseButton == second.MouseButton &&
               first.WheelDirection == second.WheelDirection;
    }

    private static string NormalizeAction(string action)
    {
        return new string(
            action
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
    }

    private static IReadOnlyList<DestinyBinding> ReadBindings(string path)
    {
        string fullPath = Path.GetFullPath(path);

        if (!string.Equals(
                Path.GetFileName(fullPath),
                CvarsFileName,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                fullPath,
                DefaultCvarsPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Only the live {CvarsFileName} file may be read.");
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Destiny 2 {CvarsFileName} was not found.",
                fullPath);
        }

        XDocument document;

        using (FileStream stream = new(
                   fullPath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete))
        {
            document = XDocument.Load(stream);
        }

        List<DestinyBinding> results = [];

        foreach (XElement element in document.Descendants("cvar"))
        {
            string? action = element.Attribute("name")?.Value;
            string? value = element.Attribute("value")?.Value;

            if (string.IsNullOrWhiteSpace(action) ||
                string.IsNullOrWhiteSpace(value) ||
                !value.Contains('!'))
            {
                continue;
            }

            string[] parts = value.Split(
                '!',
                StringSplitOptions.TrimEntries);

            string? primaryRaw = NormalizeRaw(parts.ElementAtOrDefault(0));
            string? secondaryRaw = NormalizeRaw(parts.ElementAtOrDefault(1));

            InputBinding? primary = InputBindingParser.Parse(primaryRaw);
            InputBinding? secondary = InputBindingParser.Parse(secondaryRaw);

            bool hasUsableInput =
                primary is { Kind: not InputBindingKind.Unknown } ||
                secondary is { Kind: not InputBindingKind.Unknown };

            if (!hasUsableInput)
            {
                continue;
            }

            results.Add(new DestinyBinding
            {
                Action = action,
                PrimaryRaw = primaryRaw,
                SecondaryRaw = secondaryRaw,
                Primary = primary,
                Secondary = secondary
            });
        }

        return results;
    }

    private static string? NormalizeRaw(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) ||
            raw.Equals("unused", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return raw.Trim();
    }

    private void ReplaceBindings(IReadOnlyList<DestinyBinding> loaded)
    {
        lock (_bindingsLock)
        {
            _bindings.Clear();

            foreach (DestinyBinding binding in loaded)
            {
                _bindings[binding.Action] = binding;
            }
        }

        RunOnUiThread(() =>
        {
            DisplayBindings.Clear();

            foreach (DestinyBinding binding in
                     loaded.OrderBy(item => item.Action))
            {
                DisplayBindings.Add(binding);
            }
        });

        BindingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearBindings()
    {
        lock (_bindingsLock)
        {
            _bindings.Clear();
        }

        RunOnUiThread(DisplayBindings.Clear);
        BindingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void RunOnUiThread(Action action)
    {
        System.Windows.Threading.Dispatcher? dispatcher =
            Application.Current?.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }

    private void FileChanged(object sender, FileSystemEventArgs e)
    {
        if (IsLiveCvarsFile(e.FullPath))
        {
            ScheduleReload();
        }
    }

    private void FileRenamed(object sender, RenamedEventArgs e)
    {
        if (IsLiveCvarsFile(e.OldFullPath) ||
            IsLiveCvarsFile(e.FullPath))
        {
            ScheduleReload();
        }
    }

    private static bool IsLiveCvarsFile(string path)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(path),
                DefaultCvarsPath,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void ScheduleReload()
    {
        _reloadDebounce?.Cancel();
        _reloadDebounce?.Dispose();
        _reloadDebounce = new CancellationTokenSource();
        _ = ReloadWithRetryAsync(_reloadDebounce.Token);
    }

    private async Task ReloadWithRetryAsync(
        CancellationToken cancellationToken)
    {
        const int maximumAttempts = 10;

        for (int attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            try
            {
                await Task.Delay(
                    attempt == 1 ? 500 : 250,
                    cancellationToken);

                ReplaceBindings(ReadBindings(DefaultCvarsPath));
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (
                ex is FileNotFoundException or
                IOException or
                UnauthorizedAccessException or
                XmlException)
            {
                if (attempt == maximumAttempts)
                {
                    ClearBindings();
                    LoadFailed?.Invoke(
                        this,
                        $"Could not read the live {CvarsFileName}: {ex.Message}");
                }
            }
        }
    }

    private void StopWatcher()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= FileChanged;
        _watcher.Created -= FileChanged;
        _watcher.Deleted -= FileChanged;
        _watcher.Renamed -= FileRenamed;
        _watcher.Dispose();
        _watcher = null;
    }

    public void Dispose()
    {
        _reloadDebounce?.Cancel();
        _reloadDebounce?.Dispose();
        _reloadDebounce = null;
        StopWatcher();
    }
}
