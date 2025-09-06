using System;
using System.Data;
using System.Linq;
using Terminal.Gui;
using KustoTerminal.Core.Models;

namespace KustoTerminal.UI.Panes
{
    public class ResultsPane : BasePane
    {
        private TableView _tableView;
        private Label _statusLabel;
        private Button _exportButton;
        private ScrollView _scrollView;
        
        private QueryResult? _currentResult;

        public ResultsPane()
        {
            InitializeComponents();
            SetupLayout();
            SetupElementFocusHandlers();
        }

        private void InitializeComponents()
        {
            _statusLabel = new Label("No results")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = 1
            };

            _tableView = new TableView()
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill() - 2,
                FullRowSelect = true,
                MultiSelect = false
            };

            _exportButton = new Button("Export")
            {
                X = 0,
                Y = Pos.Bottom(_tableView),
                Width = 10,
                Enabled = false
            };

            // Event handlers
            _exportButton.Clicked += OnExportClicked;
        }

        private void SetupLayout()
        {
            Add(_statusLabel, _tableView, _exportButton);
        }

        private void SetupElementFocusHandlers()
        {
            // Set up focus handlers for individual elements
            _tableView.Enter += OnElementFocusEnter;
            _tableView.Leave += OnElementFocusLeave;
            _exportButton.Enter += OnElementFocusEnter;
            _exportButton.Leave += OnElementFocusLeave;
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
            HighlightActiveElements();
        }

        protected override void OnFocusLeave()
        {
            // Remove highlighting when leaving the pane
            RemoveElementHighlighting();
        }

        private void HighlightActiveElements()
        {
            // Enhanced highlighting for the table view when pane is active
            var highlightedTableScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
            };
            
            _tableView.ColorScheme = highlightedTableScheme;
            
            // Also highlight the status label
            var highlightedStatusScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                HotNormal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
            };
            
            _statusLabel.ColorScheme = highlightedStatusScheme;
            
            // Force redraw
            _tableView.SetNeedsDisplay();
            _statusLabel.SetNeedsDisplay();
        }

        private void RemoveElementHighlighting()
        {
            // Reset to normal color schemes
            var normalTableScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
            };
            
            _tableView.ColorScheme = normalTableScheme;
            
            var normalStatusScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.White, Color.Black),
                HotNormal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.White, Color.Black),
                Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
            };
            
            _statusLabel.ColorScheme = normalStatusScheme;
            
            // Force redraw
            _tableView.SetNeedsDisplay();
            _statusLabel.SetNeedsDisplay();
        }

        public void DisplayResult(QueryResult result)
        {
            _currentResult = result;

            if (!result.IsSuccess)
            {
                DisplayError(result);
                return;
            }

            if (result.Data == null || result.Data.Rows.Count == 0)
            {
                DisplayEmpty(result);
                return;
            }

            DisplayData(result);
        }

        private void DisplayData(QueryResult result)
        {
            try
            {
                var dataTable = result.Data!;
                _tableView.Table = dataTable;

                _statusLabel.Text = $"Rows: {result.RowCount:N0} | Columns: {result.ColumnCount} | Duration: {result.Duration.TotalMilliseconds:F0}ms";
                _exportButton.Enabled = true;
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error displaying results: {ex.Message}";
                _exportButton.Enabled = false;
            }
        }

        private void DisplayError(QueryResult result)
        {
            _statusLabel.Text = $"Query failed: {result.ErrorMessage} | Duration: {result.Duration.TotalMilliseconds:F0}ms";
            
            // Clear the table
            _tableView.Table = new DataTable();
            _exportButton.Enabled = false;
        }

        private void DisplayEmpty(QueryResult result)
        {
            _statusLabel.Text = $"No results returned | Duration: {result.Duration.TotalMilliseconds:F0}ms";
            
            // Show empty table with column headers if available
            if (result.Data != null && result.Data.Columns.Count > 0)
            {
                _tableView.Table = result.Data;
            }
            else
            {
                _tableView.Table = new DataTable();
            }
            
            _exportButton.Enabled = false;
        }

        public new void Clear()
        {
            _currentResult = null;
            _tableView.Table = new DataTable();
            _statusLabel.Text = "No results";
            _exportButton.Enabled = false;
        }

        private void OnExportClicked()
        {
            if (_currentResult?.Data == null) return;

            var dialog = new SaveDialog("Export Results", "Save results to file")
            {
                NameFieldLabel = "File name:",
                AllowedFileTypes = new[] { ".csv", ".json", ".tsv" }
            };

            Application.Run(dialog);

            if (!dialog.Canceled && !dialog.FileName.IsEmpty)
            {
                try
                {
                    ExportData(_currentResult.Data, dialog.FileName.ToString());
                    MessageBox.Query("Export", "Results exported successfully!", "OK");
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery("Export Error", $"Failed to export results: {ex.Message}", "OK");
                }
            }
        }

        private void ExportData(DataTable dataTable, string fileName)
        {
            var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            
            switch (extension)
            {
                case ".csv":
                    ExportToCsv(dataTable, fileName);
                    break;
                case ".json":
                    ExportToJson(dataTable, fileName);
                    break;
                case ".tsv":
                    ExportToTsv(dataTable, fileName);
                    break;
                default:
                    throw new ArgumentException($"Unsupported file format: {extension}");
            }
        }

        private void ExportToCsv(DataTable dataTable, string fileName)
        {
            using var writer = new System.IO.StreamWriter(fileName);
            
            // Write headers
            var headers = dataTable.Columns.Cast<DataColumn>().Select(column => $"\"{column.ColumnName}\"");
            writer.WriteLine(string.Join(",", headers));
            
            // Write rows
            foreach (DataRow row in dataTable.Rows)
            {
                var fields = row.ItemArray.Select(field => $"\"{field?.ToString()?.Replace("\"", "\"\"")}\"");
                writer.WriteLine(string.Join(",", fields));
            }
        }

        private void ExportToTsv(DataTable dataTable, string fileName)
        {
            using var writer = new System.IO.StreamWriter(fileName);
            
            // Write headers
            var headers = dataTable.Columns.Cast<DataColumn>().Select(column => column.ColumnName);
            writer.WriteLine(string.Join("\t", headers));
            
            // Write rows
            foreach (DataRow row in dataTable.Rows)
            {
                var fields = row.ItemArray.Select(field => field?.ToString()?.Replace("\t", " "));
                writer.WriteLine(string.Join("\t", fields));
            }
        }

        private void ExportToJson(DataTable dataTable, string fileName)
        {
            var rows = new System.Collections.Generic.List<object>();
            
            foreach (DataRow row in dataTable.Rows)
            {
                var rowObject = new System.Collections.Generic.Dictionary<string, object?>();
                
                for (int i = 0; i < dataTable.Columns.Count; i++)
                {
                    rowObject[dataTable.Columns[i].ColumnName] = row[i] == DBNull.Value ? null : row[i];
                }
                
                rows.Add(rowObject);
            }
            
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(rows, Newtonsoft.Json.Formatting.Indented);
            System.IO.File.WriteAllText(fileName, json);
        }
    }
}