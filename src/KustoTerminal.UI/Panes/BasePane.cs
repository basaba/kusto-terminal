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
        public event EventHandler<bool>? FocusChanged;
    }
}