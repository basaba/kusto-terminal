using System.Runtime.InteropServices;

namespace KustoTerminal.UI.Common;

/// <summary>
/// Detects real-time keyboard modifier state on macOS via CoreGraphics.
/// Solves the problem where terminals send identical bytes for Enter vs Shift+Enter.
/// Returns no modifiers on non-macOS platforms (relies on driver protocol support).
/// </summary>
internal static class PlatformModifiers
{
    private static readonly bool _isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    private const ulong ShiftFlag   = 0x00020000;
    private const ulong ControlFlag = 0x00040000;
    private const ulong AltFlag     = 0x00080000;

    public static bool IsShiftHeld => _isMacOS && (GetFlags() & ShiftFlag) != 0;
    public static bool IsControlHeld => _isMacOS && (GetFlags() & ControlFlag) != 0;
    public static bool IsAltHeld => _isMacOS && (GetFlags() & AltFlag) != 0;

    private static ulong GetFlags()
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
}
