using Windows.Storage;
using Windows.Storage.Pickers;

namespace AppLauncher.Services;

public static class FolderPickerService
{
    public static async Task<string?> PickFolderAsync()
    {
        nint windowHandle = GetWindowHandle();
        if (windowHandle == 0)
        {
            return null;
        }

        FolderPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder
        };

        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);

        StorageFolder? folder = await picker.PickSingleFolderAsync();

        return folder?.Path;
    }

    private static nint GetWindowHandle()
    {
        Window? window = Application.Current?.Windows.FirstOrDefault();
        if (window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window platformWindow)
        {
            return 0;
        }

        return WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
    }
}
