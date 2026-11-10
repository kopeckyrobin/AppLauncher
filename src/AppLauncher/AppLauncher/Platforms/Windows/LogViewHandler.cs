using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment;
using ScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility;
using SolidColorBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using Thickness = Microsoft.UI.Xaml.Thickness;
using VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment;

namespace AppLauncher.Views;

public sealed class LogViewHandler : ViewHandler<LogView, RichEditBox>
{
    private const int MaximumHighlights = 1000;

    private static readonly global::Windows.UI.Color HighlightBackground = global::Windows.UI.Color.FromArgb(255, 217, 164, 65);
    private static readonly global::Windows.UI.Color HighlightForeground = global::Windows.UI.Color.FromArgb(255, 20, 23, 28);

    public static readonly IPropertyMapper<LogView, LogViewHandler> Mapper = new PropertyMapper<LogView, LogViewHandler>(ViewHandler.ViewMapper)
    {
        [nameof(LogView.TextColor)] = MapStyle,
        [nameof(LogView.FontFamily)] = MapStyle,
        [nameof(LogView.FontSize)] = MapStyle,
        [nameof(LogView.Content)] = MapContent
    };

    private string _text = String.Empty;
    private string _search = String.Empty;

    public LogViewHandler()
        : base(Mapper)
    {
    }

    protected override RichEditBox CreatePlatformView()
    {
        SolidColorBrush transparent = new(Microsoft.UI.Colors.Transparent);

        RichEditBox box = new()
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            IsSpellCheckEnabled = false,
            TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4),
            Background = transparent,
            RequestedTheme = ElementTheme.Dark,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        string[] transparentKeys =
        {
            "TextControlBackground",
            "TextControlBackgroundPointerOver",
            "TextControlBackgroundFocused",
            "TextControlBackgroundDisabled",
            "TextControlBorderBrush",
            "TextControlBorderBrushPointerOver",
            "TextControlBorderBrushFocused",
            "TextControlBorderBrushDisabled"
        };

        foreach (string key in transparentKeys)
        {
            box.Resources[key] = transparent;
        }

        ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Visible);
        return box;
    }

    private static void MapStyle(LogViewHandler handler, LogView view)
    {
        RichEditBox box = handler.PlatformView;
        SolidColorBrush brush = new(view.TextColor.ToWindowsColor());

        box.Foreground = brush;
        box.Resources["TextControlForeground"] = brush;
        box.Resources["TextControlForegroundPointerOver"] = brush;
        box.Resources["TextControlForegroundFocused"] = brush;

        if (!String.IsNullOrEmpty(view.FontFamily))
        {
            box.FontFamily = new FontFamily(view.FontFamily);
        }

        box.FontSize = view.FontSize;
    }

    private static void MapContent(LogViewHandler handler, LogView view)
    {
        handler.UpdateContent(view.Content ?? LogContent.Empty);
    }

    private void UpdateContent(LogContent content)
    {
        string text = content.Text.Replace("\r\n", "\r").Replace('\n', '\r');
        string search = content.SearchText ?? String.Empty;

        bool textChanged = !String.Equals(text, this._text, StringComparison.Ordinal);
        bool searchChanged = !String.Equals(search, this._search, StringComparison.Ordinal);

        if (!textChanged && !searchChanged)
        {
            return;
        }

        this._text = text;
        this._search = search;

        RichEditBox box = this.PlatformView;
        RichEditTextDocument document = box.Document;
        int firstMatch = -1;

        box.IsReadOnly = false;
        document.BatchDisplayUpdates();

        try
        {
            document.SetText(TextSetOptions.None, text);

            if (search.Length > 0)
            {
                firstMatch = Highlight(document, text, search);
            }
        }
        finally
        {
            document.ApplyDisplayUpdates();
            box.IsReadOnly = true;
        }

        if (firstMatch >= 0 && searchChanged)
        {
            box.UpdateLayout();
            document.GetRange(firstMatch, firstMatch + search.Length).ScrollIntoView(PointOptions.Start);
        }
    }

    private static int Highlight(RichEditTextDocument document, string text, string search)
    {
        int first = -1;
        int count = 0;
        int index = 0;

        while (count < MaximumHighlights)
        {
            index = text.IndexOf(search, index, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                break;
            }

            Microsoft.UI.Text.ITextRange range = document.GetRange(index, index + search.Length);
            range.CharacterFormat.BackgroundColor = HighlightBackground;
            range.CharacterFormat.ForegroundColor = HighlightForeground;

            if (first < 0)
            {
                first = index;
            }

            index += search.Length;
            count++;
        }

        return first;
    }
}
