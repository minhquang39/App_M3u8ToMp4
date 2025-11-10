using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using M3U8ConverterApp.Models;
using M3U8ConverterApp.Services;

namespace M3U8ConverterApp.ViewModels;

internal sealed class MainWindowViewModel : BaseViewModel
{
    internal sealed record EngineOption(DownloadEngine Engine, string DisplayName, string Description);
    private const int MaxLogEntries = 400;

    private readonly IConversionService _conversionService;
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;
    private readonly IFfmpegLocator _ffmpegLocator;
    private readonly AppSettings _settings;

    private CancellationTokenSource? _conversionCts;

    private string _sourceUrl = string.Empty;
    private string _outputPath = string.Empty;
    private string _ffmpegPath = string.Empty;
    private string _nm3u8DlRePath = string.Empty;
    private string _statusMessage = "Idle";
    private double _progressPercent;
    private bool _isBusy;
    private string _progressDetails = string.Empty;
    private bool _useAggressiveHttp;
    private int _nm3u8ThreadCount = 16;
    private EngineOption _selectedEngineOption;

    public MainWindowViewModel(IConversionService conversionService, IDialogService dialogService, ISettingsService settingsService, IFfmpegLocator ffmpegLocator)
    {
        _conversionService = conversionService;
        _dialogService = dialogService;
        _settingsService = settingsService;
        _ffmpegLocator = ffmpegLocator;

        EngineOptions = new[]
        {
            new EngineOption(DownloadEngine.Ffmpeg, "FFmpeg (sequential)", "Use ffmpeg to download sequentially."),
            new EngineOption(DownloadEngine.Nm3u8DlRe, "N_m3u8DL-RE (parallel)", "Use N_m3u8DL-RE to download segments concurrently.")
        };

        _selectedEngineOption = EngineOptions[0];

        Logs = new ObservableCollection<string>();

        StartCommand = new AsyncRelayCommand(StartConversionAsync, CanStartConversion);
        CancelCommand = new RelayCommand(CancelConversion, () => IsBusy);
        BrowseFfmpegCommand = new RelayCommand(BrowseForFfmpeg);
        BrowseNm3u8Command = new RelayCommand(BrowseForNm3u8);
        BrowseOutputCommand = new RelayCommand(BrowseForOutput);
        OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder);
        PasteUrlCommand = new RelayCommand(PasteSourceUrl);

