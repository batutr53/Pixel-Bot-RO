using System.Globalization;
using System.Windows.Data;

namespace PixelAutomation.Tool.Overlay.WPF.Converters;

public class BoolToStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isRunning)
        {
            return isRunning ? "● RUNNING" : "○ STOPPED";
        }
        return "○ STOPPED";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // This is a one-way converter, ConvertBack is not supported
        // Return DoNothing to indicate that the binding should not be updated
        return System.Windows.Data.Binding.DoNothing;
    }
}

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isRunning)
        {
            return isRunning ? "LimeGreen" : "Gray";
        }
        return "Gray";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // This is a one-way converter, ConvertBack is not supported
        // Return DoNothing to indicate that the binding should not be updated
        return System.Windows.Data.Binding.DoNothing;
    }
}

public class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}