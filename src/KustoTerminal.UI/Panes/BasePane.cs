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
                HotNormal = new Terminal.Gui.Attribute(Color.BrightBlue, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.BrightBlue, Color.Black),
                Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
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
                    Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightBlue),
                    HotNormal = new Terminal.Gui.Attribute(Color.BrightBlue, Color.Black),
                    HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                    Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
                },
                TextView => new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    HotNormal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    HotFocus = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
                },
                ListView => new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                    HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                    HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                    Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
                },
                TableView => new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                    HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                    HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                    Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
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
                    Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
                },
                TextView => new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                    HotNormal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                    HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                    Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
                },
                ListView => new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                    HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                    HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                    Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
                },
                TableView => new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                    HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                    HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                    Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
                },
                _ => _highlightedColorScheme
            };
        }

        public bool IsHighlighted => _isHighlighted;
    }
}