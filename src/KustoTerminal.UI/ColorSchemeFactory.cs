using Terminal.Gui;

namespace KustoTerminal.UI
{
    /// <summary>
    /// Factory for creating consistent color schemes across the application.
    /// All color schemes are created using the centralized ColorPalette.
    /// </summary>
    // public static class ColorSchemeFactory
    // {
    //     /// <summary>
    //     /// Creates the standard color scheme for normal UI elements.
    //     /// </summary>
    //     public static ColorScheme CreateStandard()
    //     {
    //         return new ColorScheme()
    //         {
    //             Normal = new Terminal.Gui.Attribute(ColorPalette.NormalText, ColorPalette.NormalBackground),
    //             Focus = new Terminal.Gui.Attribute(ColorPalette.FocusedText, ColorPalette.FocusedBackground),
    //             HotNormal = new Terminal.Gui.Attribute(ColorPalette.NormalText, ColorPalette.NormalBackground),
    //             HotFocus = new Terminal.Gui.Attribute(ColorPalette.FocusedText, ColorPalette.FocusedBackground),
    //             Disabled = new Terminal.Gui.Attribute(ColorPalette.DisabledText, ColorPalette.DisabledBackground)
    //         };
    //     }

    //     /// <summary>
    //     /// Creates the highlighted color scheme for active/focused panes.
    //     /// </summary>
    //     public static ColorScheme CreateHighlighted()
    //     {
    //         return new ColorScheme()
    //         {
    //             Normal = new Terminal.Gui.Attribute(ColorPalette.HighlightedText, ColorPalette.HighlightedBackground),
    //             Focus = new Terminal.Gui.Attribute(ColorPalette.HotText, ColorPalette.HotBackground),
    //             HotNormal = new Terminal.Gui.Attribute(ColorPalette.HighlightedText, ColorPalette.HighlightedBackground),
    //             HotFocus = new Terminal.Gui.Attribute(ColorPalette.HotText, ColorPalette.HotBackground),
    //             Disabled = new Terminal.Gui.Attribute(ColorPalette.DisabledText, ColorPalette.DisabledBackground)
    //         };
    //     }

    //     /// <summary>
    //     /// Creates a color scheme for buttons.
    //     /// </summary>
    //     public static ColorScheme CreateButton()
    //     {
    //         return new ColorScheme()
    //         {
    //             Normal = new Terminal.Gui.Attribute(ColorPalette.ButtonNormalText, ColorPalette.ButtonNormalBackground),
    //             Focus = new Terminal.Gui.Attribute(ColorPalette.ButtonFocusedText, ColorPalette.ButtonFocusedBackground),
    //             HotNormal = new Terminal.Gui.Attribute(ColorPalette.ButtonNormalText, ColorPalette.ButtonNormalBackground),
    //             HotFocus = new Terminal.Gui.Attribute(ColorPalette.ButtonHotText, ColorPalette.ButtonHotBackground),
    //             Disabled = new Terminal.Gui.Attribute(ColorPalette.DisabledText, ColorPalette.DisabledBackground)
    //         };
    //     }

    //     /// <summary>
    //     /// Creates a color scheme for text fields.
    //     /// </summary>
    //     public static ColorScheme CreateTextField()
    //     {
    //         return new ColorScheme()
    //         {
    //             Normal = new Terminal.Gui.Attribute(ColorPalette.TextFieldNormalText, ColorPalette.TextFieldNormalBackground),
    //             Focus = new Terminal.Gui.Attribute(ColorPalette.TextFieldFocusedText, ColorPalette.TextFieldFocusedBackground),
    //             HotNormal = new Terminal.Gui.Attribute(ColorPalette.TextFieldNormalText, ColorPalette.TextFieldNormalBackground),
    //             HotFocus = new Terminal.Gui.Attribute(ColorPalette.TextFieldFocusedText, ColorPalette.TextFieldFocusedBackground),
    //             Disabled = new Terminal.Gui.Attribute(ColorPalette.DisabledText, ColorPalette.DisabledBackground)
    //         };
    //     }

