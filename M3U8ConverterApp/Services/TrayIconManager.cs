using System;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;

namespace M3U8ConverterApp.Services;

internal sealed class TrayIconManager : IDisposable
{
    private readonly TaskbarIcon _notifyIcon;
    private readonly Window _window;
    private bool _disposed;

    public TrayIconManager(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        
        _notifyIcon = new TaskbarIcon
        {
            Icon = new System.Drawing.Icon(Application.GetResourceStream(new Uri("pack://application:,,,/Assets/icon.ico")).Stream),
            ToolTipText = "M3U8 to MP4 Converter",
            Visibility = Visibility.Collapsed
        };

        _notifyIcon.TrayLeftMouseDown += OnTrayIconClick;
        
        // Create context menu
        var contextMenu = new System.Windows.Controls.ContextMenu();
        
        var showMenuItem = new System.Windows.Controls.MenuItem { Header = "Show Window" };
        showMenuItem.Click += (s, e) => ShowWindow();
        contextMenu.Items.Add(showMenuItem);
        
        var separator = new System.Windows.Controls.Separator();
        contextMenu.Items.Add(separator);
        
        var exitMenuItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitMenuItem.Click += (s, e) => ExitApplication();
        contextMenu.Items.Add(exitMenuItem);
        
        _notifyIcon.ContextMenu = contextMenu;
    }

    public void ShowTrayIcon()
    {
        _notifyIcon.Visibility = Visibility.Visible;
    }

    public void HideTrayIcon()
    {
        _notifyIcon.Visibility = Visibility.Collapsed;
    }

    public void ShowWindow()
    {
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
        _window.Topmost = true;
        _window.Topmost = false;
        HideTrayIcon();
    }

    public void HideWindow()
    {
        _window.Hide();
        ShowTrayIcon();
        
        // Show notification
        _notifyIcon.ShowBalloonTip(
            "M3U8 to MP4 Converter",
            "Application is still running in the background. Click the tray icon to restore.",
            Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
    }

    private void OnTrayIconClick(object sender, RoutedEventArgs e)
    {
        ShowWindow();
    }

    private void ExitApplication()
    {
        _disposed = true;
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _notifyIcon.Dispose();
    }
}
