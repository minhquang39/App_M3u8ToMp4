using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace M3U8ConverterApp.Services;

internal sealed class LocalizationManager : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationManager> _instance = new(() => new LocalizationManager());
    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationManager()
    {
        _resourceManager = new ResourceManager("M3U8ConverterApp.Resources.Strings", typeof(LocalizationManager).Assembly);
        _currentCulture = Thread.CurrentThread.CurrentUICulture;
    }

    public static LocalizationManager Instance => _instance.Value;

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (_currentCulture.Equals(value))
            {
                return;
            }

            _currentCulture = value;
            Thread.CurrentThread.CurrentUICulture = value;
            Thread.CurrentThread.CurrentCulture = value;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }

    public string this[string key]
    {
        get
        {
            try
            {
                return _resourceManager.GetString(key, _currentCulture) ?? key;
            }
            catch
            {
                return key;
            }
        }
    }

    public void SetLanguage(string cultureName)
    {
        try
        {
            CurrentCulture = new CultureInfo(cultureName);
        }
        catch
        {
            CurrentCulture = CultureInfo.InvariantCulture;
        }
    }
}
