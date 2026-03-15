using System;
using System.Collections.Generic;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using KustoTerminal.UI.Common;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace KustoTerminal.UI.Controls;

public class TabBar : View
{
    private readonly List<TabItem> _tabs = new();
    private int _activeIndex = -1;
    private int _scrollOffset = 0;

    public event EventHandler<int>? TabSelected;
    public event EventHandler<int>? TabCloseRequested;
    public event EventHandler? NewTabRequested;

    public int ActiveIndex
    {
        get => _activeIndex;
        set
        {
            if (value >= 0 && value < _tabs.Count && value != _activeIndex)
            {
                _activeIndex = value;
                EnsureActiveTabVisible();
                SetNeedsDraw();
            }
        }
    }

    public int TabCount => _tabs.Count;

    public TabBar()
    {
        Height = 1;
        Width = Dim.Fill();
        CanFocus = false;
    }

    public void SetTabs(List<string> titles, int activeIndex)
    {
        _tabs.Clear();
        foreach (var title in titles)
        {
            _tabs.Add(new TabItem { Title = title });
        }
        _activeIndex = activeIndex;
        EnsureActiveTabVisible();
        SetNeedsDraw();
    }

    public void UpdateTabTitle(int index, string title)
    {
        if (index >= 0 && index < _tabs.Count)
        {
            _tabs[index].Title = title;
            SetNeedsDraw();
        }
    }

    private void EnsureActiveTabVisible()
    {
        if (_activeIndex < _scrollOffset)
        {
            _scrollOffset = _activeIndex;
        }

        // Approximate: each tab takes ~15 chars, calculate how many fit
        var availableWidth = Frame.Width - 4; // reserve space for [+]
        if (availableWidth <= 0) return;

        var currentPos = 0;
        for (int i = _scrollOffset; i <= _activeIndex && i < _tabs.Count; i++)
        {
            currentPos += GetTabRenderWidth(_tabs[i].Title);
        }

        while (currentPos > availableWidth && _scrollOffset < _activeIndex)
        {
            currentPos -= GetTabRenderWidth(_tabs[_scrollOffset].Title);
            _scrollOffset++;
        }
    }

    private static int GetTabRenderWidth(string title)
    {
        // Format: " Title × " with surrounding space + close button
        var displayTitle = title.Length > 18 ? title[..15] + "..." : title;
        return displayTitle.Length + 5; // " [title ×] "
    }

    protected override bool OnDrawingContent()
    {
        var driver = Application.Driver;
        if (driver == null) return true;

        var viewport = Viewport;
        var x = 0;

        // Draw scroll indicator if needed
        if (_scrollOffset > 0)
        {
            Move(x, 0);
            driver.AddStr("◀");
            x += 1;
        }

        // Draw tabs
        for (int i = _scrollOffset; i < _tabs.Count && x < viewport.Width - 4; i++)
        {
            var tab = _tabs[i];
            var isActive = (i == _activeIndex);
            var displayTitle = tab.Title.Length > 18 ? tab.Title[..15] + "..." : tab.Title;

            if (isActive)
            {
                driver.SetAttribute(new Attribute(Color.White, Color.Blue));
            }
            else
            {
                driver.SetAttribute(new Attribute(Color.Gray, Color.Black));
            }

            Move(x, 0);
            var tabText = $" {displayTitle} × ";
            driver.AddStr(tabText);
            x += tabText.Length;

            // Reset attribute
            driver.SetAttribute(new Attribute(Color.White, Color.Black));
        }

        // Draw [+] button
        if (x < viewport.Width - 3)
        {
            Move(x, 0);
            driver.SetAttribute(new Attribute(Color.Green, Color.Black));
            driver.AddStr(" + ");
            driver.SetAttribute(new Attribute(Color.White, Color.Black));
        }

        // Fill rest of line
        x += 3;
        driver.SetAttribute(new Attribute(Color.White, Color.Black));
        while (x < viewport.Width)
        {
            Move(x, 0);
            driver.AddStr(" ");
            x++;
        }

        return true;
    }

    protected override bool OnMouseEvent(MouseEventArgs mouseEvent)
    {
        if (mouseEvent.Flags.HasFlag(MouseFlags.Button1Clicked))
        {
            var clickX = mouseEvent.Position.X;
            HandleClick(clickX);
            return true;
        }
        return base.OnMouseEvent(mouseEvent);
    }

    private void HandleClick(int clickX)
    {
        var x = _scrollOffset > 0 ? 1 : 0;

        for (int i = _scrollOffset; i < _tabs.Count; i++)
        {
            var tab = _tabs[i];
            var displayTitle = tab.Title.Length > 18 ? tab.Title[..15] + "..." : tab.Title;
            var tabText = $" {displayTitle} × ";
            var tabWidth = tabText.Length;

            if (clickX >= x && clickX < x + tabWidth)
            {
                // Check if click is on the close button (× character, 2 chars from end)
                if (clickX >= x + tabWidth - 3)
                {
                    TabCloseRequested?.Invoke(this, i);
                }
                else
                {
                    TabSelected?.Invoke(this, i);
                }
                return;
            }
            x += tabWidth;
        }

        // Check if click is on [+]
        if (clickX >= x && clickX < x + 3)
        {
            NewTabRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private class TabItem
    {
        public string Title { get; set; } = string.Empty;
    }
}
