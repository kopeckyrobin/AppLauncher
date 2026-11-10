namespace AppLauncher.Views;

public sealed record LogContent(string Text, string SearchText)
{
    public static LogContent Empty { get; } = new(String.Empty, String.Empty);
}

public sealed class LogView : View
{
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content),
        typeof(LogContent),
        typeof(LogView),
        LogContent.Empty);

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor),
        typeof(Color),
        typeof(LogView),
        Colors.Gray);

    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(
        nameof(FontFamily),
        typeof(string),
        typeof(LogView),
        String.Empty);

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize),
        typeof(double),
        typeof(LogView),
        12d);

    public LogContent Content
    {
        get { return (LogContent)this.GetValue(ContentProperty); }
        set { this.SetValue(ContentProperty, value); }
    }

    public Color TextColor
    {
        get { return (Color)this.GetValue(TextColorProperty); }
        set { this.SetValue(TextColorProperty, value); }
    }

    public string FontFamily
    {
        get { return (string)this.GetValue(FontFamilyProperty); }
        set { this.SetValue(FontFamilyProperty, value); }
    }

    public double FontSize
    {
        get { return (double)this.GetValue(FontSizeProperty); }
        set { this.SetValue(FontSizeProperty, value); }
    }
}
