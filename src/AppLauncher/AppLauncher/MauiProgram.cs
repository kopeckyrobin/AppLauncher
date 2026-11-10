namespace AppLauncher;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
#if WINDOWS
            .ConfigureMauiHandlers(handlers => handlers.AddHandler<Views.LogView, Views.LogViewHandler>())
#endif
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if WINDOWS
        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("CompactComboBox", (handler, view) =>
        {
            handler.PlatformView.MinHeight = 0;
            handler.PlatformView.Padding = new Microsoft.UI.Xaml.Thickness(10, 0, 8, 0);
            handler.PlatformView.VerticalContentAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center;
        });
#endif

        return builder.Build();
    }
}
