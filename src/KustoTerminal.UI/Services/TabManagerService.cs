using System;
using System.Collections.Generic;
using System.Linq;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;
using KustoTerminal.Language.Services;
using KustoTerminal.UI.Models;
using KustoTerminal.UI.SyntaxHighlighting;

namespace KustoTerminal.UI.Services;

public class TabManagerService : IDisposable
{
    public const int MaxTabs = 20;

    private readonly IUserSettingsManager? _userSettingsManager;
    private readonly SyntaxHighlighter _syntaxHighlighter;
    private readonly LanguageService _languageService;
    private readonly HtmlSyntaxHighlighter _htmlSyntaxHighlighter;

    private readonly List<QueryTab> _tabs = new();
    private int _activeTabIndex = -1;

    public IReadOnlyList<QueryTab> Tabs => _tabs.AsReadOnly();
    public QueryTab? ActiveTab => _activeTabIndex >= 0 && _activeTabIndex < _tabs.Count
        ? _tabs[_activeTabIndex]
        : null;
    public int ActiveTabIndex => _activeTabIndex;

    public event EventHandler<QueryTab>? ActiveTabChanged;
    public event EventHandler<QueryTab>? TabCreated;
    public event EventHandler<QueryTab>? TabClosed;

    public TabManagerService(
        IUserSettingsManager? userSettingsManager,
        SyntaxHighlighter syntaxHighlighter,
        LanguageService languageService,
        HtmlSyntaxHighlighter htmlSyntaxHighlighter)
    {
        _userSettingsManager = userSettingsManager;
        _syntaxHighlighter = syntaxHighlighter;
        _languageService = languageService;
        _htmlSyntaxHighlighter = htmlSyntaxHighlighter;
    }

    public QueryTab? CreateTab(TabState? state = null)
    {
        if (_tabs.Count >= MaxTabs) return null;

        state ??= new TabState { Order = _tabs.Count };

        var tab = new QueryTab(
            state,
            _userSettingsManager,
            _syntaxHighlighter,
            _languageService,
            _htmlSyntaxHighlighter);

        _tabs.Add(tab);

        TabCreated?.Invoke(this, tab);

        // Activate the newly created tab
        ActivateTab(_tabs.Count - 1);

        return tab;
    }

    public void CloseTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;

        // Prevent closing the last tab
        if (_tabs.Count <= 1) return;

        var tab = _tabs[index];
        _tabs.RemoveAt(index);

        TabClosed?.Invoke(this, tab);

        // Adjust active index
        if (_activeTabIndex >= _tabs.Count)
        {
            _activeTabIndex = _tabs.Count - 1;
        }
        else if (index < _activeTabIndex)
        {
            _activeTabIndex--;
        }
        else if (index == _activeTabIndex)
        {
            // If we closed the active tab, activate the one at the same position (or previous)
            _activeTabIndex = Math.Min(index, _tabs.Count - 1);
        }

        ActiveTabChanged?.Invoke(this, _tabs[_activeTabIndex]);

        tab.Dispose();
    }

    public void CloseTab(QueryTab tab)
    {
        var index = _tabs.IndexOf(tab);
        if (index >= 0) CloseTab(index);
    }

    public void ActivateTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        if (index == _activeTabIndex) return;

        // Sync state from the currently active tab before switching
        ActiveTab?.SyncStateFromUI();

        _activeTabIndex = index;
        ActiveTabChanged?.Invoke(this, _tabs[_activeTabIndex]);
    }

    public void ActivateNextTab()
    {
        if (_tabs.Count <= 1) return;
        var next = (_activeTabIndex + 1) % _tabs.Count;
        ActivateTab(next);
    }

    public void ActivatePreviousTab()
    {
        if (_tabs.Count <= 1) return;
        var prev = (_activeTabIndex - 1 + _tabs.Count) % _tabs.Count;
        ActivateTab(prev);
    }

    public void ActivateTabByNumber(int number)
    {
        // 1-based: Alt+1 = tab index 0
        var index = number - 1;
        if (index >= 0 && index < _tabs.Count)
        {
            ActivateTab(index);
        }
    }

    public List<TabState> GetTabStates()
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].SyncStateFromUI();
            _tabs[i].State.Order = i;
            _tabs[i].State.IsActive = (i == _activeTabIndex);
        }
        return _tabs.Select(t => t.State).ToList();
    }

    public void RestoreTabs(List<TabState> states, IConnectionManager connectionManager)
    {
        if (states == null || states.Count == 0)
        {
            if (_tabs.Count == 0) CreateTab();
            return;
        }

        // Dispose existing tabs before restoring
        foreach (var existingTab in _tabs)
        {
            TabClosed?.Invoke(this, existingTab);
            existingTab.Dispose();
        }
        _tabs.Clear();
        _activeTabIndex = -1;

        var ordered = states.OrderBy(s => s.Order).ToList();
        int activeIndex = 0;

        for (int i = 0; i < ordered.Count && i < MaxTabs; i++)
        {
            var state = ordered[i];
            var tab = new QueryTab(
                state,
                _userSettingsManager,
                _syntaxHighlighter,
                _languageService,
                _htmlSyntaxHighlighter);

            _tabs.Add(tab);
            tab.RestoreQueryText();
            TabCreated?.Invoke(this, tab);

            if (state.IsActive) activeIndex = i;
        }

        if (_tabs.Count == 0)
        {
            CreateTab();
            return;
        }

        _activeTabIndex = activeIndex;
        ActiveTabChanged?.Invoke(this, _tabs[_activeTabIndex]);
    }

    public List<string> GetTabTitles()
    {
        return _tabs.Select(t => t.DisplayTitle).ToList();
    }

    public void Dispose()
    {
        foreach (var tab in _tabs)
        {
            tab.Dispose();
        }
        _tabs.Clear();
    }
}
