using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using KustoTerminal.Core.Models;
using KustoTerminal.UI.Common;
using KustoTerminal.UI.Dialogs;
using KustoTerminal.UI.Services;
using KustoTerminal.UI.SyntaxHighlighting;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace KustoTerminal.UI.Panes;

public class ResultsPane : BasePane
{
    private TableView _tableView = null!;
    private Label _statusLabel = null!;
    private TextView _errorLabel = null!;
    private Label _shortcutsLabel = null!;
    private Label[] _shortcutLabels = null!;
    private TextField _searchField = null!;
    private Label _searchLabel = null!;
    private QueryResult? _currentResult;
    private DataTable? _originalData;
    private HashSet<string> _selectedColumns = new();
    private bool _searchVisible = false;
    private string? _currentQueryText;
    private KustoConnection? _currentConnection;
    private HtmlSyntaxHighlighter _htmlSyntaxHighlighter;

    public event EventHandler? MaximizeToggleRequested;

    public ResultsPane(HtmlSyntaxHighlighter htmlSyntaxHtmlSyntaxHighlighter)
    {
        _htmlSyntaxHighlighter = htmlSyntaxHtmlSyntaxHighlighter;

        InitializeComponents();
        SetupLayout();
        SetKeyboard();

        CanFocus = true;
        TabStop = TabBehavior.TabStop;
    }

    private void InitializeComponents()
    {
        _statusLabel = new Label
        {
            Text = "No results",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1
        };

        _errorLabel = new TextView
        {
            Text = "",
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill()! - 1,
            Visible = false,
            SchemeName = "Error",
            ReadOnly = true,
            WordWrap = true
        };

        _tableView = new TableView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill()! - 1,
            FullRowSelect = false,
            MultiSelect = false
        };

        _searchLabel = new Label
        {
            Text = "Search:",
            X = 0,
            Y = Pos.Bottom(_tableView),
            Width = 8,
            Height = 1,
            Visible = false
        };

        _searchField = new TextField
        {
            X = 0,
            Y = Pos.Bottom(_tableView) - 1,
            Width = Dim.Fill(),
            Height = 1,
            Visible = false
        };

        _searchField.TextChanged += OnSearchTextChanged;

        _shortcutsLabel = new Label
        {
            X = 0,
            Y = Pos.Bottom(_tableView),
            Width = 0,
            Height = 1,
            SchemeName = "TopLevel"
        };

