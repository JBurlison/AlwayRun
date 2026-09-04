using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace AlwaysRun.Services;

/// <summary>
/// Sends interactive control/input events to a managed Windows console process.
/// </summary>
internal static class ConsoleControlService
{
    private const uint CtrlCEvent = 0;
    private const int StdInputHandle = -10;
    private const short KeyEvent = 0x0001;
    private const ushort VirtualKeyY = 0x59;
    private const ushort VirtualKeyReturn = 0x0D;

    public static async Task<bool> SendCtrlCThenYAsync(
        Process process,
        ILogger logger,
        CancellationToken ct)
    {
        var attached = false;
        try
        {
            if (!AttachConsole((uint)process.Id))
            {
                var error = Marshal.GetLastWin32Error();
                logger.LogWarning("Could not attach to PID {ProcessId} console: {Error}",
                    process.Id, new Win32Exception(error).Message);
                return false;
            }

            attached = true;

            // Prevent AlwaysRun itself from receiving the Ctrl+C event while it is
            // temporarily attached to the managed process's console.
            SetConsoleCtrlHandler(IntPtr.Zero, add: true);

            if (!GenerateConsoleCtrlEvent(CtrlCEvent, processGroupId: 0))
            {
                var error = Marshal.GetLastWin32Error();
                logger.LogWarning("Could not send Ctrl+C to PID {ProcessId}: {Error}",
                    process.Id, new Win32Exception(error).Message);
                return false;
            }

            // Give cmd/PowerShell time to display its confirmation prompt.
            await Task.Delay(TimeSpan.FromMilliseconds(750), ct);

            var inputHandle = GetStdHandle(StdInputHandle);
            if (inputHandle == IntPtr.Zero || inputHandle == new IntPtr(-1))
            {
                logger.LogWarning("Could not obtain the console input handle for PID {ProcessId}", process.Id);
                return false;
            }

            var input = new[]
            {
                CreateKey(VirtualKeyY, 'y', keyDown: true),
                CreateKey(VirtualKeyY, 'y', keyDown: false),
                CreateKey(VirtualKeyReturn, '\r', keyDown: true),
                CreateKey(VirtualKeyReturn, '\r', keyDown: false)
            };

            if (!WriteConsoleInput(inputHandle, input, (uint)input.Length, out var written) ||
                written != input.Length)
            {
                var error = Marshal.GetLastWin32Error();
                logger.LogWarning("Could not send Y+Enter to PID {ProcessId}: {Error}",
                    process.Id, new Win32Exception(error).Message);
                return false;
            }

            return true;
        }
        finally
        {
            if (attached)
            {
                FreeConsole();
                SetConsoleCtrlHandler(IntPtr.Zero, add: false);
            }
        }
    }

    private static InputRecord CreateKey(ushort virtualKey, char character, bool keyDown) => new()
    {
        EventType = KeyEvent,
        KeyEvent = new KeyEventRecord
        {
            KeyDown = keyDown,
            RepeatCount = 1,
            VirtualKeyCode = virtualKey,
            UnicodeChar = character
        }
    };

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode, Size = 20)]
    private struct InputRecord
    {
        [FieldOffset(0)] public short EventType;
        [FieldOffset(4)] public KeyEventRecord KeyEvent;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct KeyEventRecord
    {
        [MarshalAs(UnmanagedType.Bool)] public bool KeyDown;
        public ushort RepeatCount;
        public ushort VirtualKeyCode;
        public ushort VirtualScanCode;
        public char UnicodeChar;
        public uint ControlKeyState;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GenerateConsoleCtrlEvent(uint ctrlEvent, uint processGroupId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleCtrlHandler(IntPtr handlerRoutine, bool add);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll", EntryPoint = "WriteConsoleInputW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteConsoleInput(
        IntPtr consoleInput,
        InputRecord[] buffer,
        uint length,
        out uint eventsWritten);
}
