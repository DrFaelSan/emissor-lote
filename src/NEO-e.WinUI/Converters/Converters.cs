using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace NEO_e.WinUI.Converters;

public enum LogLevel
{
    Info,
    Warning,
    Error,
    Success
}

public sealed record LogMessage(string Message, LogLevel Level);

public sealed class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Info => Microsoft.UI.Xaml.Application.Current?.Resources["LogInfoBrush"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.Black),
                LogLevel.Warning => Microsoft.UI.Xaml.Application.Current?.Resources["LogWarningBrush"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.DarkOrange),
                LogLevel.Error => Microsoft.UI.Xaml.Application.Current?.Resources["LogErrorBrush"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.Red),
                LogLevel.Success => Microsoft.UI.Xaml.Application.Current?.Resources["LogSuccessBrush"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.Green),
                _ => Microsoft.UI.Xaml.Application.Current?.Resources["LogInfoBrush"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.Black)
            };
        }
        return Microsoft.UI.Xaml.Application.Current?.Resources["LogInfoBrush"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
            return boolValue ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        return Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is int count && count == 0
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}