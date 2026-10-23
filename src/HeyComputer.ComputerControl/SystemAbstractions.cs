using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HeyComputer.ComputerControl;

public interface IProcessCatalog
{
    IReadOnlyList<RunningApplication> GetRunningApplications();
}

public interface IAllowlistedTargetLauncher
{
    void Launch(LaunchTarget target);
}

public interface IMediaKeySender
{
    void TogglePlayPause();
}

public interface IComputerControlClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemComputerControlClock : IComputerControlClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class WindowsProcessCatalog : IProcessCatalog
{
    public IReadOnlyList<RunningApplication> GetRunningApplications()
    {
        var applications = new List<RunningApplication>();

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    applications.Add(new RunningApplication(
                        process.Id,
                        process.ProcessName,
                        NullIfEmpty(process.MainWindowTitle)));
                }
                catch (InvalidOperationException)
                {
                    // The process exited while it was being observed.
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Protected processes may not expose metadata to this user.
                }
            }
        }

        return applications
            .OrderBy(application => application.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(application => application.ProcessId)
            .ToArray();
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed class WindowsAllowlistedTargetLauncher : IAllowlistedTargetLauncher
{
    public void Launch(LaunchTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        Process.Start(new ProcessStartInfo
        {
            FileName = target.Value,
            UseShellExecute = true
        });
    }
}

public sealed class WindowsMediaKeySender : IMediaKeySender
{
    private const ushort VirtualKeyMediaPlayPause = 0xB3;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    public void TogglePlayPause()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Media key control is available only on Windows.");
        }

        var inputs = new[]
        {
            CreateKeyboardInput(VirtualKeyMediaPlayPause, 0),
            CreateKeyboardInput(VirtualKeyMediaPlayPause, KeyEventKeyUp)
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static Input CreateKeyboardInput(ushort virtualKey, uint flags) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                Flags = flags
            }
        }
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, Input[] inputs, int sizeOfInputStructure);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }
}
