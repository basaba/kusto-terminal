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
            
            // Configure selection highlighting
            SetupSelectionHighlighting();

            _shortcutsLabel = new Label("F5: Execute | Ctrl+L: Clear | Ctrl+A: Select All | Shift+Arrow: Select Text")
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

        private void SetupSelectionHighlighting()
        {
            // Configure TextView with enhanced selection highlighting
            if (_queryTextView != null)
            {
                // Create a custom color scheme for better text selection visibility
                var selectionColorScheme = new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow), // Selected text
                    HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),  // Selected text when focused
                    Disabled = new Terminal.Gui.Attribute(Color.Gray, Color.Black)
                };
                
                _queryTextView.ColorScheme = selectionColorScheme;
                
                // Add mouse support for better text selection
                _queryTextView.WantMousePositionReports = true;
                _queryTextView.WantContinuousButtonPressed = true;
                
                // Note: MouseEvent is handled differently in Terminal.Gui
                // We'll use a different approach to maintain selection highlighting
            }
        }


        private void ApplySelectionColorScheme()
        {
            if (_queryTextView != null)
            {
                var selectionColorScheme = new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow), // Selected text
                    HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),  // Selected text when focused
                    Disabled = new Terminal.Gui.Attribute(Color.Gray, Color.Black)
                };
                
                _queryTextView.ColorScheme = selectionColorScheme;
            }
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
            
            // Ensure selection highlighting is maintained
            ApplySelectionColorScheme();
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
                
                // Always maintain selection highlighting regardless of focus
                ApplySelectionColorScheme();
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
            
            // Ensure selection highlighting is maintained when pane gets focus
            ApplySelectionColorScheme();
        }

        protected override void OnFocusLeave()
        {
            // Use BasePane's color scheme system
            base.OnFocusLeave();
            
            // Maintain selection highlighting even when pane loses focus
            ApplySelectionColorScheme();
        }

        protected override void ApplyHighlighting()
        {
            // Override the base highlighting to preserve TextView selection colors
            foreach (View child in Subviews)
            {
                if (child is TextView textView)
                {
                    // Don't let BasePane override TextView color scheme
                    ApplySelectionColorScheme();
                }
                else
                {
                    ApplyHighlightingToControl(child);
                }
            }

            SetNeedsDisplay();
        }

        protected override ColorScheme GetHighlightedSchemeForControl(View control)
        {
            // Override for TextView to maintain selection highlighting when pane is highlighted
            if (control is TextView)
            {
                return new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                    HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow), // Selected text
                    HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),  // Selected text when focused
                    Disabled = new Terminal.Gui.Attribute(Color.Gray, Color.Black)
                };
            }
            
            return base.GetHighlightedSchemeForControl(control);
        }

        protected override ColorScheme GetNormalSchemeForControl(View control)
        {
            // Override for TextView to maintain selection highlighting when pane is not highlighted
            if (control is TextView)
            {
                return new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    Focus = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow), // Selected text
                    HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),  // Selected text when focused
                    Disabled = new Terminal.Gui.Attribute(Color.Gray, Color.Black)
                };
            }
            
            return base.GetNormalSchemeForControl(control);
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