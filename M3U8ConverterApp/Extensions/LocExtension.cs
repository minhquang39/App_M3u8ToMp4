using System;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;
using M3U8ConverterApp.Services;

namespace M3U8ConverterApp.Extensions;

[MarkupExtensionReturnType(typeof(BindingExpression))]
internal sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; }

    public LocExtension()
    {
        Key = string.Empty;
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationManager.Instance,
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
