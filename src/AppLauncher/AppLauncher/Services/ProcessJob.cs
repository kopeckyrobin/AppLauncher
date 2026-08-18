using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AppLauncher.Services;

public static class ProcessJob
{
    private const int ExtendedLimitInformation = 9;
    private const uint LimitKillOnJobClose = 0x00002000;

    private static readonly nint Handle = Create();

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimitInformationData
    {
        public BasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateJobObjectW(nint attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(nint job, int infoClass, nint info, uint infoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(nint job, nint process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint handle);

    public static bool IsAvailable
    {
        get { return Handle != 0; }
    }

    public static void Assign(Process process)
    {
        if (Handle == 0)
        {
            return;
        }

        try
        {
            AssignProcessToJobObject(Handle, process.Handle);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }

    private static nint Create()
    {
        nint job = CreateJobObjectW(0, null);

        if (job == 0)
        {
            return 0;
        }

        ExtendedLimitInformationData information = default;
        information.BasicLimitInformation.LimitFlags = LimitKillOnJobClose;

        int length = Marshal.SizeOf<ExtendedLimitInformationData>();
        nint buffer = Marshal.AllocHGlobal(length);

        try
        {
            Marshal.StructureToPtr(information, buffer, false);

            if (!SetInformationJobObject(job, ExtendedLimitInformation, buffer, (uint)length))
            {
                CloseHandle(job);
                return 0;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return job;
    }
}
