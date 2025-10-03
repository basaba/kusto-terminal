using KustoTerminal.Language.Models;
using KustoTerminal.Language.Services;
using KustoTerminal.UI.SyntaxHighlighting;
using Terminal.Gui.Views;

namespace KustoTerminal.UI.AutoCompletion;

public class AutoCompleter
{
    private readonly LanguageService _languageService;

    public AutoCompleter(LanguageService languageService)
    {
        _languageService = languageService;
    }

    public IReadOnlyList<string> GetAutoCompleteItems(TextView textView, string clusterName, string databaseName)
    {
        var position = GetPosition(textView);
        var textModel = new TextModel(textView);
        var items = _languageService.GetCompletions(textModel, position, clusterName, databaseName);
        return items.Items;
    }

    private int GetPosition(TextView textView)
    {
        var position = 0;
        var currentRow = textView.CurrentRow;
        var currentColumn = textView.CurrentColumn;
        if (currentRow > 0)
        {
            for (int i = 0; i < currentRow - 1; i++)
            {
                var line = textView.GetLine(i);
                position += line.Count;
            }
        }

        position += currentColumn;

        return position;
    }
}