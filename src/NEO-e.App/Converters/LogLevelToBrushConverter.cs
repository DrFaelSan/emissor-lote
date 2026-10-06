using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NEO_e.App.Converters;

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
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Info => App.Current?.FindResource("LogInfoBrush") as Brush ?? Brushes.Black,
                LogLevel.Warning => App.Current?.FindResource("LogWarningBrush") as Brush ?? Brushes.DarkOrange,
                LogLevel.Error => App.Current?.FindResource("LogErrorBrush") as Brush ?? Brushes.Red,
                LogLevel.Success => App.Current?.FindResource("LogSuccessBrush") as Brush ?? Brushes.Green,
                _ => App.Current?.FindResource("LogInfoBrush") as Brush ?? Brushes.Black
            };
        }
        return App.Current?.FindResource("LogInfoBrush") as Brush ?? Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}