        _settings = _settingsService.Load();
        InitializeFromSettings();
    }

    public ObservableCollection<string> Logs { get; }
    public IReadOnlyList<EngineOption> EngineOptions { get; }

    public ICommand StartCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BrowseFfmpegCommand { get; }
    public ICommand BrowseNm3u8Command { get; }
    public ICommand BrowseOutputCommand { get; }
    public ICommand OpenOutputFolderCommand { get; }
    public ICommand PasteUrlCommand { get; }

    public EngineOption SelectedEngineOption
    {
        get => _selectedEngineOption;
        set
        {
            if (value is null)
            {
                return;
            }

            if (SetProperty(ref _selectedEngineOption, value))
            {
                OnPropertyChanged(nameof(IsFfmpegSelected));
                OnPropertyChanged(nameof(IsNm3u8DlReSelected));
                ((AsyncRelayCommand)StartCommand).RaiseCanExecuteChanged();

                if (!IsFfmpegSelected && UseAggressiveHttp)
                {
                    UseAggressiveHttp = false;
                }
            }
        }
    }

    public bool IsFfmpegSelected => SelectedEngineOption.Engine == DownloadEngine.Ffmpeg;
    public bool IsNm3u8DlReSelected => SelectedEngineOption.Engine == DownloadEngine.Nm3u8DlRe;

    public string SourceUrl
    {
        get => _sourceUrl;
        set
        {
            if (SetProperty(ref _sourceUrl, value))
            {
                SuggestOutputPath();
                ((AsyncRelayCommand)StartCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (SetProperty(ref _outputPath, value))
            {
                ((AsyncRelayCommand)StartCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string FfmpegPath
    {
        get => _ffmpegPath;
        set
        {
            if (SetProperty(ref _ffmpegPath, value))
            {
                ((AsyncRelayCommand)StartCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string Nm3u8DlRePath
    {
        get => _nm3u8DlRePath;
        set
        {
            if (SetProperty(ref _nm3u8DlRePath, value))
            {
                ((AsyncRelayCommand)StartCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool UseAggressiveHttp
    {
        get => _useAggressiveHttp;
        set => SetProperty(ref _useAggressiveHttp, value);
    }

    public int Nm3u8ThreadCount
    {
        get => _nm3u8ThreadCount;
        set
        {
            var sanitized = Math.Clamp(value, 1, 128);
            SetProperty(ref _nm3u8ThreadCount, sanitized);
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ProgressDetails
    {
        get => _progressDetails;
        private set => SetProperty(ref _progressDetails, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ((AsyncRelayCommand)StartCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
            }
        }
    }

    private void InitializeFromSettings()
    {
        FfmpegPath = !string.IsNullOrWhiteSpace(_settings.FfmpegPath)
            ? _settings.FfmpegPath
            : _ffmpegLocator.TryFind() ?? string.Empty;

        Nm3u8DlRePath = _settings.Nm3u8DlRePath ?? string.Empty;
        Nm3u8ThreadCount = _settings.Nm3u8ThreadCount > 0 ? _settings.Nm3u8ThreadCount : _nm3u8ThreadCount;

        if (!string.IsNullOrWhiteSpace(_settings.LastOutputDirectory))
        {
            var suggestedName = BuildDefaultFileName(SourceUrl);
            OutputPath = Path.Combine(_settings.LastOutputDirectory!, suggestedName);
        }
        else
        {
            SuggestOutputPath();
        }

        UseAggressiveHttp = _settings.UseAggressiveHttp;

        SelectedEngineOption = EngineOptions.FirstOrDefault(option => option.Engine == _settings.PreferredEngine)
                                ?? EngineOptions[0];
    }

    private void SuggestOutputPath()
    {
        if (!string.IsNullOrWhiteSpace(OutputPath))
        {
            return;
        }

        var directory = _settings.LastOutputDirectory;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            directory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        }

        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        var fileName = BuildDefaultFileName(SourceUrl);
        OutputPath = Path.Combine(directory ?? string.Empty, fileName);
    }

    private static string BuildDefaultFileName(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return $"video_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
        }

        try
        {
            var uri = new Uri(url);
            var name = Path.GetFileNameWithoutExtension(uri.LocalPath);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"video_{DateTime.Now:yyyyMMdd_HHmmss}";
            }

            return $"{Sanitize(name)}.mp4";
        }
        catch
        {
            return $"video_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
        }

        static string Sanitize(string value)
        {
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidChar, '_');
            }
            return value;
        }
    }

    private bool CanStartConversion()
    {
        if (IsBusy)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SourceUrl) || string.IsNullOrWhiteSpace(OutputPath))
        {
            return false;
        }

        return SelectedEngineOption.Engine switch
        {
            DownloadEngine.Ffmpeg => !string.IsNullOrWhiteSpace(FfmpegPath),
            DownloadEngine.Nm3u8DlRe => !string.IsNullOrWhiteSpace(Nm3u8DlRePath) && !string.IsNullOrWhiteSpace(FfmpegPath),
            _ => false
        };
    }

    private async Task StartConversionAsync()
    {
        if (!ValidateInputs(out var validationError))
        {
            AppendLog(validationError);
            StatusMessage = "Validation failed";
            return;
        }

        IsBusy = true;
        StatusMessage = "Starting conversion...";
        ProgressDetails = string.Empty;
        ProgressPercent = 0;
        Logs.Clear();

        var outputDirectory = Path.GetDirectoryName(OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            _settings.LastOutputDirectory = outputDirectory;
        }
        _settings.FfmpegPath = string.IsNullOrWhiteSpace(FfmpegPath) ? null : FfmpegPath;
        _settings.Nm3u8DlRePath = string.IsNullOrWhiteSpace(Nm3u8DlRePath) ? null : Nm3u8DlRePath;
        _settings.UseAggressiveHttp = UseAggressiveHttp;
        _settings.Nm3u8ThreadCount = Nm3u8ThreadCount;
        _settings.PreferredEngine = SelectedEngineOption.Engine;
        _settingsService.Save(_settings);

        _conversionCts = new CancellationTokenSource();
        var progress = new Progress<ConversionProgress>(OnProgressReport);

        try
        {
            AppendLog($"{SelectedEngineOption.DisplayName} conversion started.");
            await _conversionService.ConvertAsync(
                new ConversionRequest(
                    SelectedEngineOption.Engine,
                    string.IsNullOrWhiteSpace(FfmpegPath) ? null : FfmpegPath,
                    SourceUrl.Trim(),
                    OutputPath.Trim(),
                    SelectedEngineOption.Engine == DownloadEngine.Ffmpeg && UseAggressiveHttp,
                    string.IsNullOrWhiteSpace(Nm3u8DlRePath) ? null : Nm3u8DlRePath,
                    SelectedEngineOption.Engine == DownloadEngine.Nm3u8DlRe ? Nm3u8ThreadCount : null),
                progress,
                _conversionCts.Token).ConfigureAwait(true);

            AppendLog("Conversion completed successfully.");
            StatusMessage = "Completed";
            ProgressPercent = 100;
        }
        catch (OperationCanceledException)
        {
            TryDeletePartialOutput();
            AppendLog("Conversion cancelled by user.");
            StatusMessage = "Cancelled";
        }
        catch (Exception ex)
        {
            AppendLog($"Error: {ex.Message}");
            StatusMessage = "Failed";
        }
        finally
        {
            _conversionCts?.Dispose();
            _conversionCts = null;
            IsBusy = false;
        }
    }

    private void TryDeletePartialOutput()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(OutputPath) && File.Exists(OutputPath))
            {
                File.Delete(OutputPath);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Warning: unable to delete partial file - {ex.Message}");
        }
    }

    private void OnProgressReport(ConversionProgress progress)
    {
        AppendLog(progress.Message);

        ProgressPercent = progress.Percentage ?? ProgressPercent;

        if (progress.TotalDuration.HasValue && progress.ProcessedDuration.HasValue)
        {
            ProgressDetails = $"{FormatTime(progress.ProcessedDuration.Value)} / {FormatTime(progress.TotalDuration.Value)}";
        }
        else if (progress.ProcessedDuration.HasValue)
        {
            ProgressDetails = $"{FormatTime(progress.ProcessedDuration.Value)} processed";
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }

    private void CancelConversion()
    {
        if (!IsBusy)
        {
            return;
        }

        _conversionCts?.Cancel();
    }

    private void BrowseForFfmpeg()
    {
        var selected = _dialogService.BrowseForFfmpeg();
        if (!string.IsNullOrWhiteSpace(selected))
        {
            FfmpegPath = selected;
        }
    }

    private void BrowseForNm3u8()
    {
        var selected = _dialogService.BrowseForNm3u8DlRe();
        if (!string.IsNullOrWhiteSpace(selected))
        {
            Nm3u8DlRePath = selected;
        }
    }

    private void PasteSourceUrl()
    {
        try
        {
            if (!Clipboard.ContainsText())
            {
                return;
            }

            var text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            SourceUrl = text.Trim();
        }
        catch (Exception ex)
        {
            AppendLog($"Unable to paste from clipboard: {ex.Message}");
        }
    }

    private void BrowseForOutput()
    {
        var directory = Path.GetDirectoryName(OutputPath) ?? string.Empty;
        var defaultName = Path.GetFileName(OutputPath);
        var selected = _dialogService.BrowseForOutput(directory, defaultName);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            OutputPath = selected;
        }
    }

    private void OpenOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            return;
        }

        var pathToOpen = File.Exists(OutputPath)
            ? $"/select,\"{OutputPath}\""
            : $"\"{Path.GetDirectoryName(OutputPath) ?? OutputPath}\"";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = pathToOpen,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppendLog($"Unable to open folder: {ex.Message}");
        }
    }

    private bool ValidateInputs(out string message)
    {
        if (string.IsNullOrWhiteSpace(SourceUrl))
        {
            message = "Source URL is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            message = "Output file path is required.";
            return false;
        }

        switch (SelectedEngineOption.Engine)
        {
            case DownloadEngine.Ffmpeg:
                if (string.IsNullOrWhiteSpace(FfmpegPath) || !File.Exists(FfmpegPath))
                {
                    message = "A valid ffmpeg executable path is required.";
                    return false;
                }
                break;
            case DownloadEngine.Nm3u8DlRe:
                if (string.IsNullOrWhiteSpace(Nm3u8DlRePath) || !File.Exists(Nm3u8DlRePath))
                {
                    message = "A valid N_m3u8DL-RE executable path is required.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(FfmpegPath) || !File.Exists(FfmpegPath))
                {
                    message = "FFmpeg is required to remux the downloaded stream into MP4.";
                    return false;
                }
                break;
            default:
                message = "Unsupported download engine.";
                return false;
        }

        var directory = Path.GetDirectoryName(OutputPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                message = $"Unable to create output directory: {ex.Message}";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }

    public void ApplyExternalLink(string url, string? tabTitle, string? pageUrl, long? detectedAt)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var trimmedUrl = url.Trim();
        OutputPath = string.Empty;
        SourceUrl = trimmedUrl;

        var display = !string.IsNullOrWhiteSpace(tabTitle)
            ? tabTitle
            : (!string.IsNullOrWhiteSpace(pageUrl) ? pageUrl : trimmedUrl);

        StatusMessage = $"Link received from browser: {display}";

        var captureLabel = "Captured externally";
        if (detectedAt.HasValue && detectedAt.Value > 0)
        {
            try
            {
                var captured = DateTimeOffset.FromUnixTimeMilliseconds(detectedAt.Value).LocalDateTime;
                captureLabel = $"Captured at {captured:g}";
            }
            catch
            {
                captureLabel = "Captured externally";
            }
        }

        ProgressDetails = captureLabel;

        AppendLog($"Link received from browser extension: {trimmedUrl}");

        _settingsService.Save(_settings);
    }

    private void AppendLog(string message)
    {
        if (Application.Current?.Dispatcher.CheckAccess() == false)
        {
            Application.Current.Dispatcher.Invoke(() => AppendLog(message));
            return;
        }

        if (Logs.Count >= MaxLogEntries)
        {
            Logs.RemoveAt(0);
        }

        Logs.Add($"{DateTime.Now:HH:mm:ss} {message}");
    }

}
