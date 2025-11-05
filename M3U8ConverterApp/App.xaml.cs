using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace M3U8ConverterApp;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        HandleException(e.Exception, "UI thread exception");
        e.Handled = true;
        Shutdown(-1);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            HandleException(ex, "Unhandled exception");
        }
        else
        {
            HandleException(new Exception(e.ExceptionObject?.ToString() ?? "Unknown error"), "Unhandled exception");
        }

        Shutdown(-1);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        HandleException(e.Exception, "Background task exception");
        e.SetObserved();
    }

    private static void HandleException(Exception exception, string context)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "error.log");
            var builder = new StringBuilder();
            builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}");
            builder.AppendLine(exception.ToString());
            builder.AppendLine();
            File.AppendAllText(logPath, builder.ToString());
        }
        catch
        {
            // Ignore logging failures.
        }

        try
        {
            MessageBox.Show(
                $"{context}:{Environment.NewLine}{exception.Message}",
                "M3U8 Converter - Unexpected Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // Ignore UI failures, likely due to shutdown state.
        }
    }
}
