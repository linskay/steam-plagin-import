using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public static class SteamBootstrap
{
    // Millennium kills its Lua worker's Windows job on Steam exit. Start the coordinator with
    // the user's desktop shell as parent so it can complete the deliberate Steam restart.
    // SteamSession/TrayApplication still terminate the coordinator on ordinary Steam exit.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb; public string reserved, desktop, title;
        public int x, y, width, height, xChars, yChars, fill, flags;
        public short show, reservedSize; public IntPtr reservedData, stdin, stdout, stderr;
    }
    [StructLayout(LayoutKind.Sequential)] private struct StartupInfoEx { public StartupInfo info; public IntPtr attributes; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInfo { public IntPtr process, thread; public int pid, tid; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int id);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returnSize);
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcessW(string application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfoEx startup, out ProcessInfo process);

    public static void Start(string exePath)
    {
        int session = Process.GetCurrentProcess().SessionId;
        IntPtr parent = IntPtr.Zero;
        foreach (var process in Process.GetProcessesByName("explorer"))
            using (process) if (parent == IntPtr.Zero && process.SessionId == session) parent = OpenProcess(0x0080, false, process.Id);
        if (parent == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось найти оболочку рабочего стола для запуска утилиты.");
        IntPtr size = IntPtr.Zero, attributes = IntPtr.Zero, value = IntPtr.Zero;
        bool initialized = false;
        try
        {
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            attributes = Marshal.AllocHGlobal(size);
            if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref size)) throw new Win32Exception(Marshal.GetLastWin32Error());
            initialized = true;
            value = Marshal.AllocHGlobal(IntPtr.Size); Marshal.WriteIntPtr(value, parent);
            if (!UpdateProcThreadAttribute(attributes, 0, new IntPtr(0x00020000), value, new IntPtr(IntPtr.Size), IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
            var startup = new StartupInfoEx { info = new StartupInfo { cb = Marshal.SizeOf(typeof(StartupInfoEx)) }, attributes = attributes };
            ProcessInfo child;
            if (!CreateProcessW(exePath, new StringBuilder("\"" + exePath + "\" --steam-session"), IntPtr.Zero, IntPtr.Zero, false, 0x08080000, IntPtr.Zero, System.IO.Path.GetDirectoryName(exePath), ref startup, out child)) throw new Win32Exception(Marshal.GetLastWin32Error());
            CloseHandle(child.thread); CloseHandle(child.process);
        }
        finally
        {
            if (initialized) DeleteProcThreadAttributeList(attributes);
            if (attributes != IntPtr.Zero) Marshal.FreeHGlobal(attributes);
            if (value != IntPtr.Zero) Marshal.FreeHGlobal(value);
            CloseHandle(parent);
        }
    }
}
