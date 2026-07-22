using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace CryoChaos.Services;

public static class CrashLogService
{
    private static readonly object Sync = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CryoChaos",
        "Logs");

    private static string SessionLogPath = string.Empty;

    public static string DirectoryPath => LogDirectory;

    public static void Initialize()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            SessionLogPath = Path.Combine(
                LogDirectory,
                $"session-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");

            Write(
                "STARTUP",
                $"CryoChaos started. Version={GetVersion()}, " +
                $"Runtime={RuntimeInformation.FrameworkDescription}, " +
                $"OS={RuntimeInformation.OSDescription}, " +
                $"Architecture={RuntimeInformation.ProcessArchitecture}");

            PruneOldLogs();
        }
        catch
        {
            // Logging must never become another crash source.
        }
    }

    public static void Write(string category, string message)
    {
        try
        {
            lock (Sync)
            {
                EnsureInitialized();
                File.AppendAllText(
                    SessionLogPath,
                    FormatEntry(category, message, null),
                    Encoding.UTF8);
            }
        }
        catch
        {
        }
    }

    public static void WriteException(
        string category,
        Exception exception,
        bool fatal = false)
    {
        try
        {
            lock (Sync)
            {
                EnsureInitialized();
                string entry = FormatEntry(category, exception.Message, exception);
                File.AppendAllText(SessionLogPath, entry, Encoding.UTF8);

                if (fatal)
                {
                    string crashPath = Path.Combine(
                        LogDirectory,
                        $"crash-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Environment.ProcessId}.log");
                    File.WriteAllText(crashPath, entry, Encoding.UTF8);
                }
            }
        }
        catch
        {
        }
    }

    private static string FormatEntry(
        string category,
        string message,
        Exception? exception)
    {
        StringBuilder text = new();
        text.AppendLine("============================================================");
        text.AppendLine($"Time: {DateTimeOffset.Now:O}");
        text.AppendLine($"Category: {category}");
        text.AppendLine($"Process: {Environment.ProcessId}");
        text.AppendLine($"Thread: {Environment.CurrentManagedThreadId}");
        text.AppendLine($"Message: {message}");

        if (exception is not null)
        {
            text.AppendLine("Exception:");
            text.AppendLine(exception.ToString());
        }

        text.AppendLine();
        return text.ToString();
    }

    private static void EnsureInitialized()
    {
        Directory.CreateDirectory(LogDirectory);
        if (string.IsNullOrWhiteSpace(SessionLogPath))
        {
            SessionLogPath = Path.Combine(
                LogDirectory,
                $"session-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");
        }
    }

    private static string GetVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ??
        "unknown";

    private static void PruneOldLogs()
    {
        foreach (FileInfo file in new DirectoryInfo(LogDirectory)
                     .EnumerateFiles("*.log")
                     .OrderByDescending(file => file.CreationTimeUtc)
                     .Skip(20))
        {
            try
            {
                file.Delete();
            }
            catch
            {
            }
        }
    }
}
