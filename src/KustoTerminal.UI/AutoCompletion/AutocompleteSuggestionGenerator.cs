using System.Text;
using KustoTerminal.Core.Models;
using KustoTerminal.Language.Services;
using KustoTerminal.UI.SyntaxHighlighting;
using Terminal.Gui.Views;

namespace KustoTerminal.UI.AutoCompletion;

public class AutocompleteSuggestionGenerator : ISuggestionGenerator
{
    private static IEnumerable<Suggestion> s_emptyResult = new Suggestion[0];
    private LanguageService  _languageService;
    private TextView _queryTextView = null!;
    private KustoConnection _currentConnection = null!;
    private int _allowedRow;
    private int _allowedColumn;

    public AutocompleteSuggestionGenerator(LanguageService languageService)
    {
        _languageService = languageService;
    }

    public void SetQueryTextView(TextView textView)
    {
        _queryTextView = textView;
        _queryTextView.ContentsChanged += (sender, args) =>
        {
            _allowedRow = args.Row;
            _allowedColumn = args.Col;
        };
    }
    
    public IEnumerable<Suggestion> GenerateSuggestions(AutocompleteContext context)
    {
        if (_currentConnection == null)
        {
            return s_emptyResult;
        }
        
        var clusterName = _currentConnection.GetClusterNameFromUrl();
        var databaseName = _currentConnection.Database;
        
        var line = context.CurrentLine.Select (c => c.Rune).ToList ();
        var currentWord = IdxToWord (line, context.CursorPosition, out int startIdx);
        var position = GetPosition(_queryTextView);
        var textModel = new TextModel(_queryTextView);
        
        var isEmptyBlock = _languageService.IsEmptyBlock(textModel, position, clusterName, databaseName);
        // To prevent intrusive autocomplete popup in case user is just changing the cursor position,
        // we allow displaying popup only when new character is typed, this is achieved by storing the last changed 
        // character position.
        if (_queryTextView.CurrentColumn != _allowedColumn
            || _queryTextView.CurrentRow != _allowedRow
            || isEmptyBlock)
        {
            return s_emptyResult;
        }

        var items = _languageService.GetCompletions(textModel, position, clusterName, databaseName);
        return items.Items
            .Where(item => 
                item.DisplayText.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(currentWord, item.DisplayText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.OrderText)
            .Select(item => new Suggestion(currentWord.Length, item.ApplyText, item.DisplayText));
    }

    public void SetClusterContext(KustoConnection connection)
    {
        _currentConnection = connection;
    }
    
    private int GetPosition(TextView textView)
    {
        var position = 0;
        var currentRow = textView.CurrentRow;
        var currentColumn = textView.CurrentColumn;
        for (int i = 0; i < currentRow; i++)
        {
            var line = textView.GetLine(i);
            position += line.Count + 1;
        }

        position += currentColumn;

        return position;
    }

    public bool IsWordChar(Rune rune)
    {
        var ch = (char)rune.Value;
        return char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '\'';
    }
    
    private string IdxToWord (List<Rune> line, int idx, out int startIdx, int columnOffset = 0)
    {
        var sb = new StringBuilder ();
        startIdx = idx;

        // get the ending word index
        while (startIdx < line.Count)
        {
            if (IsWordChar (line [startIdx]))
            {
                startIdx++;
            }
            else
            {
                break;
            }
        }

        // It isn't a word char then there is no way to autocomplete that word
        if (startIdx == idx && columnOffset != 0)
        {
            return null!;
        }

        // we are at the end of a word. Work out what has been typed so far
        while (startIdx-- > 0)
        {
            if (IsWordChar (line [startIdx]))
            {
                sb.Insert (0, (char)line [startIdx].Value);
            }
            else
            {
                break;
            }
        }

        startIdx = Math.Max (startIdx, 0);

        return sb.ToString ();
    }
}