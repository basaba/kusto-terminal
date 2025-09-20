using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Drawing;
using KustoTerminal.Core.Models;
using KustoTerminal.UI.Dialogs;
using Terminal.Gui.Input;
using Terminal.Gui.Drivers;

namespace KustoTerminal.UI.Panes
{
    public class ResultsPane : BasePane
    {
        private TableView _tableView;
        private Label _statusLabel;
        private TextView _errorLabel;
        private Label _shortcutsLabel;
        private TextField _searchField;
        private Label _searchLabel;
        
        private QueryResult? _currentResult;
        private DataTable? _originalData;
        private HashSet<string> _selectedColumns = new HashSet<string>();
        private bool _searchVisible = false;
        public event EventHandler? MaximizeToggleRequested;

        public ResultsPane()
        {
            InitializeComponents();
            SetupLayout();
            SetKeyboard();
            
            CanFocus = true;
            TabStop = TabBehavior.TabStop;
        }

        private void InitializeComponents()
        {
            _statusLabel = new Label()
            {
                Text = "No results",
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = 1
            };

            _errorLabel = new TextView()
            {
                Text = "",
                X = 0,
                Y = 1,  // Position where table view is
                Width = Dim.Fill(),
                Height = Dim.Fill() - 1,  // Take up the space normally used by table view
                Visible = false,
                SchemeName = "Error",
                ReadOnly = true,
            };

            _tableView = new TableView()
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill() - 1,
                FullRowSelect = false,
                MultiSelect = false,
                MaxCellWidth = 50,
            };

            _shortcutsLabel = new Label()
            {
                Text = "/: Filter | Ctrl+L: Columns | Ctrl+R: Row Select | Ctrl+J: JSON Viewer | Ctrl+S: Export | F12: Maximize/Restore",
                X = 0,
                Y = Pos.Bottom(_tableView),
                Width = Dim.Fill(),
                Height = 1,
                SchemeName = "TopLevel"
            };
            
            _searchLabel = new Label()
            {
                Text = "Search:",
                X = 0,
                Y = Pos.Bottom(_tableView),
                Width = 8,
                Height = 1,
                Visible = false
            };

            _searchField = new TextField()
            {
                X = 0,
                Y = Pos.Bottom(_tableView) - 1,
                Width = Dim.Fill(),
                Height = 1,
                Visible = false
            };

            _searchField.TextChanged += OnSearchTextChanged;
        }

        private void SetKeyboard()
        {
            KeyDown += (sender, key) =>
            {
                if (Key.TryParse("/", out var k) && key == k)
                {
                    ToggleSearch();
                    key.Handled = true;
                }
                else if (key.KeyCode == (KeyCode.CtrlMask | Key.S.KeyCode))
                {
                    OnExportClicked();
                    key.Handled = true;
                }
                else if (key == Key.F12)
                {
                    MaximizeToggleRequested?.Invoke(this, EventArgs.Empty);
                    key.Handled = true;
                }
                else if (key.KeyCode == (KeyCode.CtrlMask | Key.L.KeyCode))
                {
                    OnColumnSelectorClicked();
                    key.Handled = true;
                }
            };

            _searchField.KeyDown += (sender, key) =>
            {
                if (key == Key.Esc)
                {
                    HideSearch();
                    key.Handled = true;
                }
            };

            _tableView.KeyDown += (sender, key) =>
            {
                if (key == (KeyCode.CtrlMask | Key.C.KeyCode))
                {
                    OnCopyCellClicked();
                    key.Handled = true;
                } else if (key == (KeyCode.CtrlMask | Key.R.KeyCode))
                {
                    SwitchToRowMode();
                    key.Handled = true;
                } else if (key == Key.F11)
                {
                    MaximizeToggleRequested?.Invoke(this, EventArgs.Empty);
                    key.Handled = true;
                } else if (key == Key.Esc)
                {
                    SwitchToCellMode();
                    key.Handled = true;
                } else if (key == Key.Enter)
                {
                    OnViewCellClicked();
                    key.Handled = true;
                } else if (key.KeyCode == (KeyCode.CtrlMask | Key.Enter.KeyCode))
                {
                    OnViewJsonClicked();
                    key.Handled = true;
                } else if (key.KeyCode == (KeyCode.CtrlMask | Key.L.KeyCode))
                {
                    OnColumnSelectorClicked();
                    key.Handled = true;
                }
            };
        }

