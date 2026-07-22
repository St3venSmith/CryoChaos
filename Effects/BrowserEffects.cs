using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Effects;

/// <summary>
/// Base for effects that open one random YouTube URL in a dedicated browser
/// app-mode mini-player.
/// Derive from this class and replace VideoUrls to make themed playlists.
/// </summary>
public abstract class RandomYouTubeEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }
    protected abstract IReadOnlyList<string> VideoUrls { get; }

    public async Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string[] validUrls = VideoUrls
            .Where(IsYouTubeUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (validUrls.Length == 0)
        {
            throw new InvalidOperationException(
                "The YouTube effect needs at least one valid youtube.com or youtu.be HTTPS URL.");
        }

        string selected = AddAutoplay(validUrls[context.Random.Next(validUrls.Length)]);
        string browserPath = FindAppModeBrowser() ??
            throw new InvalidOperationException(
                "The YouTube mini-player requires Microsoft Edge or Google Chrome.");

        HashSet<IntPtr> windowsBeforeLaunch = GetBrowserWindows();
        using Process browserProcess = StartAppModeBrowser(browserPath, selected);

        // Keep Destiny active while the new tab loads. A YouTube window title
        // is the best external signal available without browser automation.
        IntPtr browserWindow = IntPtr.Zero;
        for (int index = 0; index < 32; index++)
        {
            await Task.Delay(250, cancellationToken);
            ForegroundWindowService.TryActivateDestinyWindow();
            browserWindow = FindNewBrowserWindow(
                windowsBeforeLaunch,
                browserProcess.HasExited ? null : (uint)browserProcess.Id,
                requireYouTubeTitle: true);
            if (browserWindow != IntPtr.Zero && index >= 3)
            {
                break;
            }
        }

        // Some browsers/media policies need a real activation before playback
        // begins. Prefer the newly loaded YouTube-titled window, focus it very
        // briefly, then return focus to Destiny.
        browserWindow = browserWindow != IntPtr.Zero
            ? browserWindow
            : FindNewBrowserWindow(
                windowsBeforeLaunch,
                browserProcess.HasExited ? null : (uint)browserProcess.Id,
                requireYouTubeTitle: false);
        if (browserWindow != IntPtr.Zero)
        {
            GameMonitorPlacementService.PlaceMiniPlayer(browserWindow);
            TryForceForegroundWindow(browserWindow);
            await Task.Delay(200, cancellationToken);
        }

        foreach (int delay in new[] { 50, 100, 200, 350 })
        {
            await Task.Delay(delay, cancellationToken);
            ForegroundWindowService.TryActivateDestinyWindow();
        }
    }

    private static Process StartAppModeBrowser(string browserPath, string url)
    {
        ProcessStartInfo startInfo = new(browserPath)
        {
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add($"--app={url}");
        startInfo.ArgumentList.Add("--new-window");
        startInfo.ArgumentList.Add("--no-first-run");
        startInfo.ArgumentList.Add("--autoplay-policy=no-user-gesture-required");

        return Process.Start(startInfo) ??
            throw new Win32Exception("The YouTube mini-player browser did not start.");
    }

    private static string? FindAppModeBrowser()
    {
        string programFiles = Environment.GetFolderPath(
            Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(
            Environment.SpecialFolder.ProgramFilesX86);
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        string[] candidates =
        [
            Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe")
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string AddAutoplay(string value)
    {
        UriBuilder builder = new(value);
        string query = builder.Query.TrimStart('?');
        if (!query.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Any(item => item.StartsWith("autoplay=", StringComparison.OrdinalIgnoreCase)))
        {
            builder.Query = string.IsNullOrWhiteSpace(query)
                ? "autoplay=1"
                : $"{query}&autoplay=1";
        }

        return builder.Uri.AbsoluteUri;
    }

    private static bool IsYouTubeUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        string host = uri.Host.TrimStart('.');
        return host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<IntPtr> GetBrowserWindows()
    {
        HashSet<IntPtr> windows = [];
        EnumWindows((window, _) =>
        {
            if (IsWindowVisible(window) && IsBrowserWindow(window))
            {
                windows.Add(window);
            }

            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private static IntPtr FindNewBrowserWindow(
        IReadOnlySet<IntPtr> windowsBeforeLaunch,
        uint? preferredProcessId,
        bool requireYouTubeTitle)
    {
        IntPtr youtubeWindow = IntPtr.Zero;
        IntPtr processWindow = IntPtr.Zero;
        IntPtr newWindow = IntPtr.Zero;

        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window) ||
                !IsBrowserWindow(window) ||
                windowsBeforeLaunch.Contains(window))
            {
                return true;
            }

            string title = GetWindowText(window);
            _ = GetWindowThreadProcessId(window, out uint processId);
            if (newWindow == IntPtr.Zero)
            {
                newWindow = window;
            }

            if (preferredProcessId == processId && processWindow == IntPtr.Zero)
            {
                processWindow = window;
            }

            if (title.Contains("YouTube", StringComparison.OrdinalIgnoreCase))
            {
                youtubeWindow = window;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return youtubeWindow != IntPtr.Zero
            ? youtubeWindow
            : requireYouTubeTitle
                ? IntPtr.Zero
                : processWindow != IntPtr.Zero ? processWindow : newWindow;
    }

    private static bool IsBrowserWindow(IntPtr window)
    {
        string className = GetWindowClass(window);
        return className.Equals("Chrome_WidgetWin_1", StringComparison.OrdinalIgnoreCase) ||
               className.Equals("ApplicationFrameWindow", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryForceForegroundWindow(IntPtr window)
    {
        IntPtr foreground = GetForegroundWindow();
        uint foregroundThread = foreground == IntPtr.Zero
            ? 0
            : GetWindowThreadProcessId(foreground, out _);
        uint currentThread = GetCurrentThreadId();
        bool attached = foregroundThread != 0 &&
            foregroundThread != currentThread &&
            AttachThreadInput(currentThread, foregroundThread, true);

        try
        {
            ShowWindowAsync(window, 9); // SW_RESTORE
            BringWindowToTop(window);
            return SetForegroundWindow(window);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }

    private static string GetWindowText(IntPtr window)
    {
        int length = GetWindowTextLength(window);
        StringBuilder text = new(Math.Max(1, length + 1));
        _ = GetWindowText(window, text, text.Capacity);
        return text.ToString();
    }

    private static string GetWindowClass(IntPtr window)
    {
        StringBuilder text = new(256);
        _ = GetClassName(window, text, text.Capacity);
        return text.ToString();
    }

    private delegate bool EnumWindowsProcedure(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProcedure callback, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximumCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextLengthW")]
    private static extern int GetWindowTextLength(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maximumCount);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint firstThread, uint secondThread, bool attach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr window, int showCommand);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);
}

public sealed class RandomYouTubeVideoEffect : RandomYouTubeEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "random_youtube_video",
        Name = "Unexpected YouTube",
        Description = "Opens one random video in a dedicated mini-player over the game.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 5,
        DurationSeconds = 2,
        CooldownSeconds = 180,
        CanStack = true
    };

    protected override IReadOnlyList<string> VideoUrls { get; } =
    [
        "https://www.youtube.com/watch?v=h92ZrzSeicA&t=43s",
        "https://www.youtube.com/watch?v=Yh64X-kkBSk",
        "https://www.youtube.com/watch?v=rCYo8OLxbRM&t=304s",
        "https://www.youtube.com/watch?v=8hN8cVe3hzU",
        "https://www.youtube.com/watch?v=ic5EFF3FM88",
        "https://www.youtube.com/watch?v=eBjHneKNgrY",
        "https://www.youtube.com/watch?v=CiclmZBqvCA",
        "https://www.youtube.com/watch?v=8cRcAC6k8Tk",
        "https://www.youtube.com/watch?v=CPvpE2BHAUo",
        "https://www.youtube.com/watch?v=pmZY-YDf8M8",
        "https://www.youtube.com/watch?v=gqaYQ1pn4Ok",
        "https://www.youtube.com/watch?v=lxPNtKCyLYw",
        "https://www.youtube.com/watch?v=0sopZhH1s0Y"
    ];
}

[ChaosEffectTemplate]
public sealed class YourRandomYouTubeEffect : RandomYouTubeEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "your_random_youtube",
        Name = "Your YouTube Playlist",
        Description = "Template random YouTube mini-player effect.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 5,
        DurationSeconds = 2,
        CooldownSeconds = 120,
        CanStack = true
    };

    protected override IReadOnlyList<string> VideoUrls { get; } =
    [
        "https://www.youtube.com/watch?v=h92ZrzSeicA&t=43s",
        "https://www.youtube.com/watch?v=Yh64X-kkBSk"
    ];
}
