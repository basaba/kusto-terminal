using System;
using System.Drawing;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;

using KustoTerminal.Core.Models;
using KustoTerminal.Core.Interfaces;
using Terminal.Gui.Input;
using Terminal.Gui.Drivers;
using KustoTerminal.UI.SyntaxHighlighting;
using KustoTerminal.UI.AutoCompletion;
using KustoTerminal.UI.Common;

namespace KustoTerminal.UI.Panes
{
    public class QueryEditorPane : BasePane
    {
        private TextView _queryTextView = null!;
        private Label _connectionLabel = null!;
        private Label _progressLabel = null!;
        private Label _shortcutsLabel = null!;
        private Label[] _shortcutLabels = null!;
        private Label _temporaryMessageLabel = null!;
        
        private bool _isExecuting = false;
        private System.Threading.Timer? _temporaryMessageTimer;
        private readonly IUserSettingsManager? _userSettingsManager;
        private readonly SyntaxHighlighter _syntaxHighlighter = null!;
        private readonly AutocompleteSuggestionGenerator _autocompleteSuggestionGenerator = null!;

        private KustoConnection _currentConnection = null!;

        public event EventHandler<string>? QueryExecuteRequested;
        public event EventHandler? QueryCancelRequested;
        public event EventHandler? MaximizeToggleRequested;

        public QueryEditorPane(IUserSettingsManager? userSettingsManager = null, SyntaxHighlighter syntaxHighlighter = null!, AutocompleteSuggestionGenerator autocompleteSuggestionGenerator = null!)
        {
            _userSettingsManager = userSettingsManager;
            _syntaxHighlighter = syntaxHighlighter!;
            _autocompleteSuggestionGenerator = autocompleteSuggestionGenerator!;
            InitializeComponents();
            SetupLayout();
            SetKeyboard();
            CanFocus = true;
            SetupAutocomplete();
        }
        
        private void InitializeComponents()
        {
            _connectionLabel = new Label()
            {
                Text = "No connection",
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = 1
            };

            _queryTextView = new TextView()
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill()! - 1,
                Text = "",
            };
            
            _progressLabel = new Label()
            {
                X = 0,
                Y = Pos.Bottom(_queryTextView) - 1,
                Width = Dim.Fill(),
                Height = 1,
                Visible = false
            };

            _temporaryMessageLabel = new Label()
            {
                X = 0,
                Y = Pos.Bottom(_progressLabel) - 1,
                Width = Dim.Fill(),
                Height = 1,
                Visible = false
            };
            
            _shortcutsLabel = new Label()
            {
                X = 0,
                Y = Pos.Bottom(_queryTextView),
                Width = 0,
                Height = 1,
            };

            _shortcutLabels = BuildShortcutsLabels(_shortcutsLabel).ToArray();
        }
        
        private static List<Label> BuildShortcutsLabels(Label labelToAppendTo)
        {
            var labels = new List<Label>();
            var last = labelToAppendTo;
            var normalScheme = Constants.ShortcutDescriptionSchemeName;
            var shortcutKeyScheme = Constants.ShortcutKeySchemeName;
            last = last.AppendLabel("F5: ", shortcutKeyScheme, labels);
            last = last.AppendLabel("Execute query ", normalScheme, labels);
            last = last.AppendLabel("| ", normalScheme, labels);
            last = last.AppendLabel( "F12: ", shortcutKeyScheme, labels);
            last = last.AppendLabel("Maximize/Restore ", normalScheme, labels);
            return labels;
        }

        private void SetKeyboard()
        {
            _queryTextView.KeyBindings.ReplaceCommands(KeyCode.CtrlMask | Key.V.KeyCode, Command.Paste);
            _queryTextView.KeyBindings.ReplaceCommands(KeyCode.CtrlMask | Key.C.KeyCode, Command.Copy);

            _queryTextView.KeyDown += (sender, key) =>
            {
                if (key == Key.F5)
                {
                    OnExecuteClicked();
                }
                else if (key == Key.F12)
                {
                    MaximizeToggleRequested?.Invoke(this, EventArgs.Empty);
                    key.Handled = true;
                }
                else if (key == Key.Esc)
                {
                    if (_isExecuting)
                    {
                        QueryCancelRequested?.Invoke(this, EventArgs.Empty);
                    }
                        
                    key.Handled = true;
                }
            };
        }

