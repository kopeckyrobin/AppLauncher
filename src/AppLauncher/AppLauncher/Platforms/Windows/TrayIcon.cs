using System.Runtime.InteropServices;

namespace AppLauncher.WinUI;

public sealed class TrayIcon : IDisposable
{
    private const string ActivationMessageName = "AppLauncher.ActivateMainWindow";
    private const string TaskbarCreatedMessageName = "TaskbarCreated";
    private const string WindowClassName = "AppLauncherTrayWindow";

    private const uint MessageNull = 0x0000;
    private const uint MessageApp = 0x8000;
    private const uint CallbackMessage = MessageApp + 1;
    private const uint LeftButtonUp = 0x0202;
    private const uint LeftButtonDoubleClick = 0x0203;
    private const uint RightButtonUp = 0x0205;

    private const uint AddIcon = 0x00000000;
    private const uint ModifyIcon = 0x00000001;
    private const uint DeleteIcon = 0x00000002;

    private const uint IconFlagMessage = 0x00000001;
    private const uint IconFlagIcon = 0x00000002;
    private const uint IconFlagTip = 0x00000004;
    private const uint IconFlagInfo = 0x00000010;

    private const uint MenuString = 0x00000000;
    private const uint MenuSeparator = 0x00000800;

    private const uint TrackRightButton = 0x0002;
    private const uint TrackNoNotify = 0x0080;
    private const uint TrackReturnCommand = 0x0100;

    private const uint StylePopup = 0x80000000;
    private const uint ExtendedStyleToolWindow = 0x00000080;

    private const nint BroadcastWindow = 0xFFFF;
    private const nint ApplicationIconResource = 32512;

    private const uint OpenCommand = 1;
    private const uint ExitCommand = 2;

    private readonly WindowProcedure _procedure;
    private readonly string _tooltip;
    private readonly string _openText;
    private readonly string _exitText;
    private readonly uint _activationMessage;
    private readonly uint _taskbarCreatedMessage;

    private nint _window;
    private nint _icon;
    private bool _isVisible;

