#if WINDOWS
using AppLauncher.WinUI;
using Microsoft.UI.Windowing;
#endif

namespace AppLauncher;

public partial class App : Application
{
    private MainPage? _page;

#if WINDOWS
    private TrayIcon? _trayIcon;
    private AppWindow? _appWindow;
    private bool _isExiting;
    private bool _hasAnnouncedBackground;
#endif

    public App()
    {
        this.InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        MainPage page = new();
        this._page = page;

        Window window = new(page)
        {
            Title = "AppLauncher",
            Width = 1320,
            Height = 860,
            MinimumWidth = 960,
            MinimumHeight = 600
        };

        window.Destroying += (_, _) => page.Shutdown();

#if WINDOWS
        window.HandlerChanged += (_, _) => this.AttachBackgroundMode(window);
#endif

        return window;
    }

#if WINDOWS
    private void AttachBackgroundMode(Window window)
    {
        if (this._appWindow is not null)
        {
            return;
        }

        if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window platformWindow)
        {
            return;
        }

        this._appWindow = platformWindow.AppWindow;

        if (this._appWindow is null)
        {
            return;
        }

        this._appWindow.Closing += this.OnAppWindowClosing;

        this._trayIcon = new TrayIcon("AppLauncher", "Otevřít AppLauncher", "Ukončit AppLauncher");
        this._trayIcon.OpenRequested += this.OnTrayOpenRequested;
        this._trayIcon.ExitRequested += this.OnTrayExitRequested;
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs eventArgs)
    {
        if (this._isExiting || this._trayIcon is null)
        {
            return;
        }

        eventArgs.Cancel = true;
        sender.Hide();

        if (!this._hasAnnouncedBackground)
        {
            this._hasAnnouncedBackground = true;
            this._trayIcon.ShowNotice(
                "AppLauncher běží dál",
                "Spuštěné aplikace zůstávají zapnuté. Ukončíš je přes ikonu v oznamovací oblasti.");
        }
    }

    private void OnTrayOpenRequested(object? sender, EventArgs eventArgs)
    {
        if (this._appWindow is null)
        {
            return;
        }

        this._appWindow.Show(true);

        if (this._appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Restore();
        }

        TrayIcon.BringToFront(Microsoft.UI.Win32Interop.GetWindowFromWindowId(this._appWindow.Id));
    }

    private void OnTrayExitRequested(object? sender, EventArgs eventArgs)
    {
        this._isExiting = true;

        this._page?.Shutdown();

        this._trayIcon?.Dispose();
        this._trayIcon = null;

        Microsoft.UI.Xaml.Application.Current.Exit();
    }
#endif
}
