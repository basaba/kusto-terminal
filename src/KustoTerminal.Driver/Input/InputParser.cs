using Terminal.Gui;
using Terminal.Gui.Input;
using Terminal.Gui.Drawing;
using KustoTerminal.Driver.Platform;
using static KustoTerminal.Driver.Input.AnsiSequenceReader;

namespace KustoTerminal.Driver.Input;

/// <summary>
/// Converts parsed ANSI sequences into Terminal.Gui Key and MouseEventArgs events.
/// Maps CSI/SS3 sequences to key codes, handles modifiers, and parses SGR mouse reports.
/// </summary>
internal static class InputParser
{
    /// <summary>Parse a sequence into a Terminal.Gui Key</summary>
    public static Key? ToKey(ref ParsedSequence seq)
    {
        switch (seq.Type)
        {
            case SequenceType.Char:
                return CharToKey(seq.Codepoint);

            case SequenceType.CsiSequence:
                return CsiToKey(seq.FinalByte, seq.Parameters);

            case SequenceType.Ss3Sequence:
                return Ss3ToKey(seq.FinalByte);

            case SequenceType.EscapeOnly:
                if (seq.Codepoint == 0)
                    return Key.Esc;
                // Alt+key
                return CharToKey(seq.Codepoint).WithAlt;

            default:
                return null;
        }
    }

    /// <summary>Parse a CSI sequence into a MouseEventArgs if it's a mouse report</summary>
    public static MouseEventArgs? ToMouse(ref ParsedSequence seq)
    {
        if (seq.Type != SequenceType.CsiSequence) return null;

        // SGR mouse: ESC [ < Cb ; Cx ; Cy M/m
        if (seq.FinalByte is (byte)'M' or (byte)'m' && seq.Parameters.Length > 0
            && seq.Parameters[0] == (byte)'<')
        {
            return ParseSgrMouse(seq.Parameters[1..], seq.FinalByte == (byte)'M');
        }

        return null;
    }

    private static Key CharToKey(int codepoint)
    {
        // Control characters — but NOT those with dedicated key mappings
        // (Tab=9/Ctrl+I, LF=10/Ctrl+J, CR=13/Ctrl+M, BS=8/Ctrl+H)
        if (codepoint >= 1 && codepoint <= 26
            && codepoint != 8 && codepoint != 9 && codepoint != 10 && codepoint != 13)
        {
            // Ctrl+A through Ctrl+Z (excluding the special ones above)
            var letter = (KeyCode)((int)KeyCode.A + codepoint - 1);
            return new Key(letter | KeyCode.CtrlMask);
        }

        return codepoint switch
        {
            0 => new Key(KeyCode.Space | KeyCode.CtrlMask), // Ctrl+Space = NUL
            8 or 127 => Key.Backspace,
            9 => ApplyPlatformModifiers(KeyCode.Tab),
            10 or 13 => ApplyPlatformModifiers(KeyCode.Enter),
            27 => Key.Esc,
            32 => Key.Space,
            // Uppercase letters need ShiftMask — Terminal.Gui uses KeyCode.A='a', KeyCode.A|Shift='A'
            >= 'A' and <= 'Z' => new Key((KeyCode)codepoint | KeyCode.ShiftMask),
            _ when codepoint >= 32 && codepoint < 127 =>
                new Key((KeyCode)codepoint),
            _ => new Key((KeyCode)codepoint)
        };
    }

    /// <summary>
    /// On macOS, query CoreGraphics for the real-time modifier key state.
    /// This solves the problem where terminals send identical bytes for
    /// Enter vs Shift+Enter (both \r). Works regardless of kitty/xterm protocol.
    /// On Linux, returns the key unmodified (relies on kitty/modifyOtherKeys).
    /// </summary>
    private static Key ApplyPlatformModifiers(KeyCode baseKey)
    {
        var flags = Interop.GetMacOSModifierFlags();
        if (flags == 0)
            return new Key(baseKey);

        KeyCode modifiers = KeyCode.Null;
        if ((flags & Interop.kCGEventFlagMaskShift) != 0) modifiers |= KeyCode.ShiftMask;
        if ((flags & Interop.kCGEventFlagMaskAlternate) != 0) modifiers |= KeyCode.AltMask;
        if ((flags & Interop.kCGEventFlagMaskControl) != 0) modifiers |= KeyCode.CtrlMask;

        return new Key(baseKey | modifiers);
    }

