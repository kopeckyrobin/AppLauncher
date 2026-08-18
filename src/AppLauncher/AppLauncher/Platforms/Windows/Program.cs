using Velopack;

namespace AppLauncher.WinUI;

public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        WinRT.ComWrappersSupport.InitializeComWrappers();

        Microsoft.UI.Xaml.Application.Start(parameters =>
        {
            Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext context =
                new(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());

            SynchronizationContext.SetSynchronizationContext(context);

            _ = new App();
        });
    }
}
