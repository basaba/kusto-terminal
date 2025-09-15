using System;
using System.Data;
using System.Linq;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;

using KustoTerminal.Core.Models;
using Terminal.Gui.Input;
using Terminal.Gui.Drivers;

namespace KustoTerminal.UI.Panes
{
    public class ResultsPane : BasePane
    {
        private TableView _tableView;
        private Label _statusLabel;
        private Label _shortcutsLabel;
        private TextField _searchField;
        private Label _searchLabel;
        
        private QueryResult? _currentResult;
        private DataTable? _originalData;
        private bool _searchVisible = false;

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
                Text = "/: Filter | Ctrl+S: Export | Ctrl+T: Row Select",
                X = 0,
                Y = Pos.Bottom(_tableView),
                Width = Dim.Fill(),
                Height = 1
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
                } else if (key == (KeyCode.CtrlMask | Key.T.KeyCode))
                {
                    SwitchToRowMode();
                    key.Handled = true;
                } else if (key == Key.Esc)
                {
                    SwitchToCellMode();
                    key.Handled = true;
                } else if (key == Key.Enter)
                {
                    OnViewCellClicked();
                    key.Handled = true;
                }
            };
        }

        private void SetupLayout()
        {
            Add(_statusLabel, _tableView, _searchLabel, _searchField, _shortcutsLabel);
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
                _originalData = dataTable.Copy(); // Keep a copy of the original data
                _tableView.Table = new DataTableSource(dataTable);

                _statusLabel.Text = $"Rows: {result.RowCount:N0} | Columns: {result.ColumnCount} | Duration: {result.Duration.TotalMilliseconds:F0}ms";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error displaying results: {ex.Message}";
            }
        }

        private void DisplayError(QueryResult result)
        {
            _statusLabel.Text = $"Query failed: {result.ErrorMessage} | Duration: {result.Duration.TotalMilliseconds:F0}ms";
            
            // Clear the table
            _tableView.Table = new DataTableSource(new DataTable());
        }

        private void DisplayEmpty(QueryResult result)
        {
            _statusLabel.Text = $"No results returned | Duration: {result.Duration.TotalMilliseconds:F0}ms";
            
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
            _tableView.Table = new DataTableSource(new DataTable());
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
                MessageBox.ErrorQuery("Copy Error", "No data available to copy.", "OK");
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
                MessageBox.ErrorQuery("View Error", "No data available to view.", "OK");
                return;
            }

            var selectedRow = _tableView.SelectedRow;
            var selectedCol = _tableView.SelectedColumn;

            var cellValue = _tableView.Table[selectedRow, selectedCol]?.ToString() ?? "";
            var columnName = _tableView.Table.ColumnNames.ElementAt(selectedCol);

            ShowCellDetailDialog(columnName, cellValue);
        }

        private void ShowCellDetailDialog(string columnName, string cellValue)
        {
            var dialog = new Dialog()
            {
                Title = $"Cell Content: {columnName}",
                Height = 20,
                Width = 80,
                Modal = true
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

        private void ToggleSearch()
        {
            if (_searchVisible)
            {
                // If search is already visible, just focus on the search field
                // and preserve the current search text
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
            
            
            // Update shortcuts position
            // _shortcutsLabel.Y = Pos.Bottom(_searchField);
            
            _searchField.SetFocus();
            // SetNeedsDisplay();
        }

        private void HideSearch()
        {
            _searchVisible = false;
            _searchLabel.Visible = false;
            _searchField.Visible = false;
            _searchField.Text = "";
            
            // Restore shortcuts position
            // _shortcutsLabel.Y = Pos.Bottom(_tableView);
            
            // Restore original data if we have it
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
                _statusLabel.Text = $"Search error: {ex.Message}";
            }
        }

        private void UpdateStatusForOriginalData()
        {
            if (_currentResult != null)
            {
                _statusLabel.Text = $"Rows: {_currentResult.RowCount:N0} | Columns: {_currentResult.ColumnCount} | Duration: {_currentResult.Duration.TotalMilliseconds:F0}ms";
            }
        }

        private void UpdateStatusForFilteredData(int filteredRowCount, string searchText)
        {
            if (_currentResult != null)
            {
                _statusLabel.Text = $"Filtered: {filteredRowCount:N0} of {_currentResult.RowCount:N0} rows | Search: \"{searchText}\" | Columns: {_currentResult.ColumnCount} | Duration: {_currentResult.Duration.TotalMilliseconds:F0}ms";
            }
        }
    }
}