    //     /// <summary>
    //     /// Creates a color scheme for active frame borders.
    //     /// </summary>
    //     public static ColorScheme CreateActiveFrame()
    //     {
    //         return new ColorScheme()
    //         {
    //             Normal = new Terminal.Gui.Attribute(ColorPalette.FrameActiveText, ColorPalette.FrameActiveBackground),
    //             Focus = new Terminal.Gui.Attribute(ColorPalette.FrameActiveText, ColorPalette.FrameActiveBackground),
    //             HotNormal = new Terminal.Gui.Attribute(ColorPalette.FrameActiveText, ColorPalette.FrameActiveBackground),
    //             HotFocus = new Terminal.Gui.Attribute(ColorPalette.FrameActiveText, ColorPalette.FrameActiveBackground),
    //             Disabled = new Terminal.Gui.Attribute(ColorPalette.DisabledText, ColorPalette.DisabledBackground)
    //         };
    //     }

    //     /// <summary>
    //     /// Creates a color scheme for shortcut labels.
    //     /// </summary>
    //     public static ColorScheme CreateShortcutLabel()
    //     {
    //         return new ColorScheme()
    //         {
    //             Normal = new Terminal.Gui.Attribute(ColorPalette.ShortcutText, ColorPalette.ShortcutBackground),
    //             Focus = new Terminal.Gui.Attribute(ColorPalette.ShortcutText, ColorPalette.ShortcutBackground),
    //             HotNormal = new Terminal.Gui.Attribute(ColorPalette.ShortcutText, ColorPalette.ShortcutBackground),
    //             HotFocus = new Terminal.Gui.Attribute(ColorPalette.ShortcutText, ColorPalette.ShortcutBackground),
    //             Disabled = new Terminal.Gui.Attribute(ColorPalette.DisabledText, ColorPalette.DisabledBackground)
    //         };
    //     }

    //     /// <summary>
    //     /// Creates a color scheme for warning messages.
    //     /// </summary>
    //     public static ColorScheme CreateWarning()
    //     {
    //         return new ColorScheme()
    //         {
    //             Normal = new Terminal.Gui.Attribute(ColorPalette.WarningText, ColorPalette.WarningBackground),
    //             Focus = new Terminal.Gui.Attribute(ColorPalette.WarningText, ColorPalette.WarningBackground),
    //             HotNormal = new Terminal.Gui.Attribute(ColorPalette.WarningText, ColorPalette.WarningBackground),
    //             HotFocus = new Terminal.Gui.Attribute(ColorPalette.WarningText, ColorPalette.WarningBackground),
    //             Disabled = new Terminal.Gui.Attribute(ColorPalette.DisabledText, ColorPalette.DisabledBackground)
    //         };
    //     }

    //     /// <summary>
    //     /// Creates a color scheme for text views in normal state.
    //     /// </summary>
    //     public static ColorScheme CreateTextViewNormal()
    //     {
    //         return new ColorScheme()
    //         {
    //             Normal = new Terminal.Gui.Attribute(ColorPalette.TextViewNormalText, ColorPalette.TextViewNormalBackground),
    //             Focus = new Terminal.Gui.Attribute(ColorPalette.TextViewNormalText, ColorPalette.TextViewNormalBackground),
    //             HotNormal = new Terminal.Gui.Attribute(ColorPalette.TextViewSelectedText, ColorPalette.TextViewSelectedBackground), // Selected text
    //             HotFocus = new Terminal.Gui.Attribute(ColorPalette.TextViewSelectedText, ColorPalette.TextViewSelectedBackground),  // Selected text when focused
    //             Disabled = new Terminal.Gui.Attribute(ColorPalette.DisabledText, ColorPalette.DisabledBackground)
    //         };
    //     }