    private static Key CsiToKey(byte final, ReadOnlySpan<byte> parameters)
    {
        // Parse numeric parameters (semicolon-separated)
        Span<int> nums = stackalloc int[8];
        int numCount = ParseCsiParams(parameters, nums);

        // Kitty keyboard protocol: CSI codepoint ; modifier u
        if (final == (byte)'u' && numCount >= 1)
        {
            KeyCode modifiers = numCount >= 2 ? DecodeModifier(nums[1]) : KeyCode.Null;
            return new Key(CodepointToKeyCode(nums[0]) | modifiers);
        }

        // Modifier from second parameter (CSI 1;mod X format)
        KeyCode mods = KeyCode.Null;
        if (numCount >= 2)
        {
            mods = DecodeModifier(nums[1]);
        }

        // xterm modifyOtherKeys: CSI 27 ; modifier ; codepoint ~
        if (final == (byte)'~' && numCount >= 3 && nums[0] == 27)
        {
            KeyCode xMods = DecodeModifier(nums[1]);
            return new Key(CodepointToKeyCode(nums[2]) | xMods);
        }

        return final switch
        {
            (byte)'A' => new Key(KeyCode.CursorUp | mods),
            (byte)'B' => new Key(KeyCode.CursorDown | mods),
            (byte)'C' => new Key(KeyCode.CursorRight | mods),
            (byte)'D' => new Key(KeyCode.CursorLeft | mods),
            (byte)'H' => new Key(KeyCode.Home | mods),
            (byte)'F' => new Key(KeyCode.End | mods),
            (byte)'Z' => new Key(KeyCode.Tab | KeyCode.ShiftMask), // Shift+Tab
            (byte)'~' when numCount >= 1 => TildeKey(nums[0], mods),
            (byte)'P' => new Key(KeyCode.F1 | mods),
            (byte)'Q' => new Key(KeyCode.F2 | mods),
            (byte)'R' => new Key(KeyCode.F3 | mods),
            (byte)'S' => new Key(KeyCode.F4 | mods),
            _ => Key.Empty
        };
    }

    /// <summary>
    /// Map a Unicode codepoint to Terminal.Gui KeyCode.
    /// Handles special codepoints (Enter, Tab, Backspace, Escape)
    /// and ASCII printable characters.
    /// </summary>
    private static KeyCode CodepointToKeyCode(int codepoint) => codepoint switch
    {
        8 or 127 => KeyCode.Backspace,
        9 => KeyCode.Tab,
        13 => KeyCode.Enter,
        27 => KeyCode.Esc,
        32 => KeyCode.Space,
        // Uppercase lowercase letters — Terminal.Gui keybindings use KeyCode.A-Z (65-90)
        >= 'a' and <= 'z' => (KeyCode)(codepoint - 32),
        _ when codepoint >= 33 && codepoint < 127 => (KeyCode)codepoint,
        _ => (KeyCode)codepoint
    };

    private static Key TildeKey(int num, KeyCode modifiers) => num switch
    {
        1 => new Key(KeyCode.Home | modifiers),
        2 => new Key(KeyCode.Insert | modifiers),
        3 => new Key(KeyCode.Delete | modifiers),
        4 => new Key(KeyCode.End | modifiers),
        5 => new Key(KeyCode.PageUp | modifiers),
        6 => new Key(KeyCode.PageDown | modifiers),
        15 => new Key(KeyCode.F5 | modifiers),
        17 => new Key(KeyCode.F6 | modifiers),
        18 => new Key(KeyCode.F7 | modifiers),
        19 => new Key(KeyCode.F8 | modifiers),
        20 => new Key(KeyCode.F9 | modifiers),
        21 => new Key(KeyCode.F10 | modifiers),
        23 => new Key(KeyCode.F11 | modifiers),
        24 => new Key(KeyCode.F12 | modifiers),
        _ => Key.Empty
    };

