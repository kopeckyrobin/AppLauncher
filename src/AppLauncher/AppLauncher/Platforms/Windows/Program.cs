using Velopack;

namespace AppLauncher.WinUI;

public static class Program
{
    private const string InstanceName = @"Local\AppLauncher.SingleInstance";

    private static readonly TimeSpan HandoverTimeout = TimeSpan.FromSeconds(2);

    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        using Mutex instance = new(true, InstanceName, out bool isOwner);

        if (!isOwner)
        {
            TrayIcon.BroadcastActivation();

            if (!TryAcquire(instance))
            {
                return;
            }
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();

        Microsoft.UI.Xaml.Application.Start(parameters =>
        {
            Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext context =
                new(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());

            SynchronizationContext.SetSynchronizationContext(context);

            _ = new App();
        });

        GC.KeepAlive(instance);
    }

    private static bool TryAcquire(Mutex instance)
    {
        try
        {
            return instance.WaitOne(HandoverTimeout);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }
}
