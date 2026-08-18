using System.Globalization;
using AppLauncher.Models;

namespace AppLauncher.Converters;

public sealed class DiffKindToBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        DiffLineKind kind = value is DiffLineKind diffKind ? diffKind : DiffLineKind.Context;

        switch (kind)
        {
            case DiffLineKind.Added:
                return Color.FromArgb("#0E2A18");
            case DiffLineKind.Removed:
                return Color.FromArgb("#2E1417");
            case DiffLineKind.Hunk:
                return Color.FromArgb("#151C2B");
            case DiffLineKind.Filler:
                return Color.FromArgb("#12141A");
            default:
                return Colors.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class DiffKindToGutterConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        DiffLineKind kind = value is DiffLineKind diffKind ? diffKind : DiffLineKind.Context;

        switch (kind)
        {
            case DiffLineKind.Added:
                return Color.FromArgb("#12351E");
            case DiffLineKind.Removed:
                return Color.FromArgb("#3B171B");
            case DiffLineKind.Hunk:
                return Color.FromArgb("#151C2B");
            case DiffLineKind.Filler:
                return Color.FromArgb("#12141A");
            default:
                return Colors.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class DiffKindToTextColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        DiffLineKind kind = value is DiffLineKind diffKind ? diffKind : DiffLineKind.Context;

        switch (kind)
        {
            case DiffLineKind.Added:
                return Color.FromArgb("#B9F0C0");
            case DiffLineKind.Removed:
                return Color.FromArgb("#FFC2C0");
            case DiffLineKind.Hunk:
                return Color.FromArgb("#7A8AA3");
            default:
                return Color.FromArgb("#C4CAD4");
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class DiffKindToSignConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        DiffLineKind kind = value is DiffLineKind diffKind ? diffKind : DiffLineKind.Context;

        switch (kind)
        {
            case DiffLineKind.Added:
                return "+";
            case DiffLineKind.Removed:
                return "-";
            default:
                return String.Empty;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class DiffKindToSignColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        DiffLineKind kind = value is DiffLineKind diffKind ? diffKind : DiffLineKind.Context;

        switch (kind)
        {
            case DiffLineKind.Added:
                return Color.FromArgb("#7EE787");
            case DiffLineKind.Removed:
                return Color.FromArgb("#FF9492");
            default:
                return Color.FromArgb("#59616F");
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
