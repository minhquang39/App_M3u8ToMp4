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
    private bool _autoScrollEnabled = true;

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

    private void OnLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add)
        {
            return;
        }

        // Chỉ auto-scroll nếu enabled
        if (!_autoScrollEnabled)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (ActivityLogList.Items.Count == 0)
            {
                return;
            }

            var lastItem = ActivityLogList.Items[^1];
            ActivityLogList.ScrollIntoView(lastItem);
            
            // Sau khi scroll, check xem user có ở cuối không
            CheckScrollPosition();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void CheckScrollPosition()
    {
        // Tìm ScrollViewer bên trong ListBox
        var scrollViewer = FindVisualChild<ScrollViewer>(ActivityLogList);
        if (scrollViewer != null)
        {
            // Nếu user scroll lên (không ở cuối), tắt auto-scroll
            _autoScrollEnabled = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 1;
        }
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

    private void ActivityLogList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Khi user scroll, check xem còn ở cuối không
        if (e.VerticalChange != 0)
        {
            CheckScrollPosition();
        }
    }

    private void ActivityLogList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Khi user dùng wheel, tạm tắt auto-scroll
        _autoScrollEnabled = false;
        
        // Sau 2 giây không scroll, bật lại auto-scroll nếu ở cuối
        Task.Delay(2000).ContinueWith(_ =>
        {
            Dispatcher.Invoke(() => CheckScrollPosition());
        });
    }

    private void BringToForeground()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;
        Topmost = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel.Logs is INotifyCollectionChanged logs)
        {
            logs.CollectionChanged -= OnLogsCollectionChanged;
        }

        _bridgeServer?.Dispose();
        base.OnClosed(e);
    }
}
