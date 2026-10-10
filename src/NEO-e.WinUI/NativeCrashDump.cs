using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NEO_e.WinUI;

internal static class NativeCrashDump
{
    private delegate int UnhandledExceptionFilter(IntPtr exceptionInfo);

    private static readonly UnhandledExceptionFilter Filter = OnUnhandledException;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr SetUnhandledExceptionFilter(UnhandledExceptionFilter topLevelExceptionFilter);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("dbghelp.dll", SetLastError = true)]
    private static extern bool MiniDumpWriteDump(
        IntPtr process,
        uint processId,
        IntPtr fileHandle,
        int dumpType,
        IntPtr exceptionParam,
        IntPtr userStreamParam,
        IntPtr callbackParam);

    public static void Install()
    {
        SetUnhandledExceptionFilter(Filter);
    }

    private static int OnUnhandledException(IntPtr exceptionInfo)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(dir);

            var record = Marshal.ReadIntPtr(exceptionInfo);
            var code = Marshal.ReadInt32(record);
            var address = Marshal.ReadIntPtr(record, 16);

            File.WriteAllText(
                Path.Combine(dir, "crash-code.txt"),
                "code=0x" + code.ToString("X8") +
                " addr=0x" + address.ToInt64().ToString("X") +
                " tid=" + GetCurrentThreadId());

            using var stream = File.Create(Path.Combine(dir, "crash.dmp"));
            using var process = Process.GetCurrentProcess();

            var info = new MiniDumpExceptionInfo
            {
                ThreadId = GetCurrentThreadId(),
                ExceptionPointers = exceptionInfo,
                ClientId = 0,
            };

            var infoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<MiniDumpExceptionInfo>());
            try
            {
                Marshal.StructureToPtr(info, infoPtr, false);
                MiniDumpWriteDump(
                    process.Handle,
                    (uint)process.Id,
                    stream.SafeFileHandle.DangerousGetHandle(),
                    0x00000002,
                    infoPtr,
                    IntPtr.Zero,
                    IntPtr.Zero);
            }
            finally
            {
                Marshal.FreeHGlobal(infoPtr);
            }
        }
        catch
        {
        }

        return 1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MiniDumpExceptionInfo
    {
        public uint ThreadId;
        public IntPtr ExceptionPointers;
        public int ClientId;
    }
}
