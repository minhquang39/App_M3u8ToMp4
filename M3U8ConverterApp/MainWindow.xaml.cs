using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using M3U8ConverterApp.Interop;
using M3U8ConverterApp.Services;
using M3U8ConverterApp.ViewModels;

namespace M3U8ConverterApp;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly NativeBridgeServer? _bridgeServer;
    private readonly TrayIconManager _trayIconManager;
    private bool _autoScrollEnabled = true;
    private bool _isExiting = false;

    public MainWindow() : this(NativeBridgeServer.DefaultPipeName)
    {
    }

    public MainWindow(string pipeName)
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel(
            new ConversionService(),
            new DialogService(),
            new SettingsService(),
            new FfmpegLocator());

        DataContext = _viewModel;

        if (_viewModel.Logs is INotifyCollectionChanged logs)
        {
            logs.CollectionChanged += OnLogsCollectionChanged;
        }

        var pipe = string.IsNullOrWhiteSpace(pipeName) ? NativeBridgeServer.DefaultPipeName : pipeName;
        _bridgeServer = new NativeBridgeServer(pipe, HandleNativeRequestAsync);

        _trayIconManager = new TrayIconManager(this);
        
        // Handle window closing event
        Closing += OnWindowClosing;
    }

    private async Task<NativeBridgeResponse> HandleNativeRequestAsync(NativeBridgeRequest request)
    {
        if (request is null)
        {
            return NativeBridgeResponse.Failure("Request payload was empty.");
        }

        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return NativeBridgeResponse.Failure("URL is required.");
        }

        await Dispatcher.InvokeAsync(() =>
        {
            _viewModel.ApplyExternalLink(
                request.Url!,
                request.TabTitle,
                request.PageUrl,
                request.DetectedAt);
            BringToForeground();
        });

        return NativeBridgeResponse.Success("Link applied.");
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            _trayIconManager.HideWindow();
        }
    }

    private void OnLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || !_autoScrollEnabled)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (ActivityLogList.Items.Count > 0)
            {
                ActivityLogList.ScrollIntoView(ActivityLogList.Items[^1]);
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ActivityLogList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange == 0) return;

        var scrollViewer = FindVisualChild<ScrollViewer>(ActivityLogList);
        if (scrollViewer != null)
        {
            _autoScrollEnabled = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 1;
        }
    }

    private void ActivityLogList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _autoScrollEnabled = false;
        Task.Delay(2000).ContinueWith(_ =>
        {
            Dispatcher.Invoke(() =>
            {
                var scrollViewer = FindVisualChild<ScrollViewer>(ActivityLogList);
                if (scrollViewer != null)
                {
                    _autoScrollEnabled = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 1;
                }
            });
        });
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;

            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }
        return null;
    }

    private void BringToForeground()
    {
        if (!IsVisible)
        {
            _trayIconManager.ShowWindow();
            return;
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;
        Topmost = false;
    }

    public void ExitApplication()
    {
        _isExiting = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel.Logs is INotifyCollectionChanged logs)
        {
            logs.CollectionChanged -= OnLogsCollectionChanged;
        }

        _trayIconManager?.Dispose();
        _bridgeServer?.Dispose();
        base.OnClosed(e);
    }
}
