using System;
using Terminal.Gui;
using KustoTerminal.Core.Models;

namespace KustoTerminal.UI.Panes
{
    public class QueryEditorPane : BasePane
    {
        private TextView _queryTextView;
        private Label _connectionLabel;
        private Button _executeButton;
        private Button _clearButton;
        
        private KustoConnection? _currentConnection;

        public event EventHandler<string>? QueryExecuteRequested;

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
                Height = Dim.Fill() - 3,
                Text = "-- Enter your KQL query here\n"
            };

            _executeButton = new Button("Execute (F5)")
            {
                X = 0,
                Y = Pos.Bottom(_queryTextView),
                Width = 15
            };

            _clearButton = new Button("Clear")
            {
                X = Pos.Right(_executeButton) + 1,
                Y = Pos.Bottom(_queryTextView),
                Width = 8
            };

            // Event handlers
            _executeButton.Clicked += OnExecuteClicked;
            _clearButton.Clicked += OnClearClicked;
            
            // Set up key bindings for the TextView
            _queryTextView.KeyPress += OnKeyPress;
        }

        private void SetupElementFocusHandlers()
        {
            // Set up focus handlers for individual elements
            _queryTextView.Enter += OnElementFocusEnter;
            _queryTextView.Leave += OnElementFocusLeave;
            _executeButton.Enter += OnElementFocusEnter;
            _executeButton.Leave += OnElementFocusLeave;
            _clearButton.Enter += OnElementFocusEnter;
            _clearButton.Leave += OnElementFocusLeave;
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
            // Additional highlighting when the pane itself receives focus
            HighlightActiveElement();
        }

        protected override void OnFocusLeave()
        {
            // Remove highlighting when leaving the pane
            RemoveElementHighlighting();
        }

        private void HighlightActiveElement()
        {
            // Add a subtle border or highlighting to the text view when pane is active
            var highlightedTextViewScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                HotNormal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
            };
            
            _queryTextView.ColorScheme = highlightedTextViewScheme;
            _queryTextView.SetNeedsDisplay();
        }

        private void RemoveElementHighlighting()
        {
            // Reset to normal color scheme
            var normalTextViewScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.Black, Color.White),
                HotNormal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.White),
                Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
            };
            
            _queryTextView.ColorScheme = normalTextViewScheme;
            _queryTextView.SetNeedsDisplay();
        }

        private void SetupLayout()
        {
            Add(_connectionLabel, _queryTextView, _executeButton, _clearButton);
            
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
            // Handle Ctrl+A for select all
            else if (e.KeyEvent.Key == (Key.CtrlMask | Key.A))
            {
                _queryTextView.SelectAll();
                e.Handled = true;
            }
        }

        private void OnExecuteClicked()
        {
            var query = GetCurrentQuery();
            if (!string.IsNullOrWhiteSpace(query))
            {
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
            _executeButton.Enabled = true;
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
    }
}