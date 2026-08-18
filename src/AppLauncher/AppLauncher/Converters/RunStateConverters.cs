using System.Globalization;
using AppLauncher.Models;

namespace AppLauncher.Converters;

public sealed class RunStateToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        RunState state = value is RunState runState ? runState : RunState.Idle;

        switch (state)
        {
            case RunState.Starting:
            case RunState.Stopping:
                return Color.FromArgb("#D9A441");
            case RunState.Running:
                return Color.FromArgb("#3FB950");
            case RunState.Failed:
                return Color.FromArgb("#E5534B");
            case RunState.Exited:
                return Color.FromArgb("#5A626E");
            default:
                return Color.FromArgb("#3A414C");
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class BoolToColorConverter : IValueConverter
{
    public Color TrueColor { get; set; } = Colors.Transparent;

    public Color FalseColor { get; set; } = Colors.Transparent;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool flag && flag)
        {
            return this.TrueColor;
        }

        return this.FalseColor;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool flag)
        {
            return !flag;
        }

        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool flag)
        {
            return !flag;
        }

        return true;
    }
}
