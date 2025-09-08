using System;
using Terminal.Gui;

namespace KustoTerminal.UI.Panes
{
    public abstract class BasePane : View
    {
        private bool _isHighlighted = false;
        protected ColorScheme _normalColorScheme;
        protected ColorScheme _highlightedColorScheme;

        public event EventHandler<bool>? FocusChanged;

        protected BasePane()
        {
            InitializeColorSchemes();
            SetupFocusHandling();
        }

        private void InitializeColorSchemes()
        {
            _normalColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.White, Color.Black),
                HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.Black),
                Disabled = new Terminal.Gui.Attribute(Color.Black, Color.Black)
            };

            _highlightedColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                HotNormal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                Disabled = new Terminal.Gui.Attribute(Color.Black, Color.Black)
            };

            ColorScheme = _normalColorScheme;
        }

        private void SetupFocusHandling()
        {
            Enter += OnPaneEnter;
            Leave += OnPaneLeave;
        }

        private void OnPaneEnter(FocusEventArgs obj)
        {
            SetHighlighted(true);
            OnFocusEnter();
        }

        private void OnPaneLeave(FocusEventArgs obj)
        {
            SetHighlighted(false);
            OnFocusLeave();
        }

        protected virtual void OnFocusEnter()
        {
            // Override in derived classes for custom focus behavior
        }

        protected virtual void OnFocusLeave()
        {
            // Override in derived classes for custom focus behavior
        }

        public void SetHighlighted(bool highlighted)
        {
            if (_isHighlighted == highlighted) return;

            _isHighlighted = highlighted;
            ApplyHighlighting();
            FocusChanged?.Invoke(this, highlighted);
        }

        protected virtual void ApplyHighlighting()
        {
            // Apply highlighting to all child controls
            foreach (View child in Subviews)
            {
                ApplyHighlightingToControl(child);
            }

            SetNeedsDisplay();
        }

        protected virtual void ApplyHighlightingToControl(View control)
        {
            if (control == null) return;

            // Apply appropriate color scheme based on highlight state
            var colorScheme = _isHighlighted ? GetHighlightedSchemeForControl(control) : GetNormalSchemeForControl(control);
            control.ColorScheme = colorScheme;

            // Recursively apply to child controls
            foreach (View child in control.Subviews)
            {
                ApplyHighlightingToControl(child);
            }
        }

        protected virtual ColorScheme GetNormalSchemeForControl(View control)
        {
            return control switch
            {
                Button => new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.Black, Color.Black),
                    HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.Black),
                    HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                    Disabled = new Terminal.Gui.Attribute(Color.Black, Color.Black)
                },
                TextView => new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    HotNormal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    HotFocus = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    Disabled = new Terminal.Gui.Attribute(Color.Black, Color.Black)
                },
                ListView => new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                    HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                    HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                    Disabled = new Terminal.Gui.Attribute(Color.Black, Color.Black)
                },
                TableView => new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                    HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                    HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                    Disabled = new Terminal.Gui.Attribute(Color.Black, Color.Black)
                },
                _ => _normalColorScheme
            };
        }

        protected virtual ColorScheme GetHighlightedSchemeForControl(View control)
        {
            return control switch
            {
                Button => new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.Black, Color.Black),
                    HotNormal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.Black),
                    Disabled = new Terminal.Gui.Attribute(Color.Black, Color.Black)
                },
                TextView => new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.Black, Color.Black),
                    HotNormal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                    HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.Black),
                    Disabled = new Terminal.Gui.Attribute(Color.Black, Color.Black)
                },
                ListView => new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                    HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                    HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                    Disabled = new Terminal.Gui.Attribute(Color.Black, Color.Black)
                },
                TableView => new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                    HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                    HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                    Disabled = new Terminal.Gui.Attribute(Color.Black, Color.Black)
                },
                _ => _highlightedColorScheme
            };
        }

        public bool IsHighlighted => _isHighlighted;

        // Static methods for creating consistent color schemes across the application
        public static ColorScheme CreateStandardColorScheme()
        {
            return new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.White, Color.Black),
                HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.Black),
                Disabled = new Terminal.Gui.Attribute(Color.Black, Color.Black)
            };
        }

        public static ColorScheme CreateHighlightedColorScheme()
        {
            return new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                HotNormal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                Disabled = new Terminal.Gui.Attribute(Color.Black, Color.Black)
            };
        }

        public static ColorScheme CreateButtonColorScheme()
        {
            return new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.Black, Color.Black),
                HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                Disabled = new Terminal.Gui.Attribute(Color.Black, Color.Black)
            };
        }

        public static ColorScheme CreateTextFieldColorScheme()
        {
            return new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                HotNormal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                Disabled = new Terminal.Gui.Attribute(Color.Black, Color.Black)
            };
        }

        public static ColorScheme CreateActiveFrameColorScheme()
        {
            return new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                HotNormal = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                Disabled = new Terminal.Gui.Attribute(Color.Black, Color.Black)
            };
        }

        // Specialized color schemes for specific control types
        public static ColorScheme CreateShortcutLabelColorScheme()
        {
            return new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                HotNormal = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                Disabled = new Terminal.Gui.Attribute(Color.Gray, Color.Black)
            };
        }

        public static ColorScheme CreateTextViewNormalColorScheme()
        {
            return new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.White, Color.Black),
                HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow), // Selected text
                HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),  // Selected text when focused
                Disabled = new Terminal.Gui.Attribute(Color.Gray, Color.Black)
            };
        }

        public static ColorScheme CreateTextViewHighlightedColorScheme()
        {
            return new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow), // Selected text
                HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),  // Selected text when focused
                Disabled = new Terminal.Gui.Attribute(Color.Gray, Color.Black)
            };
        }

        // Common control focus handling methods
        protected void SetupCommonElementFocusHandlers(params View[] controls)
        {
            foreach (var control in controls)
            {
                if (control != null)
                {
                    control.Enter += OnElementFocusEnter;
                    control.Leave += OnElementFocusLeave;
                }
            }
        }

        protected virtual void OnElementFocusEnter(FocusEventArgs args)
        {
            // When any element in this pane gets focus, highlight the entire pane
            SetHighlighted(true);
        }

        protected virtual void OnElementFocusLeave(FocusEventArgs args)
        {
            // Check if focus is moving to another element within this pane
            Application.MainLoop.Invoke(() =>
            {
                var focusedView = Application.Top.MostFocused;
                bool stillInPane = IsChildOf(focusedView, this);
                
                if (!stillInPane)
                {
                    SetHighlighted(false);
                }
            });
        }

        protected bool IsChildOf(View? child, View parent)
        {
            if (child == null) return false;
            if (child == parent) return true;
            
            foreach (View subview in parent.Subviews)
            {
                if (IsChildOf(child, subview))
                    return true;
            }
            return false;
        }

        // Apply specific color scheme to a control by type
        protected void ApplyColorSchemeToControl(View control, string controlType, bool isHighlighted = false)
        {
            var colorScheme = controlType.ToLowerInvariant() switch
            {
                "shortcut" => CreateShortcutLabelColorScheme(),
                "textview_normal" => CreateTextViewNormalColorScheme(),
                "textview_highlighted" => CreateTextViewHighlightedColorScheme(),
                "button" => CreateButtonColorScheme(),
                "textfield" => CreateTextFieldColorScheme(),
                "activeframe" => CreateActiveFrameColorScheme(),
                _ => isHighlighted ? _highlightedColorScheme : _normalColorScheme
            };
            
            control.ColorScheme = colorScheme;
        }
    }
}