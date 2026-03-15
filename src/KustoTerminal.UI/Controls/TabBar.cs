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
    private bool _isEditorFocused;

    public event EventHandler<int>? TabSelected;
    public event EventHandler<int>? TabCloseRequested;
    public event EventHandler? NewTabRequested;

    public bool IsEditorFocused
    {
        get => _isEditorFocused;
        set
        {
            if (_isEditorFocused != value)
            {
                _isEditorFocused = value;
                SetNeedsDraw();
            }
        }
    }

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

        // Use the resolved color scheme from Terminal.Gui (matches FrameView titles)
        var scheme = GetScheme();
        var inactiveTabAttr = new Attribute(Color.DarkGray, scheme.Normal.Background);
        var normalAttr = _isEditorFocused ? inactiveTabAttr : inactiveTabAttr;
        var activeAttr = _isEditorFocused ? scheme.Focus : new Attribute(Color.Cyan, scheme.Normal.Background);
        var bgAttr = scheme.Normal;

        // Fill entire row with background first
        driver.SetAttribute(bgAttr);
        for (int col = 0; col < viewport.Width; col++)
        {
            Move(col, 0);
            driver.AddStr(" ");
        }

        // Draw scroll indicator if needed
        if (_scrollOffset > 0)
        {
            driver.SetAttribute(normalAttr);
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
                driver.SetAttribute(activeAttr);
            }
            else
            {
                driver.SetAttribute(normalAttr);
            }

            Move(x, 0);
            var tabText = $" {displayTitle} × ";
            driver.AddStr(tabText);
            x += tabText.Length;

            // Separator
            driver.SetAttribute(bgAttr);
            Move(x, 0);
            driver.AddStr(" ");
            x += 1;
        }

        // Draw [+] button
        if (x < viewport.Width - 3)
        {
            Move(x, 0);
            driver.SetAttribute(_isEditorFocused ? scheme.HotNormal : scheme.Disabled);
            driver.AddStr(" + ");
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
