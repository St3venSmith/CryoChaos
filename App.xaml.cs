using System.Windows;
using System.Windows.Threading;
using CryoChaos.Services;

namespace CryoChaos;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        CrashLogService.Initialize();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CrashLogService.Write("SHUTDOWN", $"Normal application exit. Code={e.ApplicationExitCode}");
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        CrashLogService.WriteException(
            "FATAL UI THREAD EXCEPTION",
            e.Exception,
            fatal: true);

        // Preserve normal crash behavior after the log is flushed.
        e.Handled = false;
    }

    private static void OnDomainUnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            CrashLogService.WriteException(
                "FATAL APPDOMAIN EXCEPTION",
                exception,
                fatal: true);
        }
        else
        {
            CrashLogService.Write(
                "FATAL APPDOMAIN EXCEPTION",
                e.ExceptionObject?.ToString() ?? "Unknown exception");
        }
    }

    private static void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        CrashLogService.WriteException(
            "UNOBSERVED TASK EXCEPTION",
            e.Exception);
        e.SetObserved();
    }
}
