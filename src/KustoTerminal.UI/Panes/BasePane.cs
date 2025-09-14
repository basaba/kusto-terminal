using System;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Drawing;

namespace KustoTerminal.UI.Panes
{
    public abstract class BasePane : View
    {
        private bool _isHighlighted = false;
        // protected ColorScheme _normalColorScheme;
        // protected ColorScheme _highlightedColorScheme;

        public event EventHandler<bool>? FocusChanged;

        protected BasePane()
        {
            InitializeColorSchemes();
            SetupFocusHandling();
        }

        private void InitializeColorSchemes()
        {
            // _normalColorScheme = ColorSchemeFactory.CreateStandard();
            // _highlightedColorScheme = ColorSchemeFactory.CreateHighlighted();
            // ColorScheme = _normalColorScheme;
        }

        private void SetupFocusHandling()
        {
            // Enter += OnPaneEnter;
            // Leave += OnPaneLeave;
        }

        // private void OnPaneEnter(FocusEventArgs obj)
        // {
        //     SetHighlighted(true);
        //     OnFocusEnter();
        // }

        // private void OnPaneLeave(FocusEventArgs obj)
        // {
        //     SetHighlighted(false);
        //     OnFocusLeave();
        // }

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
            // // Apply highlighting to all child controls
            // foreach (View child in Subviews)
            // {
            //     ApplyHighlightingToControl(child);
            // }

            // SetNeedsDisplay();
        }

        protected virtual void ApplyHighlightingToControl(View control)
        {
            // if (control == null) return;

            // // Apply appropriate color scheme based on highlight state
            // var colorScheme = _isHighlighted ? GetHighlightedSchemeForControl(control) : GetNormalSchemeForControl(control);
            // // control.ColorScheme = colorScheme;

            // // Recursively apply to child controls
            // foreach (View child in control.Subviews)
            // {
            //     ApplyHighlightingToControl(child);
            // }
        }

        // protected virtual ColorScheme GetNormalSchemeForControl(View control)
        // {
        //     return control switch
        //     {
        //         Button => ColorSchemeFactory.CreateButton(),
        //         TextView => ColorSchemeFactory.CreateTextViewNormal(),
        //         ListView => ColorSchemeFactory.CreateListNormal(),
        //         TableView => ColorSchemeFactory.CreateListNormal(),
        //         _ => _normalColorScheme
        //     };
        // }

        // protected virtual ColorScheme GetHighlightedSchemeForControl(View control)
        // {
        //     return control switch
        //     {
        //         Button => ColorSchemeFactory.CreateButton(),
        //         TextView => ColorSchemeFactory.CreateTextViewHighlighted(),
        //         ListView => ColorSchemeFactory.CreateListHighlighted(),
        //         TableView => ColorSchemeFactory.CreateListHighlighted(),
        //         _ => _highlightedColorScheme
        //     };
        // }

        public bool IsHighlighted => _isHighlighted;

        // Static methods for creating consistent color schemes across the application
        // These methods now delegate to the centralized ColorSchemeFactory
        // public static ColorScheme CreateStandardColorScheme()
        // {
        //     return ColorSchemeFactory.CreateStandard();
        // }

        // public static ColorScheme CreateHighlightedColorScheme()
        // {
        //     return ColorSchemeFactory.CreateHighlighted();
        // }

        // public static ColorScheme CreateButtonColorScheme()
        // {
        //     return ColorSchemeFactory.CreateButton();
        // }

        // public static ColorScheme CreateTextFieldColorScheme()
        // {
        //     return ColorSchemeFactory.CreateTextField();
        // }

        // public static ColorScheme CreateActiveFrameColorScheme()
        // {
        //     return ColorSchemeFactory.CreateActiveFrame();
        // }

        // public static ColorScheme CreateShortcutLabelColorScheme()
        // {
        //     return ColorSchemeFactory.CreateShortcutLabel();
        // }

        // public static ColorScheme CreateTextViewNormalColorScheme()
        // {
        //     return ColorSchemeFactory.CreateTextViewNormal();
        // }

        // public static ColorScheme CreateTextViewHighlightedColorScheme()
        // {
        //     return ColorSchemeFactory.CreateTextViewHighlighted();
        // }

        // Common control focus handling methods
        protected void SetupCommonElementFocusHandlers(params View[] controls)
        {
            // foreach (var control in controls)
            // {
            //     if (control != null)
            //     {
            //         control.Enter += OnElementFocusEnter;
            //         control.Leave += OnElementFocusLeave;
            //     }
            // }
        }

        // protected virtual void OnElementFocusEnter(FocusEventArgs args)
        // {
        //     // When any element in this pane gets focus, highlight the entire pane
        //     SetHighlighted(true);
        // }

        // protected virtual void OnElementFocusLeave(FocusEventArgs args)
        // {
        //     // Check if focus is moving to another element within this pane
        //     Application.MainLoop.Invoke(() =>
        //     {
        //         var focusedView = Application.Top.MostFocused;
        //         bool stillInPane = IsChildOf(focusedView, this);
                
        //         if (!stillInPane)
        //         {
        //             SetHighlighted(false);
        //         }
        //     });
        // }

        // protected bool IsChildOf(View? child, View parent)
        // {
        //     if (child == null) return false;
        //     if (child == parent) return true;
            
        //     foreach (View subview in parent.Subviews)
        //     {
        //         if (IsChildOf(child, subview))
        //             return true;
        //     }
        //     return false;
        // }

        // Apply specific color scheme to a control by type
        protected void ApplyColorSchemeToControl(View control, string controlType, bool isHighlighted = false)
        {
            // var colorScheme = ColorSchemeFactory.CreateForControl(controlType, isHighlighted);
            // control.ColorScheme = colorScheme;
        }
    }
}