    //     /// <summary>
    //     /// Creates a color scheme for text views in highlighted state.
    //     /// </summary>
    //     public static ColorScheme CreateTextViewHighlighted()
    //     {
    //         return new ColorScheme()
    //         {
    //             Normal = new Terminal.Gui.Attribute(ColorPalette.TextViewNormalText, ColorPalette.TextViewNormalBackground), // Keep normal text when pane is highlighted
    //             Focus = new Terminal.Gui.Attribute(ColorPalette.TextViewNormalText, ColorPalette.TextViewNormalBackground),  // Normal text when focused
    //             HotNormal = new Terminal.Gui.Attribute(ColorPalette.TextViewSelectedText, ColorPalette.TextViewSelectedBackground), // Selected text
    //             HotFocus = new Terminal.Gui.Attribute(ColorPalette.TextViewSelectedText, ColorPalette.TextViewSelectedBackground),  // Selected text when focused
    //             Disabled = new Terminal.Gui.Attribute(ColorPalette.DisabledText, ColorPalette.DisabledBackground)
    //         };
    //     }

    //     /// <summary>
    //     /// Creates a color scheme for lists and tables in normal state.
    //     /// </summary>
    //     public static ColorScheme CreateListNormal()
    //     {
    //         return new ColorScheme()
    //         {
    //             Normal = new Terminal.Gui.Attribute(ColorPalette.ListNormalText, ColorPalette.ListNormalBackground),
    //             Focus = new Terminal.Gui.Attribute(ColorPalette.ListFocusedText, ColorPalette.ListFocusedBackground),
    //             HotNormal = new Terminal.Gui.Attribute(ColorPalette.ListFocusedText, ColorPalette.ListFocusedBackground),
    //             HotFocus = new Terminal.Gui.Attribute(ColorPalette.ListSelectedText, ColorPalette.ListSelectedBackground),
    //             Disabled = new Terminal.Gui.Attribute(ColorPalette.DisabledText, ColorPalette.DisabledBackground)
    //         };
    //     }

    //     /// <summary>
    //     /// Creates a color scheme for lists and tables in highlighted state.
    //     /// </summary>
    //     public static ColorScheme CreateListHighlighted()
    //     {
    //         return new ColorScheme()
    //         {
    //             Normal = new Terminal.Gui.Attribute(ColorPalette.HighlightedText, ColorPalette.HighlightedBackground),
    //             Focus = new Terminal.Gui.Attribute(ColorPalette.ListSelectedText, ColorPalette.ListSelectedBackground),
    //             HotNormal = new Terminal.Gui.Attribute(ColorPalette.ListFocusedText, ColorPalette.ListFocusedBackground),
    //             HotFocus = new Terminal.Gui.Attribute(ColorPalette.ListSelectedText, ColorPalette.ListSelectedBackground),
    //             Disabled = new Terminal.Gui.Attribute(ColorPalette.DisabledText, ColorPalette.DisabledBackground)
    //         };
    //     }

    //     /// <summary>
    //     /// Creates a color scheme for a specific control type and state.
    //     /// </summary>
    //     /// <param name="controlType">The type of control (button, textview, etc.)</param>
    //     /// <param name="isHighlighted">Whether the control is in highlighted state</param>
    //     /// <returns>Appropriate color scheme for the control</returns>
    //     public static ColorScheme CreateForControl(string controlType, bool isHighlighted = false)
    //     {
    //         return controlType.ToLowerInvariant() switch
    //         {
    //             "button" => CreateButton(),
    //             "textfield" => CreateTextField(),
    //             "textview_normal" => CreateTextViewNormal(),
    //             "textview_highlighted" => CreateTextViewHighlighted(),
    //             "listview" or "tableview" => isHighlighted ? CreateListHighlighted() : CreateListNormal(),
    //             "shortcut" => CreateShortcutLabel(),
    //             "activeframe" => CreateActiveFrame(),
    //             "warning" => CreateWarning(),
    //             _ => isHighlighted ? CreateHighlighted() : CreateStandard()
    //         };
    //     }
    // }
}