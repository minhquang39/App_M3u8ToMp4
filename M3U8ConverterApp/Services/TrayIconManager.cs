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
        
        var loc = LocalizationManager.Instance;
        
        _notifyIcon = new TaskbarIcon
        {
            Icon = new System.Drawing.Icon(Application.GetResourceStream(new Uri("pack://application:,,,/Assets/logo.ico")).Stream),
            ToolTipText = loc["TrayIcon_Title"],
            Visibility = Visibility.Collapsed
        };

        _notifyIcon.TrayLeftMouseDown += OnTrayIconClick;
        
        // Create context menu
        var contextMenu = new System.Windows.Controls.ContextMenu();
        
        var showMenuItem = new System.Windows.Controls.MenuItem { Header = loc["TrayIcon_ShowWindow"] };
        showMenuItem.Click += (s, e) => ShowWindow();
        contextMenu.Items.Add(showMenuItem);
        
        var separator = new System.Windows.Controls.Separator();
        contextMenu.Items.Add(separator);
        
        var exitMenuItem = new System.Windows.Controls.MenuItem { Header = loc["TrayIcon_Exit"] };
        exitMenuItem.Click += (s, e) => ExitApplication();
        contextMenu.Items.Add(exitMenuItem);
        
        _notifyIcon.ContextMenu = contextMenu;
        
        // Subscribe to language changes
        loc.PropertyChanged += (s, e) => UpdateLocalizedStrings();
    }

    private void UpdateLocalizedStrings()
    {
        var loc = LocalizationManager.Instance;
        _notifyIcon.ToolTipText = loc["TrayIcon_Title"];
        
        if (_notifyIcon.ContextMenu != null)
        {
            ((System.Windows.Controls.MenuItem)_notifyIcon.ContextMenu.Items[0]).Header = loc["TrayIcon_ShowWindow"];
            ((System.Windows.Controls.MenuItem)_notifyIcon.ContextMenu.Items[2]).Header = loc["TrayIcon_Exit"];
        }
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
        
        var loc = LocalizationManager.Instance;
        // Show notification
        _notifyIcon.ShowBalloonTip(
            loc["TrayIcon_Title"],
            loc["TrayIcon_Message"],
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
