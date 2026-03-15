using System;
using System.Threading;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;
using KustoTerminal.Core.Services;
using KustoTerminal.Language.Services;
using KustoTerminal.UI.AutoCompletion;
using KustoTerminal.UI.Panes;
using KustoTerminal.UI.SyntaxHighlighting;
using Terminal.Gui.ViewBase;

namespace KustoTerminal.UI.Models;

public class QueryTab : IDisposable
{
    public TabState State { get; }
    public QueryEditorPane EditorPane { get; }
    public ResultsPane ResultsPane { get; }
    public KustoConnection? Connection { get; set; }
    public CancellationTokenSource? CancellationTokenSource { get; set; }
    public KustoClient? CurrentKustoClient { get; set; }

    public string DisplayTitle
    {
        get
        {
            if (Connection != null && !string.IsNullOrEmpty(Connection.Database))
            {
                var clusterName = Connection.GetClusterNameFromUrl();
                return $"{Connection.Database} @ {clusterName}";
            }
            return State.Title;
        }
    }

    public QueryTab(
        TabState state,
        IUserSettingsManager? userSettingsManager,
        SyntaxHighlighter syntaxHighlighter,
        LanguageService languageService,
        HtmlSyntaxHighlighter htmlSyntaxHighlighter)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));

        // Each tab gets its own autocomplete generator so they don't share state
        var autocompleteSuggestionGenerator = new AutocompleteSuggestionGenerator(languageService);

        EditorPane = new QueryEditorPane(userSettingsManager, syntaxHighlighter, autocompleteSuggestionGenerator)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            SchemeName = "Base"
        };

        ResultsPane = new ResultsPane(htmlSyntaxHighlighter)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            SchemeName = "Base"
        };
    }

    /// <summary>
    /// Captures the current UI state into the TabState for persistence.
    /// </summary>
    public void SyncStateFromUI()
    {
        State.QueryText = EditorPane.GetCurrentQuery();
        if (Connection != null)
        {
            State.ConnectionId = Connection.Id;
            State.ClusterUri = Connection.ClusterUri;
            State.Database = Connection.Database;
        }
    }

    /// <summary>
    /// Restores connection label from state (used when restoring persisted tabs).
    /// </summary>
    public void RestoreConnectionLabel()
    {
        if (Connection != null)
        {
            EditorPane.SetConnection(Connection);
        }
    }

    /// <summary>
    /// Restores the query text from the persisted TabState into the editor.
    /// </summary>
    public void RestoreQueryText()
    {
        if (!string.IsNullOrEmpty(State.QueryText))
        {
            EditorPane.SetQueryText(State.QueryText);
        }
    }

    public void Dispose()
    {
        if (CancellationTokenSource != null)
        {
            CancellationTokenSource.Cancel();
            CancellationTokenSource.Dispose();
            CancellationTokenSource = null;
        }
        if (CurrentKustoClient != null)
        {
            CurrentKustoClient.Dispose();
            CurrentKustoClient = null;
        }
        EditorPane.Dispose();
        ResultsPane.Dispose();
    }
}
