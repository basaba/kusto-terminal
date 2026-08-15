using System.Runtime.InteropServices;

namespace KustoTerminal.UI.Common;

/// <summary>
/// Detects real-time keyboard modifier state on macOS and Windows.
/// Solves the problem where terminals send identical bytes for Enter vs Shift+Enter.
/// Returns no modifiers on other platforms (relies on driver protocol support).
/// </summary>
internal static class PlatformModifiers
{
    private static readonly bool _isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    private static readonly bool _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private const ulong ShiftFlag   = 0x00020000;
    private const ulong ControlFlag = 0x00040000;
    private const ulong AltFlag     = 0x00080000;

    private const int VirtualKeyShift = 0x10;
    private const int VirtualKeyControl = 0x11;
    private const int VirtualKeyAlt = 0x12;
    private const int VirtualKeyEnter = 0x0D;
    private const int VirtualKeyBackspace = 0x08;
    private const int KeyDownMask = 0x8000;

    public static bool IsWindows => _isWindows;
    public static bool IsShiftHeld => IsModifierHeld(ShiftFlag, VirtualKeyShift);
    public static bool IsControlHeld => IsModifierHeld(ControlFlag, VirtualKeyControl);
    public static bool IsAltHeld => IsModifierHeld(AltFlag, VirtualKeyAlt);
    public static bool IsEnterHeld => _isWindows && IsWindowsKeyHeld(VirtualKeyEnter);
    public static bool IsBackspaceHeld => _isWindows && IsWindowsKeyHeld(VirtualKeyBackspace);

    private static bool IsModifierHeld(ulong macOSFlag, int windowsVirtualKey)
    {
        if (_isMacOS)
            return (GetMacOSFlags() & macOSFlag) != 0;

        return _isWindows && IsWindowsKeyHeld(windowsVirtualKey);
    }

    private static bool IsWindowsKeyHeld(int virtualKey) =>
        (GetAsyncKeyState(virtualKey) & KeyDownMask) != 0;

    private static ulong GetMacOSFlags()
    {
        try
        {
            return CGEventSourceFlagsState(0);
        }
        catch
        {
            return 0;
        }
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern ulong CGEventSourceFlagsState(int stateID);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