        private void SetupLayout()
        {
            Add(_statusLabel, _errorLabel, _tableView, _searchLabel, _searchField, _shortcutsLabel);
        }

        public void DisplayResult(QueryResult result)
        {
            Clear();
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
                // Hide error label and show table view
                _errorLabel.Visible = false;
                _tableView.Visible = true;
                _statusLabel.Visible = true;
                
                var dataTable = result.Data!;
                _originalData = dataTable.Copy(); // Keep a copy of the original data
                
                // Initialize selected columns if not set
                if (_selectedColumns.Count == 0)
                {
                    foreach (DataColumn column in dataTable.Columns)
                    {
                        _selectedColumns.Add(column.ColumnName);
                    }
                }
                
                // Apply column filtering
                var filteredTable = ApplyColumnFilter(dataTable);
                _tableView.Table = new DataTableSource(filteredTable);

                var statusText = $"Rows: {result.RowCount:N0} | Columns: {result.ColumnCount} | Duration: {result.Duration.TotalMilliseconds:F0}ms";
                if (!string.IsNullOrEmpty(result.ClientRequestId))
                {
                    statusText += $" | ClientRequestId: {result.ClientRequestId}";
                }
                _statusLabel.Text = statusText;
            }
            catch (Exception ex)
            {
                // Hide table view and show error label for display errors
                _tableView.Visible = false;
                _errorLabel.Visible = true;
                _errorLabel.Text = $"Error displaying results: {ex.Message}";
                _statusLabel.Text = $"Error occurred while displaying results | Duration: {result.Duration.TotalMilliseconds:F0}ms";
            }
        }

        private void DisplayError(QueryResult result)
        {
            // Hide table view and show error label in its place
            _tableView.Visible = false;
            _errorLabel.Visible = true;
            _errorLabel.Text = $"Query failed: {result.ErrorMessage}";
            
            // Keep status label visible for other information
            var statusText = "Error occurred during query execution";
            if (!string.IsNullOrEmpty(result.ClientRequestId))
            {
                statusText += $" | ClientRequestId: {result.ClientRequestId}";
            }
            _statusLabel.Text = statusText;
        }

        private void DisplayEmpty(QueryResult result)
        {
            // Hide error label and show table view
            _errorLabel.Visible = false;
            _tableView.Visible = true;
            _statusLabel.Visible = true;
            
            var statusText = $"No results returned | Duration: {result.Duration.TotalMilliseconds:F0}ms";
            if (!string.IsNullOrEmpty(result.ClientRequestId))
            {
                statusText += $" | ClientRequestId: {result.ClientRequestId}";
            }
            _statusLabel.Text = statusText;
            
            // Show empty table with column headers if available
            if (result.Data != null && result.Data.Columns.Count > 0)
            {
                _tableView.Table = new DataTableSource(result.Data);
            }
            else
            {
                _tableView.Table = new DataTableSource(new DataTable());
            }
        }

        public new void Clear()
        {
            _currentResult = null;
            _originalData = null;
            _selectedColumns.Clear();
            _tableView.Table = new DataTableSource(new DataTable());
            
            // Reset to showing table view and status label, hide error label
            _errorLabel.Visible = false;
            _tableView.Visible = true;
            _statusLabel.Visible = true;
            _statusLabel.Text = "No results";
            
            HideSearch();
        }

        private void OnExportClicked()
        {
            if (_currentResult?.Data == null) return;

            var dialog = new SaveDialog()
            {
                Title = "Export Results",
                AllowedTypes = new List<IAllowedType>()
                {
                    new AllowedType("csv", ".csv"),
                    new AllowedType("json", ".json"),
                    new AllowedType("tsv", ".tsv"),
                },
                OpenMode = OpenMode.File,
            };

            dialog.Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "results.csv");

            Application.Run(dialog);

            if (!dialog.Canceled && !string.IsNullOrEmpty(dialog.FileName))
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

        private void OnCopyCellClicked()
        {
            if (_currentResult?.Data == null || _tableView.Table == null)
            {
                return;
            }

            var selectedRow = _tableView.SelectedRow;
            var selectedCol = _tableView.SelectedColumn;

            var cellValue = _tableView.Table[selectedRow,selectedCol]?.ToString() ?? "";

            try
            {
                Clipboard.Contents = cellValue;
            }
            catch { }
        }

        private void SwitchToRowMode()
        {
            _tableView.FullRowSelect = true;
            _tableView.SetNeedsLayout();
        }

        private void SwitchToCellMode()
        {
            _tableView.FullRowSelect = false;
            _tableView.SetNeedsLayout();
        }

        private void OnViewCellClicked()
        {
            if (_currentResult?.Data == null || _tableView.Table == null)
            {
                return;
            }

            var selectedRow = _tableView.SelectedRow;
            var selectedCol = _tableView.SelectedColumn;

            var cellValue = _tableView.Table[selectedRow, selectedCol]?.ToString() ?? "";
            var columnName = _tableView.Table.ColumnNames.ElementAt(selectedCol);

            ShowCellDetailDialog(columnName, cellValue);
        }

        private void OnViewJsonClicked()
        {
            if (_currentResult?.Data == null || _tableView.Table == null)
            {
                return;
            }

            var selectedRow = _tableView.SelectedRow;
            var selectedCol = _tableView.SelectedColumn;

            var cellValue = _tableView.Table[selectedRow, selectedCol]?.ToString() ?? "";
            var columnName = _tableView.Table.ColumnNames.ElementAt(selectedCol);

            // Check if content is JSON
            if (IsValidJson(cellValue))
            {
                ShowJsonTreeViewDialog(columnName, cellValue);
            }
            else
            {
                // Show a brief message that the cell doesn't contain JSON
                MessageBox.Query("Not JSON", "The selected cell does not contain valid JSON data.", "OK");
            }
        }

        private void ShowCellDetailDialog(string columnName, string cellValue)
        {
            // Default text view dialog
            var dialog = new Dialog()
            {
                Title = $"Cell Content: {columnName}",
                Height = 20,
                Width = 80,
                Modal = true,
            };
            var textView = new TextView()
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill(1),
                Height = Dim.Fill(2),
                Text = cellValue,
                ReadOnly = true,
                WordWrap = true
            };

            dialog.Add(textView);
            textView.SetFocus();

            Application.Run(dialog);
        }

        private void ShowJsonTreeViewDialog(string columnName, string jsonContent)
        {
            var dialog = new JsonTreeViewDialog(columnName, jsonContent);
            Application.Run(dialog);
        }

        private static bool IsValidJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();
            
            // Quick check for JSON-like structure
            if (!(value.StartsWith("{") && value.EndsWith("}")) &&
                !(value.StartsWith("[") && value.EndsWith("]")))
                return false;

            try
            {
                System.Text.Json.JsonDocument.Parse(value);
                return true;
            }
            catch (System.Text.Json.JsonException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ToggleSearch()
        {
            if (_searchVisible)
            {
                _searchField.SetFocus();
            }
            else
            {
                ShowSearch();
            }
        }

        private void ShowSearch()
        {
            if (_currentResult?.Data == null || _originalData == null)
                return;

            _searchVisible = true;
            _searchLabel.Visible = true;
            _searchField.Visible = true;
            _searchField.SetFocus();
        }

        private void HideSearch()
        {
            _searchVisible = false;
            _searchLabel.Visible = false;
            _searchField.Visible = false;
            _searchField.Text = "";
            if (_originalData != null)
            {
                _tableView.Table = new DataTableSource(_originalData);
                UpdateStatusForOriginalData();
            }
            
            _tableView.SetFocus();
        }

        private void OnSearchTextChanged(object? sender, EventArgs e)
        {
            PerformSearch();
        }

        private void PerformSearch()
        {
            if (_originalData == null || !_searchVisible)
                return;

            var searchText = _searchField.Text.ToString();
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Show all data when search is empty
                _tableView.Table = new DataTableSource(_originalData);
                UpdateStatusForOriginalData();
                return;
            }

            try
            {
                // Create a filtered view of the data
                var filteredTable = _originalData.Clone();
                
                foreach (DataRow row in _originalData.Rows)
                {
                    bool rowMatches = false;
                    
                    // Check each column for the search text (case-insensitive)
                    foreach (var item in row.ItemArray)
                    {
                        var cellValue = item?.ToString() ?? "";
                        if (cellValue.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            rowMatches = true;
                            break;
                        }
                    }
                    
                    if (rowMatches)
                    {
                        filteredTable.ImportRow(row);
                    }
                }
                
                _tableView.Table = new DataTableSource(filteredTable);
                UpdateStatusForFilteredData(filteredTable.Rows.Count, searchText);
            }
            catch (Exception ex)
            {
                // Hide table view and show error label for search errors
                _tableView.Visible = false;
                _errorLabel.Visible = true;
                _errorLabel.Text = $"Search error: {ex.Message}";
                _statusLabel.Text = "Error occurred during search";
            }
        }

        private void UpdateStatusForOriginalData()
        {
            if (_currentResult != null)
            {
                var statusText = $"Rows: {_currentResult.RowCount:N0} | Columns: {_currentResult.ColumnCount} | Duration: {_currentResult.Duration.TotalMilliseconds:F0}ms";
                if (!string.IsNullOrEmpty(_currentResult.ClientRequestId))
                {
                    statusText += $" | ClientRequestId: {_currentResult.ClientRequestId}";
                }
                _statusLabel.Text = statusText;
            }
        }

        private void UpdateStatusForFilteredData(int filteredRowCount, string searchText)
        {
            if (_currentResult != null)
            {
                var statusText = $"Filtered: {filteredRowCount:N0} of {_currentResult.RowCount:N0} rows | Search: \"{searchText}\" | Columns: {_currentResult.ColumnCount} | Duration: {_currentResult.Duration.TotalMilliseconds:F0}ms";
                if (!string.IsNullOrEmpty(_currentResult.ClientRequestId))
                {
                    statusText += $" | ClientRequestId: {_currentResult.ClientRequestId}";
                }
                _statusLabel.Text = statusText;
            }
        }

        private void OnColumnSelectorClicked()
        {
            if (_originalData == null) return;

            var dialog = new ColumnSelectorDialog(_originalData, _selectedColumns);
            Application.Run(dialog);

            // Update selected columns and refresh display
            _selectedColumns = dialog.SelectedColumns;
            
            if (_currentResult != null)
            {
                DisplayData(_currentResult);
            }
        }

        private DataTable ApplyColumnFilter(DataTable sourceTable)
        {
            if (_selectedColumns.Count == 0 || _selectedColumns.Count == sourceTable.Columns.Count)
            {
                // If no columns selected or all columns selected, return the original table
                return sourceTable;
            }

            // Create a new table with only selected columns
            var filteredTable = new DataTable();
            
            // Add selected columns to the new table
            foreach (DataColumn column in sourceTable.Columns)
            {
                if (_selectedColumns.Contains(column.ColumnName))
                {
                    filteredTable.Columns.Add(column.ColumnName, column.DataType);
                }
            }

            // Copy rows with only selected columns
            foreach (DataRow sourceRow in sourceTable.Rows)
            {
                var newRow = filteredTable.NewRow();
                foreach (DataColumn column in filteredTable.Columns)
                {
                    newRow[column.ColumnName] = sourceRow[column.ColumnName];
                }
                filteredTable.Rows.Add(newRow);
            }

            return filteredTable;
        }
    }
}