using System;
using Terminal.Gui;
using KustoTerminal.Core.Models;

namespace KustoTerminal.UI.Panes
{
    public class QueryEditorPane : BasePane
    {
        private TextView _queryTextView;
        private Label _connectionLabel;
        private Label _progressLabel;
        private Label _shortcutsLabel;
        
        private KustoConnection? _currentConnection;
        private bool _isExecuting = false;

        public event EventHandler<string>? QueryExecuteRequested;
        public event EventHandler? EscapePressed;

        public QueryEditorPane()
        {
            InitializeComponents();
            SetupLayout();
            SetupElementFocusHandlers();
        }

        private void InitializeComponents()
        {
            _connectionLabel = new Label("No connection")
            {
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
                Height = Dim.Fill() - 4,
                Text = ""
            };
            
            // Configure selection highlighting using BasePane method
             ApplyColorSchemeToControl(_queryTextView, "textview_normal");

            _shortcutsLabel = new Label("F5: Execute | Ctrl+L: Clear | Ctrl+A: Select All | Shift+Arrow: Select Text")
            {
                X = 0,
                Y = Pos.Bottom(_queryTextView),
                Width = Dim.Fill(),
                Height = 1
            };
            
            // Apply shortcut label color scheme using BasePane method
            ApplyColorSchemeToControl(_shortcutsLabel, "shortcut");

            _progressLabel = new Label("")
            {
                X = 0,
                Y = Pos.Bottom(_shortcutsLabel),
                Width = Dim.Fill(),
                Height = 1,
                Visible = false
            };
            
            // Set up key bindings for the TextView
             _queryTextView.KeyPress += OnKeyPress;
        }

        private void SetupElementFocusHandlers()
        {
            // Use BasePane's common focus handling for all controls
            SetupCommonElementFocusHandlers(_queryTextView);
        }

        protected override void ApplyHighlighting()
        {
            // Apply highlighting to all controls, with special handling for TextView
            foreach (View child in Subviews)
            {
                if (child is TextView)
                {
                    // Apply appropriate TextView color scheme based on highlight state
                    var colorSchemeType = IsHighlighted ? "textview_highlighted" : "textview_normal";
                    ApplyColorSchemeToControl(child, colorSchemeType);
                }
                else
                {
                    ApplyHighlightingToControl(child);
                }
            }

            SetNeedsDisplay();
        }

        private void SetupLayout()
        {
            Add(_connectionLabel, _queryTextView, _shortcutsLabel, _progressLabel);
            
            // Focus on the text view
            _queryTextView.SetFocus();
        }

        private void OnKeyPress(KeyEventEventArgs e)
        {
            // Handle F5 for query execution
            if (e.KeyEvent.Key == Key.F5)
            {
                OnExecuteClicked();
                e.Handled = true;
            }
            // Handle Ctrl+L for clear
            else if (e.KeyEvent.Key == (Key.CtrlMask | Key.L))
            {
                OnClearClicked();
                e.Handled = true;
            }
            // Handle Ctrl+A for select all
            else if (e.KeyEvent.Key == (Key.CtrlMask | Key.A))
            {
                _queryTextView.SelectAll();
                e.Handled = true;
            }
            // Handle ESC to switch focus to results pane
            else if (e.KeyEvent.Key == Key.Esc)
            {
                EscapePressed?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        }

        private void OnExecuteClicked()
        {
            if (_isExecuting)
                return;

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

        private void OnClearClicked()
        {
            _queryTextView.Text = "";
            _queryTextView.SetFocus();
        }

        public string GetCurrentQuery()
        {
            return _queryTextView.Text?.ToString() ?? "";
        }

        public void SetConnection(KustoConnection connection)
        {
            _currentConnection = connection;
            _connectionLabel.Text = $"Connected: {connection.DisplayName} | {connection.Database}";
        }

        public void FocusEditor()
        {
            _queryTextView.SetFocus();
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
            Application.MainLoop.Invoke(() =>
            {
                if (_isExecuting && _progressLabel.Visible)
                {
                    _spinnerIndex = (_spinnerIndex + 1) % _spinnerFrames.Length;
                    var currentMessage = _progressLabel.Text?.ToString() ?? "Running query...";
                    // Extract the message part (everything after the spinner character and space)
                    var messagePart = currentMessage.Length > 2 ? currentMessage.Substring(2) : "Running query...";
                    _progressLabel.Text = $"{_spinnerFrames[_spinnerIndex]} {messagePart}";
                    _progressLabel.SetNeedsDisplay();
                }
            });
        }

        public void UpdateProgressMessage(string message)
        {
            if (_isExecuting && _progressLabel.Visible)
            {
                _progressLabel.Text = $"{_spinnerFrames[_spinnerIndex]} {message}";
                _progressLabel.SetNeedsDisplay();
            }
        }
    }
}