    private static Key Ss3ToKey(byte final) => final switch
    {
        (byte)'A' => Key.CursorUp,
        (byte)'B' => Key.CursorDown,
        (byte)'C' => Key.CursorRight,
        (byte)'D' => Key.CursorLeft,
        (byte)'H' => Key.Home,
        (byte)'F' => Key.End,
        (byte)'P' => Key.F1,
        (byte)'Q' => Key.F2,
        (byte)'R' => Key.F3,
        (byte)'S' => Key.F4,
        _ => Key.Empty
    };

    private static KeyCode DecodeModifier(int mod)
    {
        // CSI modifier encoding: value = 1 + (shift ? 1 : 0) + (alt ? 2 : 0) + (ctrl ? 4 : 0) + (super ? 8 : 0)
        // Super = Cmd on macOS. Map Cmd → Ctrl so Cmd+C/V → Copy/Paste.
        mod -= 1;
        KeyCode result = KeyCode.Null;
        if ((mod & 1) != 0) result |= KeyCode.ShiftMask;
        if ((mod & 2) != 0) result |= KeyCode.AltMask;
        if ((mod & 4) != 0 || (mod & 8) != 0) result |= KeyCode.CtrlMask;
        return result;
    }

    private static int ParseCsiParams(ReadOnlySpan<byte> data, Span<int> output)
    {
        int count = 0;
        int current = 0;
        bool hasValue = false;

        foreach (byte b in data)
        {
            if (b >= (byte)'0' && b <= (byte)'9')
            {
                current = current * 10 + (b - '0');
                hasValue = true;
            }
            else if (b == (byte)';')
            {
                if (count < output.Length)
                    output[count++] = current;
                current = 0;
                hasValue = false;
            }
        }

        if (hasValue && count < output.Length)
            output[count++] = current;

        return count;
    }

    private static MouseEventArgs? ParseSgrMouse(ReadOnlySpan<byte> data, bool pressed)
    {
        // Format: Cb;Cx;Cy (after stripping '<')
        Span<int> nums = stackalloc int[3];
        int count = ParseCsiParams(data, nums);
        if (count < 3) return null;

        int cb = nums[0];
        int cx = nums[1] - 1; // 1-based to 0-based
        int cy = nums[2] - 1;

        var flags = MouseFlags.None;

        int button = cb & 0x03;
        bool motion = (cb & 0x20) != 0;
        bool shift = (cb & 0x04) != 0;
        bool alt = (cb & 0x08) != 0;
        bool ctrl = (cb & 0x10) != 0;

        if (motion)
        {
            flags |= MouseFlags.ReportMousePosition;
            if (button == 0) flags |= MouseFlags.Button1Pressed;
            else if (button == 1) flags |= MouseFlags.Button2Pressed;
            else if (button == 2) flags |= MouseFlags.Button3Pressed;
        }
        else if ((cb & 0x40) != 0)
        {
            // Scroll
            flags |= button == 0 ? MouseFlags.WheeledUp : MouseFlags.WheeledDown;
        }
        else
        {
            if (pressed)
            {
                flags |= button switch
                {
                    0 => MouseFlags.Button1Pressed,
                    1 => MouseFlags.Button2Pressed,
                    2 => MouseFlags.Button3Pressed,
                    _ => MouseFlags.None
                };
            }
            else
            {
                flags |= button switch
                {
                    0 => MouseFlags.Button1Released,
                    1 => MouseFlags.Button2Released,
                    2 => MouseFlags.Button3Released,
                    _ => MouseFlags.None
                };
            }
        }

        if (shift) flags |= MouseFlags.ButtonShift;
        if (alt) flags |= MouseFlags.ButtonAlt;
        if (ctrl) flags |= MouseFlags.ButtonCtrl;

        return new MouseEventArgs
        {
            Position = new(cx, cy),
            Flags = flags
        };
    }
}
