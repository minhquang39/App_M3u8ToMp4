using System;
using System.Threading.Tasks;
using System.Windows;
using M3U8ConverterApp.Interop;
using M3U8ConverterApp.Services;
using M3U8ConverterApp.ViewModels;

namespace M3U8ConverterApp;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly NativeBridgeServer? _bridgeServer;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel(
            new ConversionService(),
            new DialogService(),
            new SettingsService(),
            new FfmpegLocator());

        DataContext = _viewModel;

        _bridgeServer = new NativeBridgeServer("m3u8_converter_bridge", HandleNativeRequestAsync);
    }

    private async Task<NativeBridgeResponse> HandleNativeRequestAsync(NativeBridgeRequest request)
    {
        if (request is null)
        {
            return NativeBridgeResponse.Failure("Request payload was empty.");
        }

        if (!request.IsEnqueueRequest())
        {
            return NativeBridgeResponse.Failure("Unsupported request type.");
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
        _bridgeServer?.Dispose();
        base.OnClosed(e);
    }
}
