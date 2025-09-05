using System;
using Terminal.Gui;
using KustoTerminal.Core.Models;

namespace KustoTerminal.UI.Panes
{
    public class QueryEditorPane : View
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
        }

        private void InitializeComponents()
        {
            _queryTextView = new TextView()
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill() - 2,
                WordWrap = false,
                AllowsTab = true,
                Text = "// Enter your KQL query here\n// Press F5 to execute\n\n"
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
    }
}