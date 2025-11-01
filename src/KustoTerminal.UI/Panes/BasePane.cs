using System;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Drawing;

namespace KustoTerminal.UI.Panes;

public abstract class BasePane : View
{
#pragma warning disable CS0067 // Event is never used but may be implemented by derived classes
    public event EventHandler<bool>? FocusChanged;
#pragma warning restore CS0067
}
