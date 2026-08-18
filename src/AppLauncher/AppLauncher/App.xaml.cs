namespace AppLauncher;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        MainPage page = new();

        Window window = new(page)
        {
            Title = "AppLauncher",
            Width = 1320,
            Height = 860,
            MinimumWidth = 960,
            MinimumHeight = 600
        };

        window.Destroying += (_, _) => page.Shutdown();

        return window;
    }
}
