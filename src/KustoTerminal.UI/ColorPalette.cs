using Terminal.Gui;

namespace KustoTerminal.UI
{
    /// <summary>
    /// Centralized color palette for the entire application.
    /// Change colors here to apply globally across all UI components.
    /// </summary>
    public static class ColorPalette
    {
        // Primary colors
        public static readonly Color Primary = Color.White;
        public static readonly Color Background = Color.Black;
        public static readonly Color Accent = Color.BrightCyan;
        public static readonly Color Highlight = Color.BrightYellow;
        public static readonly Color Disabled = Color.Gray;

        // Semantic colors - these can be easily changed for different themes
        public static readonly Color NormalText = Primary;
        public static readonly Color NormalBackground = Background;
        
        public static readonly Color FocusedText = Primary;
        public static readonly Color FocusedBackground = Background;
        
        public static readonly Color HighlightedText = Accent;
        public static readonly Color HighlightedBackground = Background;
        
        public static readonly Color SelectionText = Background;
        public static readonly Color SelectionBackground = Accent;
        
        public static readonly Color HotText = Background;
        public static readonly Color HotBackground = Highlight;
        
        public static readonly Color DisabledText = Disabled;
        public static readonly Color DisabledBackground = Background;
        
        public static readonly Color FrameActiveText = Highlight;
        public static readonly Color FrameActiveBackground = Background;
        
        public static readonly Color ShortcutText = Highlight;
        public static readonly Color ShortcutBackground = Background;

        // Button-specific colors
        public static readonly Color ButtonNormalText = Primary;
        public static readonly Color ButtonNormalBackground = Background;
        public static readonly Color ButtonFocusedText = Background;
        public static readonly Color ButtonFocusedBackground = Background;
        public static readonly Color ButtonHotText = Background;
        public static readonly Color ButtonHotBackground = Accent;

        // TextField-specific colors
        public static readonly Color TextFieldNormalText = Primary;
        public static readonly Color TextFieldNormalBackground = Background;
        public static readonly Color TextFieldFocusedText = Background;
        public static readonly Color TextFieldFocusedBackground = Accent;

        // TextView-specific colors
        public static readonly Color TextViewNormalText = Primary;
        public static readonly Color TextViewNormalBackground = Background;
        public static readonly Color TextViewSelectedText = Background;
        public static readonly Color TextViewSelectedBackground = Highlight;

        // ListView/TableView-specific colors
        public static readonly Color ListNormalText = Primary;
        public static readonly Color ListNormalBackground = Background;
        public static readonly Color ListFocusedText = Background;
        public static readonly Color ListFocusedBackground = Accent;
        public static readonly Color ListSelectedText = Background;
        public static readonly Color ListSelectedBackground = Highlight;
    }
}