        private void SetupLayout()
        {
            Add(_connectionLabel, _queryTextView, _shortcutsLabel, _progressLabel, _temporaryMessageLabel);
            Add(_shortcutLabels);
        }
        
        private void SetupAutocomplete()
        {
            _queryTextView.Autocomplete.MaxWidth = 30;
            _queryTextView.Autocomplete.MaxHeight = 10;
            _autocompleteSuggestionGenerator.SetQueryTextView(_queryTextView);
            _queryTextView.Autocomplete.SuggestionGenerator = _autocompleteSuggestionGenerator;
            
            _queryTextView.DrawingText += (e, a) =>
            {
                _syntaxHighlighter.Highlight(_queryTextView, _currentConnection);
            };
            
            _queryTextView.DrawingContent += (sender, args) =>
            {
                OnQueryTextViewDrawing();
            };
        }

        private void OnQueryTextViewDrawing()
        {
            if (!_queryTextView.Autocomplete.Visible || _queryTextView.SelectedLength > 0)
            {
                return;
            }
            
            // A workaround for bug in Terminal.Gui where the popup is displayed only once.
            var cursorPosition = _queryTextView.CursorPosition;
            var renderPosition = new Point(cursorPosition.X, cursorPosition.Y + 1 - _queryTextView.TopRow);
            _queryTextView.Autocomplete.RenderOverlay(renderPosition);
        }

        private void OnExecuteClicked()
        {
            var query = GetCurrentQuery();
            if (!string.IsNullOrWhiteSpace(query))
            {
                SetExecuting(true);
                QueryExecuteRequested?.Invoke(this, query);
            }
            else
            {
                MessageBox.ErrorQuery("Error", "Please enter a query to execute.", "OK");
            }
        }

        public string GetCurrentQuery()
        {
            var fullText = _queryTextView.Text?.ToString() ?? "";
            if (string.IsNullOrEmpty(fullText))
                return "";

            // Get cursor position (Point with X=column, Y=row)
            var cursorPos = _queryTextView.CursorPosition;
            
            // Split text into lines
            var lines = fullText.Split('\n');
            
            // The cursor position Y gives us the line index directly
            int currentLineIndex = Math.Min(cursorPos.Y, lines.Length - 1);
            
            // Find the query block that contains the current line
            // A query block is separated by empty lines
            int queryStartLine = currentLineIndex;
            int queryEndLine = currentLineIndex;
            
            // Find start of query block (go backwards until empty line or start)
            while (queryStartLine > 0 && !string.IsNullOrWhiteSpace(lines[queryStartLine - 1]))
            {
                queryStartLine--;
            }
            
            // Find end of query block (go forwards until empty line or end)
            while (queryEndLine < lines.Length - 1 && !string.IsNullOrWhiteSpace(lines[queryEndLine + 1]))
            {
                queryEndLine++;
            }
            
            // Extract the query block
            var queryLines = new string[queryEndLine - queryStartLine + 1];
            Array.Copy(lines, queryStartLine, queryLines, 0, queryLines.Length);
            
            var query = string.Join("\n", queryLines).Trim();
            
            // If no query block found at cursor position, fallback to entire text
            return string.IsNullOrWhiteSpace(query) ? fullText : query;
        }

        public void SetConnection(KustoConnection connection)
        {
            _connectionLabel.Text = $"Connected: {connection.DisplayName} | {connection.Database}";
            _currentConnection = connection;
            _autocompleteSuggestionGenerator.SetClusterContext(connection);
        }

        public void FocusEditor()
        {
            _queryTextView.SetFocus();
        }

