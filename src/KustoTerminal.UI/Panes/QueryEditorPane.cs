using System;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;

using KustoTerminal.Core.Models;

namespace KustoTerminal.UI.Panes
{
    public class QueryEditorPane : BasePane
    {
        private TextView _queryTextView;
        private Label _connectionLabel;
        private Label _progressLabel;
        private Label _shortcutsLabel;
        private Label _temporaryMessageLabel;
        
        private KustoConnection? _currentConnection;
        private bool _isExecuting = false;
        private System.Threading.Timer? _temporaryMessageTimer;

        public event EventHandler<string>? QueryExecuteRequested;
        public event EventHandler? EscapePressed;
        public event EventHandler? QueryCancelRequested;

        public QueryEditorPane()
        {
            InitializeComponents();
            SetupLayout();
            SetupElementFocusHandlers();
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
                Height = Dim.Fill() - 4,
                Text = ""
            };
            
            // Configure selection highlighting using BasePane method
             ApplyColorSchemeToControl(_queryTextView, "textview_normal");

            _shortcutsLabel = new Label()
            {
                Text = "F5: Execute | Esc: Cancel/Switch | Ctrl+L: Clear | Ctrl+A: Select All",
                X = 0,
                Y = Pos.Bottom(_queryTextView),
                Width = Dim.Fill(),
                Height = 1
            };
            
            // Apply shortcut label color scheme using BasePane method
            ApplyColorSchemeToControl(_shortcutsLabel, "shortcut");

            _progressLabel = new Label()
            {
                X = 0,
                Y = Pos.Bottom(_shortcutsLabel),
                Width = Dim.Fill(),
                Height = 1,
                Visible = false
            };

            _temporaryMessageLabel = new Label()
            {
                X = 0,
                Y = Pos.Bottom(_progressLabel),
                Width = Dim.Fill(),
                Height = 1,
                Visible = false
            };
            
            // Apply color scheme for temporary message
            ApplyColorSchemeToControl(_temporaryMessageLabel, "warning");

            // Set up key bindings for the TextView
            //_queryTextView.KeyBindings = new KeyBindings
             // += OnKeyPress;
        }

        private void SetupElementFocusHandlers()
        {
            // Use BasePane's common focus handling for all controls
            SetupCommonElementFocusHandlers(_queryTextView);
        }

        protected override void ApplyHighlighting()
        {
            // // Apply highlighting to all controls, with special handling for TextView
            // foreach (View child in Subviews)
            // {
            //     if (child is TextView)
            //     {
            //         // Apply appropriate TextView color scheme based on highlight state
            //         var colorSchemeType = IsHighlighted ? "textview_highlighted" : "textview_normal";
            //         ApplyColorSchemeToControl(child, colorSchemeType);
            //     }
            //     else
            //     {
            //         ApplyHighlightingToControl(child);
            //     }
            // }

            // SetNeedsDisplay();
        }

        private void SetupLayout()
        {
            Add(_connectionLabel, _queryTextView, _shortcutsLabel, _progressLabel, _temporaryMessageLabel);
            
            // Focus on the text view
            _queryTextView.SetFocus();
        }

        // private void OnKeyPress(KeyEventEventArgs e)
        // {
        //     // Handle F5 for query execution
        //     if (e.KeyEvent.Key == Key.F5)
        //     {
        //         OnExecuteClicked();
        //         e.Handled = true;
        //     }
        //     // Handle Ctrl+L for clear
        //     else if (e.KeyEvent.Key == (Key.CtrlMask | Key.L))
        //     {
        //         OnClearClicked();
        //         e.Handled = true;
        //     }
        //     // Handle Ctrl+A for select all
        //     else if (e.KeyEvent.Key == (Key.CtrlMask | Key.A))
        //     {
        //         _queryTextView.SelectAll();
        //         e.Handled = true;
        //     }
        //     // Handle ESC - cancel query if executing, otherwise switch focus to results pane
        //     else if (e.KeyEvent.Key == Key.Esc)
        //     {
        //         if (_isExecuting)
        //         {
        //             QueryCancelRequested?.Invoke(this, EventArgs.Empty);
        //         }
        //         else
        //         {
        //             EscapePressed?.Invoke(this, EventArgs.Empty);
        //         }
        //         e.Handled = true;
        //     }
        // }

        private void OnExecuteClicked()
        {
            if (_isExecuting)
            {
                ShowTemporaryMessage("Query already running. Please wait for it to complete.", 3000);
                return;
            }

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
                // _progressLabel.SetNeedsDisplay();
            }
        }

        public void ShowTemporaryMessage(string message, int durationMs = 2000)
        {
            // Cancel any existing timer
            _temporaryMessageTimer?.Dispose();
            
            // Show the message
            _temporaryMessageLabel.Text = message;
            _temporaryMessageLabel.Visible = true;
            //_temporaryMessageLabel.SetNeedsDisplay();
            
            // Set up timer to hide the message after the specified duration
            _temporaryMessageTimer = new System.Threading.Timer(HideTemporaryMessage, null, durationMs, System.Threading.Timeout.Infinite);
        }

        private void HideTemporaryMessage(object? state)
        {
            Application.Invoke(() =>
            {
                _temporaryMessageLabel.Visible = false;
                //_temporaryMessageLabel.SetNeedsDisplay();
                _temporaryMessageTimer?.Dispose();
                _temporaryMessageTimer = null;
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _temporaryMessageTimer?.Dispose();
                _spinnerTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}