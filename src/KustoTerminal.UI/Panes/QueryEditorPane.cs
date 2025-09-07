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

            _shortcutsLabel = new Label("F5: Execute | Ctrl+L: Clear | Ctrl+A: Select All")
            {
                X = 0,
                Y = Pos.Bottom(_queryTextView),
                Width = Dim.Fill(),
                Height = 1,
                ColorScheme = new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                    HotNormal = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                    HotFocus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black)
                }
            };

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
            // Set up focus handlers for individual elements
            _queryTextView.Enter += OnElementFocusEnter;
            _queryTextView.Leave += OnElementFocusLeave;
        }

        private void OnElementFocusEnter(FocusEventArgs args)
        {
            // When any element in this pane gets focus, highlight the entire pane
            SetHighlighted(true);
        }

        private void OnElementFocusLeave(FocusEventArgs args)
        {
            // Check if focus is moving to another element within this pane
            Application.MainLoop.Invoke(() =>
            {
                var focusedView = Application.Top.MostFocused;
                bool stillInPane = IsChildOf(focusedView, this);
                
                if (!stillInPane)
                {
                    SetHighlighted(false);
                }
            });
        }

        private bool IsChildOf(View? child, View parent)
        {
            if (child == null) return false;
            if (child == parent) return true;
            
            foreach (View subview in parent.Subviews)
            {
                if (IsChildOf(child, subview))
                    return true;
            }
            return false;
        }

        protected override void OnFocusEnter()
        {
            // Use BasePane's color scheme system
            base.OnFocusEnter();
        }

        protected override void OnFocusLeave()
        {
            // Use BasePane's color scheme system
            base.OnFocusLeave();
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

        public void SetQuery(string query)
        {
            _queryTextView.Text = query;
        }

        public void AppendQuery(string query)
        {
            var currentText = _queryTextView.Text?.ToString() ?? "";
            _queryTextView.Text = currentText + query;
        }

        public void InsertTemplate(string template)
        {
            var cursor = _queryTextView.CursorPosition;
            var currentText = _queryTextView.Text?.ToString() ?? "";
            
            var newText = currentText.Insert(cursor.Y * currentText.Length + cursor.X, template);
            _queryTextView.Text = newText;
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