        public (string text, System.Drawing.Point cursor) GetEditorState()
        {
            var text = _queryTextView.Text?.ToString() ?? string.Empty;
            var cursor = _queryTextView.CursorPosition;
            return (text, cursor);
        }

        public void SetEditorState(string text, System.Drawing.Point cursor)
        {
            _queryTextView.Text = text;
            _queryTextView.CursorPosition = cursor;
        }

        public void SetExecuting(bool isExecuting)
        {
            _isExecuting = isExecuting;
            
            if (isExecuting)
            {
                _progressLabel.Text = "⠋ Running query...";
                _progressLabel.Visible = true;
                StartProgressSpinner();
            }
            else
            {
                _progressLabel.Visible = false;
                StopProgressSpinner();
            }
        }

        private System.Threading.Timer? _spinnerTimer;
        private readonly string[] _spinnerFrames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        private int _spinnerIndex = 0;

        private void StartProgressSpinner()
        {
            _spinnerTimer = new System.Threading.Timer(UpdateSpinner, null, 0, 100);
        }

        private void StopProgressSpinner()
        {
            _spinnerTimer?.Dispose();
            _spinnerTimer = null;
        }

        private void UpdateSpinner(object? state)
        {
            Application.Invoke(() =>
            {
                if (_isExecuting && _progressLabel.Visible)
                {
                    _spinnerIndex = (_spinnerIndex + 1) % _spinnerFrames.Length;
                    var currentMessage = _progressLabel.Text?.ToString() ?? "Running query...";
                    // Extract the message part (everything after the spinner character and space)
                    var messagePart = currentMessage.Length > 2 ? currentMessage.Substring(2) : "Running query...";
                    _progressLabel.Text = $"{_spinnerFrames[_spinnerIndex]} {messagePart}";
                    //_progressLabel.SetNeedsDisplay();
                }
            });
        }

        public void UpdateProgressMessage(string message)
        {
            if (_isExecuting && _progressLabel.Visible)
            {
                _progressLabel.Text = $"{_spinnerFrames[_spinnerIndex]} {message}";
             }
        }

        public void ShowTemporaryMessage(string message, int durationMs = 2000)
        {
            // Cancel any existing timer
            _temporaryMessageTimer?.Dispose();
            
            // Show the message
            _temporaryMessageLabel.Text = message;
            _temporaryMessageLabel.Visible = true;
            
            // Set up timer to hide the message after the specified duration
            _temporaryMessageTimer = new System.Threading.Timer(HideTemporaryMessage, null, durationMs, System.Threading.Timeout.Infinite);
        }

        private void HideTemporaryMessage(object? state)
        {
            Application.Invoke(() =>
            {
                _temporaryMessageLabel.Visible = false;
                _temporaryMessageTimer?.Dispose();
                _temporaryMessageTimer = null;
            });
        }

        public async void LoadLastQueryAsync()
        {
            if (_userSettingsManager != null)
            {
                try
                {
                    var lastQuery = await _userSettingsManager.GetLastQueryAsync();
                    if (!string.IsNullOrEmpty(lastQuery))
                    {
                        Application.Invoke(() =>
                        {
                            _queryTextView.Text = lastQuery;
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Silently fail - don't crash the app if settings can't be loaded
                    Console.WriteLine($"Warning: Failed to load last query: {ex.Message}");
                }
            }
        }

        public async void SaveCurrentQueryAsync()
        {
            if (_userSettingsManager != null)
            {
                try
                {
                    var currentQuery = _queryTextView.Text?.ToString() ?? string.Empty;
                    await _userSettingsManager.SaveLastQueryAsync(currentQuery);
                }
                catch (Exception ex)
                {
                    // Silently fail - don't crash the app if settings can't be saved
                    Console.WriteLine($"Warning: Failed to save query: {ex.Message}");
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Save query before disposing
                SaveCurrentQueryAsync();
                _temporaryMessageTimer?.Dispose();
                _spinnerTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}