    public TrayIcon(string tooltip, string openText, string exitText)
    {
        this._tooltip = tooltip;
        this._openText = openText;
        this._exitText = exitText;
        this._procedure = this.OnMessage;
        this._activationMessage = RegisterWindowMessageW(ActivationMessageName);
        this._taskbarCreatedMessage = RegisterWindowMessageW(TaskbarCreatedMessageName);

        nint instance = GetModuleHandleW(null);

        WindowClass windowClass = new()
        {
            Procedure = Marshal.GetFunctionPointerForDelegate(this._procedure),
            Instance = instance,
            ClassName = WindowClassName
        };

        RegisterClassW(ref windowClass);

        this._window = CreateWindowExW(
            ExtendedStyleToolWindow,
            WindowClassName,
            WindowClassName,
            StylePopup,
            0,
            0,
            0,
            0,
            0,
            0,
            instance,
            0);

        if (this._window == 0)
        {
            return;
        }

        this._icon = LoadApplicationIcon();

        this.AddToNotificationArea();
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? ExitRequested;

    public bool IsVisible
    {
        get { return this._isVisible; }
    }

    public static void BroadcastActivation()
    {
        uint message = RegisterWindowMessageW(ActivationMessageName);

        if (message != 0)
        {
            PostMessageW(BroadcastWindow, message, 0, 0);
        }
    }

    public static void BringToFront(nint window)
    {
        if (window == 0)
        {
            return;
        }

        SetForegroundWindow(window);
    }

    public void ShowNotice(string title, string message)
    {
        if (!this._isVisible)
        {
            return;
        }

        NotifyIconData data = this.CreateData();
        data.Flags = IconFlagInfo;
        data.InfoTitle = title;
        data.Info = message;

        Shell_NotifyIconW(ModifyIcon, ref data);
    }

    public void Dispose()
    {
        if (this._isVisible)
        {
            NotifyIconData data = this.CreateData();
            Shell_NotifyIconW(DeleteIcon, ref data);
            this._isVisible = false;
        }

        if (this._icon != 0)
        {
            DestroyIcon(this._icon);
            this._icon = 0;
        }

        if (this._window != 0)
        {
            DestroyWindow(this._window);
            this._window = 0;
        }
    }

    private void AddToNotificationArea()
    {
        if (this._window == 0)
        {
            return;
        }

        NotifyIconData data = this.CreateData();
        data.Flags = IconFlagMessage | IconFlagIcon | IconFlagTip;
        data.CallbackMessage = CallbackMessage;
        data.Icon = this._icon;
        data.Tip = this._tooltip;

        this._isVisible = Shell_NotifyIconW(AddIcon, ref data);
    }

    private NotifyIconData CreateData()
    {
        return new NotifyIconData
        {
            Size = (uint)Marshal.SizeOf<NotifyIconData>(),
            Window = this._window,
            Id = 1,
            Tip = String.Empty,
            Info = String.Empty,
            InfoTitle = String.Empty
        };
    }

    private static nint LoadApplicationIcon()
    {
        string? path = Environment.ProcessPath;

        if (!String.IsNullOrEmpty(path))
        {
            uint extracted = ExtractIconExW(path, 0, out nint large, out nint small, 1);

            if (extracted > 0)
            {
                if (large != 0 && large != small)
                {
                    DestroyIcon(large);
                }

                if (small != 0)
                {
                    return small;
                }

                if (large != 0)
                {
                    return large;
                }
            }
        }

        return LoadIconW(0, ApplicationIconResource);
    }

    private nint OnMessage(nint window, uint message, nint firstParameter, nint secondParameter)
    {
        if (message == CallbackMessage)
        {
            uint trigger = (uint)(secondParameter & 0xFFFF);

            if (trigger is LeftButtonUp or LeftButtonDoubleClick)
            {
                this.OpenRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (trigger == RightButtonUp)
            {
                this.ShowMenu();
            }

            return 0;
        }

        if (this._activationMessage != 0 && message == this._activationMessage)
        {
            this.OpenRequested?.Invoke(this, EventArgs.Empty);
            return 0;
        }

        if (this._taskbarCreatedMessage != 0 && message == this._taskbarCreatedMessage)
        {
            this._isVisible = false;
            this.AddToNotificationArea();
            return 0;
        }

        return DefWindowProcW(window, message, firstParameter, secondParameter);
    }

    private void ShowMenu()
    {
        nint menu = CreatePopupMenu();

        if (menu == 0)
        {
            return;
        }

        AppendMenuW(menu, MenuString, OpenCommand, this._openText);
        AppendMenuW(menu, MenuSeparator, 0, null);
        AppendMenuW(menu, MenuString, ExitCommand, this._exitText);

        GetCursorPos(out CursorPoint point);
        SetForegroundWindow(this._window);

        uint command = TrackPopupMenu(
            menu,
            TrackRightButton | TrackNoNotify | TrackReturnCommand,
            point.X,
            point.Y,
            0,
            this._window,
            0);

        PostMessageW(this._window, MessageNull, 0, 0);
        DestroyMenu(menu);

        if (command == OpenCommand)
        {
            this.OpenRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (command == ExitCommand)
        {
            this.ExitRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProcedure(nint window, uint message, nint firstParameter, nint secondParameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Style;
        public nint Procedure;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? MenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string ClassName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public nint Window;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string InfoTitle;
        public uint InfoFlags;
        public Guid ItemGuid;
        public nint BalloonIcon;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandleW(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassW(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(
        uint extendedStyle,
        string className,
        string? windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DefWindowProcW(nint window, uint message, nint firstParameter, nint secondParameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(nint window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessageW(string name);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool PostMessageW(nint window, uint message, nint firstParameter, nint secondParameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out CursorPoint point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool AppendMenuW(nint menu, uint flags, nuint identifier, string? item);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint window, nint rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyMenu(nint menu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadIconW(nint instance, nint name);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint icon);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconExW(string file, int index, out nint largeIcon, out nint smallIcon, uint count);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint message, ref NotifyIconData data);
}
