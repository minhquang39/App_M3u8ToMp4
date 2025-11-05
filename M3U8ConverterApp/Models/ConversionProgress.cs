using System;

namespace M3U8ConverterApp.Models;

internal sealed record ConversionProgress(
    string Message,
    TimeSpan? TotalDuration,
    TimeSpan? ProcessedDuration,
    double? Percentage);