        _shortcutLabels = BuildShortcutsLabels(_shortcutsLabel).ToArray();
    }

    private static List<Label> BuildShortcutsLabels(Label labelToAppendTo)
    {
        var labels = new List<Label>();
        var last = labelToAppendTo;
        var normalScheme = Constants.ShortcutDescriptionSchemeName;
        var shortcutKeyScheme = Constants.ShortcutKeySchemeName;
        last = last.AppendLabel("/: ", shortcutKeyScheme, labels);
        last = last.AppendLabel("Filter ", normalScheme, labels);
        last = last.AppendLabel("| ", normalScheme, labels);
        last = last.AppendLabel("Ctrl+L: ", shortcutKeyScheme, labels);
        last = last.AppendLabel("Select Columns ", normalScheme, labels);
        last = last.AppendLabel("| ", normalScheme, labels);
        last = last.AppendLabel("Ctrl+S: ", shortcutKeyScheme, labels);
        last = last.AppendLabel("Share ", normalScheme, labels);
        last = last.AppendLabel("| ", normalScheme, labels);
        last = last.AppendLabel("Ctrl+R: ", shortcutKeyScheme, labels);
        last = last.AppendLabel("Row Select ", normalScheme, labels);
        last = last.AppendLabel("| ", normalScheme, labels);
        last = last.AppendLabel("Ctrl+J: ", shortcutKeyScheme, labels);
        last = last.AppendLabel("JSON Viewer ", normalScheme, labels);
        last = last.AppendLabel("| ", normalScheme, labels);
        last = last.AppendLabel("Ctrl+E: ", shortcutKeyScheme, labels);
        last = last.AppendLabel("Export ", normalScheme, labels);
        last = last.AppendLabel("| ", normalScheme, labels);
        last = last.AppendLabel("F12: ", shortcutKeyScheme, labels);
        last = last.AppendLabel("Maximize/Restore ", normalScheme, labels);
        return labels;
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
            else if (key.KeyCode == (KeyCode.CtrlMask | Key.E.KeyCode))
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
            else if (key.KeyCode == (KeyCode.CtrlMask | Key.S.KeyCode))
            {
                OnShareClicked();
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
            else if (key == Key.Enter)
            {
                _tableView.SetFocus();
            }
        };

        _tableView.KeyDown += (sender, key) =>
        {
            if (key == (KeyCode.CtrlMask | Key.C.KeyCode))
            {
                OnCopyCellClicked();
                key.Handled = true;
            }
            else if (key == (KeyCode.CtrlMask | Key.R.KeyCode))
            {
                SwitchToRowMode();
                key.Handled = true;
            }
            else if (key == Key.F11)
            {
                MaximizeToggleRequested?.Invoke(this, EventArgs.Empty);
                key.Handled = true;
            }
            else if (key == Key.Esc)
            {
                SwitchToCellMode();
                key.Handled = true;
            }
            else if (key == Key.Enter)
            {
                OnViewCellClicked();
                key.Handled = true;
            }
            else if (key.KeyCode == (KeyCode.CtrlMask | Key.Enter.KeyCode))
            {
                OnViewJsonClicked();
                key.Handled = true;
            }
            else if (key.KeyCode == (KeyCode.CtrlMask | Key.L.KeyCode))
            {
                OnColumnSelectorClicked();
                key.Handled = true;
            }
        };
    }

    private void SetupLayout()
    {
        Add(_statusLabel, _errorLabel, _tableView, _searchLabel, _searchField, _shortcutsLabel);
        Add(_shortcutLabels);
    }

    public void SetQueryText(string? queryText)
    {
        _currentQueryText = queryText;
    }

    public void SetConnection(KustoConnection? connection)
    {
        _currentConnection = connection;
    }

    public void DisplayResult(QueryResult result)
    {
        Clear();
        _currentResult = result;
        _tableView.SelectedColumn = 0;
        _tableView.SelectedRow = 0;

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
            _errorLabel.Visible = false;
            _tableView.Visible = true;
            _statusLabel.Visible = true;

            var dataTable = result.Data!;
            _originalData = dataTable.Copy();

            if (_selectedColumns.Count == 0)
            {
                foreach (DataColumn column in dataTable.Columns)
                {
                    _selectedColumns.Add(column.ColumnName);
                }
            }

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
            _tableView.Visible = false;
            _errorLabel.Visible = true;
            _errorLabel.Text = $"Error displaying results: {ex.Message}";
            _statusLabel.Text = $"Error occurred while displaying results | Duration: {result.Duration.TotalMilliseconds:F0}ms";
        }
    }

    private void DisplayError(QueryResult result)
    {
        _tableView.Visible = false;
        _errorLabel.Visible = true;
        _errorLabel.Text = $"Query failed: {result.ErrorMessage}";

        var statusText = "Error occurred during query execution";
        if (!string.IsNullOrEmpty(result.ClientRequestId))
        {
            statusText += $" | ClientRequestId: {result.ClientRequestId}";
        }

        _statusLabel.Text = statusText;
    }

    private void DisplayEmpty(QueryResult result)
    {
        _errorLabel.Visible = false;
        _tableView.Visible = true;
        _statusLabel.Visible = true;

        var statusText = $"No results returned | Duration: {result.Duration.TotalMilliseconds:F0}ms";
        if (!string.IsNullOrEmpty(result.ClientRequestId))
        {
            statusText += $" | ClientRequestId: {result.ClientRequestId}";
        }

        _statusLabel.Text = statusText;

        if (result.Data != null && result.Data.Columns.Count > 0)
        {
            _tableView.Table = new DataTableSource(result.Data);
        }
        else
        {
            _tableView.Table = new DataTableSource(new DataTable());
        }
    }

    public void Clear()
    {
        _currentResult = null;
        _originalData = null;
        _selectedColumns.Clear();
        _tableView.Table = new DataTableSource(new DataTable());

        _errorLabel.Visible = false;
        _tableView.Visible = true;
        _statusLabel.Visible = true;
        _statusLabel.Text = "No results";

        HideSearch();
    }

    private void OnExportClicked()
    {
        if (_currentResult?.Data == null)
        {
            return;
        }

        var dialog = new SaveDialog
        {
            Title = "Export Results",
            AllowedTypes = new List<IAllowedType>
            {
                new AllowedType("csv", ".csv"),
                new AllowedType("json", ".json"),
                new AllowedType("tsv", ".tsv")
            },
            OpenMode = OpenMode.File
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
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

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
        using var writer = new StreamWriter(fileName);

        var headers = dataTable.Columns.Cast<DataColumn>().Select(column => $"\"{column.ColumnName}\"");
        writer.WriteLine(string.Join(",", headers));

        foreach (DataRow row in dataTable.Rows)
        {
            var fields = row.ItemArray.Select(field => $"\"{field?.ToString()?.Replace("\"", "\"\"")}\"");
            writer.WriteLine(string.Join(",", fields));
        }
    }

    private void ExportToTsv(DataTable dataTable, string fileName)
    {
        using var writer = new StreamWriter(fileName);

        var headers = dataTable.Columns.Cast<DataColumn>().Select(column => column.ColumnName);
        writer.WriteLine(string.Join("\t", headers));

        foreach (DataRow row in dataTable.Rows)
        {
            var fields = row.ItemArray.Select(field => field?.ToString()?.Replace("\t", " "));
            writer.WriteLine(string.Join("\t", fields));
        }
    }

    private void ExportToJson(DataTable dataTable, string fileName)
    {
        var rows = new List<object>();

        foreach (DataRow row in dataTable.Rows)
        {
            var rowObject = new Dictionary<string, object?>();

            for (var i = 0; i < dataTable.Columns.Count; i++)
            {
                rowObject[dataTable.Columns[i].ColumnName] = row[i] == DBNull.Value ? null : row[i];
            }

            rows.Add(rowObject);
        }

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(rows, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(fileName, json);
    }

    private void OnCopyCellClicked()
    {
        if (_currentResult?.Data == null || _tableView.Table == null)
        {
            return;
        }

        var selectedRow = _tableView.SelectedRow;
        var selectedCol = _tableView.SelectedColumn;

        var cellValue = _tableView.Table[selectedRow, selectedCol]?.ToString() ?? "";

        try
        {
            Clipboard.Contents = cellValue;
        }
        catch
        {
        }
    }

    private void OnShareClicked()
    {
        var hasQuery = !string.IsNullOrWhiteSpace(_currentQueryText);
        var hasResult = _originalData != null && _originalData.Rows.Count > 0;

        if (!hasQuery && !hasResult)
        {
            return;
        }

        var dialog = new ShareDialog(hasQuery, hasResult);
        Application.Run(dialog);

        if (dialog.WasCanceled)
        {
            return;
        }

        try
        {
            if (!dialog.CopyQuery && !dialog.CopyResult)
            {
                return;
            }

            DataTable? dataTable = null;
            string? queryToCopy = null;

            if (dialog.CopyResult)
            {
                dataTable = ApplyColumnFilter(_originalData!);
            }

            if (dialog.CopyQuery)
            {
                queryToCopy = _currentQueryText;
            }

            var htmlContent = _htmlSyntaxHighlighter.GenerateHtmlWithQuery(queryToCopy!, dataTable!, _currentConnection!);
            ClipboardService.SetClipboardWithHtml(htmlContent);
        }
        catch
        {
        }
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

        if (IsValidJson(cellValue))
        {
            ShowJsonTreeViewDialog(columnName, cellValue);
        }
        else
        {
            MessageBox.Query("Not JSON", "The selected cell does not contain valid JSON data.", "OK");
        }
    }

    private void ShowCellDetailDialog(string columnName, string cellValue)
    {
        var dialog = new Dialog
        {
            Title = $"Cell Content: {columnName}",
            Height = 20,
            Width = Dim.Percent(80),
            Modal = true,
            Arrangement = ViewArrangement.Resizable
        };
        var textView = new TextView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(2),
            Text = cellValue,
            ReadOnly = true,
            WordWrap = true,
            SchemeName = "Base"
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
        {
            return false;
        }

        value = value.Trim();

        if (!(value.StartsWith("{") && value.EndsWith("}")) &&
            !(value.StartsWith("[") && value.EndsWith("]")))
        {
            return false;
        }

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
        {
            return;
        }

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
            var filteredByColumns = ApplyColumnFilter(_originalData);
            _tableView.Table = new DataTableSource(filteredByColumns);
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
        {
            return;
        }

        var searchText = _searchField.Text.ToString();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            var filteredByColumns = ApplyColumnFilter(_originalData);
            _tableView.Table = new DataTableSource(filteredByColumns);
            UpdateStatusForOriginalData();
            return;
        }

        try
        {
            var filteredTable = _originalData.Clone();

            foreach (DataRow row in _originalData.Rows)
            {
                var rowMatches = false;

                foreach (DataColumn column in _originalData.Columns)
                {
                    if (!_selectedColumns.Contains(column.ColumnName))
                    {
                        continue;
                    }

                    var cellValue = row[column]?.ToString() ?? "";
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

            var finalTable = ApplyColumnFilter(filteredTable);
            _tableView.Table = new DataTableSource(finalTable);
            UpdateStatusForFilteredData(finalTable.Rows.Count, searchText);
        }
        catch (Exception ex)
        {
            _tableView.Visible = false;
            _errorLabel.Visible = true;
            _errorLabel.Text = $"Search error: {ex.Message}";
            _statusLabel.Text = "Error occurred during search";
        }
    }

    private void UpdateStatusForOriginalData()
    {
        if (_currentResult == null)
        {
            return;
        }

        var statusText = $"Rows: {_currentResult.RowCount:N0} | Columns: {_currentResult.ColumnCount} | Duration: {_currentResult.Duration.TotalMilliseconds:F0}ms";
        if (!string.IsNullOrEmpty(_currentResult.ClientRequestId))
        {
            statusText += $" | ClientRequestId: {_currentResult.ClientRequestId}";
        }

        _statusLabel.Text = statusText;
    }

    private void UpdateStatusForFilteredData(int filteredRowCount, string searchText)
    {
        if (_currentResult == null)
        {
            return;
        }

        var statusText = $"Filtered: {filteredRowCount:N0} of {_currentResult.RowCount:N0} rows | Search: \"{searchText}\" | Columns: {_currentResult.ColumnCount} | Duration: {_currentResult.Duration.TotalMilliseconds:F0}ms";
        if (!string.IsNullOrEmpty(_currentResult.ClientRequestId))
        {
            statusText += $" | ClientRequestId: {_currentResult.ClientRequestId}";
        }

        _statusLabel.Text = statusText;
    }

    private void OnColumnSelectorClicked()
    {
        if (_originalData == null)
        {
            return;
        }

        var dialog = new ColumnSelectorDialog(_originalData, _selectedColumns);
        Application.Run(dialog);

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
            return sourceTable;
        }

        var filteredTable = new DataTable();

        foreach (DataColumn column in sourceTable.Columns)
        {
            if (_selectedColumns.Contains(column.ColumnName))
            {
                filteredTable.Columns.Add(column.ColumnName, column.DataType);
            }
        }

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
