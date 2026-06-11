using System.Runtime.InteropServices;

namespace KitLog.Cli.Services;

internal static class WindowsAnsiBootstrap {
    const int StdOutputHandle = -11;
    const int StdErrorHandle = -12;
    const uint EnableVirtualTerminalProcessing = 0x0004;
    const uint GenericWrite = 0x40000000;
    const uint FileShareWrite = 2;
    const uint OpenExisting = 3;
    static readonly nint InvalidHandleValue = new(-1);

    public static void EnableIfNeeded() {
        if (!OperatingSystem.IsWindows())
            return;

        Environment.SetEnvironmentVariable("DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION", "1");

        EnableVt(GetStdHandle(StdOutputHandle));
        EnableVt(GetStdHandle(StdErrorHandle));
        EnableVt(OpenConsoleOutput());

        try {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch {
            // Best-effort only.
        }
    }

    static nint OpenConsoleOutput() {
        try {
            var handle = CreateFileW("CONOUT$", GenericWrite, FileShareWrite, 0, OpenExisting, 0, 0);
            return handle == InvalidHandleValue ? nint.Zero : handle;
        }
        catch {
            return nint.Zero;
        }
    }

    static void EnableVt(nint handle) {
        if (handle == nint.Zero || handle == InvalidHandleValue)
            return;

        try {
            if (GetConsoleMode(handle, out var mode))
                SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
        }
        catch {
            // Best-effort only.
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern nint CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        nint lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        nint hTemplateFile);

    [DllImport("kernel32.dll")]
    static extern nint GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll")]
    static extern bool SetConsoleMode(nint hConsoleHandle, uint dwMode);
}
