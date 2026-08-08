using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace KustoTerminal.UI.Dialogs;

public class ColumnSelectorDialog : Dialog
{
    private ListView _columnsList = null!;
    private Label _shortcutsLabel = null!;
    private Label _instructionLabel = null!;
    private readonly DataTable _dataTable;
    private readonly HashSet<string> _selectedColumns;

    public HashSet<string> SelectedColumns => _selectedColumns;

    public ColumnSelectorDialog(DataTable dataTable, HashSet<string>? currentSelection = null)
    {
        _dataTable = dataTable ?? throw new ArgumentNullException(nameof(dataTable));
        _selectedColumns = currentSelection != null ? new HashSet<string>(currentSelection) : new HashSet<string>();

        if (_selectedColumns.Count == 0)
        {
            foreach (DataColumn column in _dataTable.Columns)
            {
                _selectedColumns.Add(column.ColumnName);
            }
        }

        Title = "Select Columns to Display";
        Width = 60;
        Height = Math.Min(30, _dataTable.Columns.Count + 8);

        InitializeComponents();
        SetupLayout();
        SetKeyboard();
    }

    private void InitializeComponents()
    {
        _instructionLabel = new Label
        {
            Text = "Use Space to toggle column selection:",
            X = 1,
            Y = 1,
            Width = Dim.Fill()! - 2,
            Height = 1
        };

        _columnsList = new ListView
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill()! - 2,
            Height = Dim.Fill()! - 1,
            AllowsMarking = false,
            AllowsMultipleSelection = false
        };
        // Disable letter-based navigation so shortcut keys ('a', 'n') aren't swallowed
        // by the ListView's collection navigator. Assigning null here throws
        // NullReferenceException in OnKeyDown and prevented Enter/Esc from closing
        // the dialog.
        _columnsList.KeystrokeNavigator.Matcher = new NoOpMatcher();

        RefreshColumnsList();

        _shortcutsLabel = new Label
        {
            Text = "space toggle | a all | n none | enter ok | esc cancel",
            X = 1,
            Y = Pos.Bottom(_columnsList),
            Width = Dim.Fill()! - 2,
            Height = 1
        };

        Add(_instructionLabel, _columnsList, _shortcutsLabel);
    }

    private void SetupLayout()
    {
        _columnsList.SetFocus();
    }

    private void SetKeyboard()
    {
        KeyBindings.ReplaceCommands(Key.Enter, Command.Accept);
        AddCommand(Command.Accept, () =>
        {
            OnOkClicked();
            return true;
        });

        KeyBindings.Add(Key.Esc, Command.Cancel);
        AddCommand(Command.Cancel, () =>
        {
            OnCancelClicked();
            return true;
        });

        _columnsList.KeyDown += (sender, key) =>
        {
            if (key == Key.Esc)
            {
                OnCancelClicked();
                key.Handled = true;
            }
            else if (key == Key.Space)
            {
                ToggleSelectedColumn();
                key.Handled = true;
            }
            else if (key.KeyCode == Key.A.KeyCode)
            {
                SelectAllColumns();
                key.Handled = true;
            }
            else if (key.KeyCode == Key.N.KeyCode)
            {
                SelectNoColumns();
                key.Handled = true;
            }
        };
    }

    private void ToggleSelectedColumn()
    {
        var selectedIndex = _columnsList.SelectedItem;
        if (selectedIndex < 0 || selectedIndex >= _dataTable.Columns.Count)
        {
            return;
        }

        var columnName = _dataTable.Columns[selectedIndex].ColumnName;

        if (_selectedColumns.Contains(columnName))
        {
            _selectedColumns.Remove(columnName);
        }
        else
        {
            _selectedColumns.Add(columnName);
        }

        RefreshColumnsList();
    }

    private void SelectAllColumns()
    {
        _selectedColumns.Clear();
        foreach (DataColumn column in _dataTable.Columns)
        {
            _selectedColumns.Add(column.ColumnName);
        }

        RefreshColumnsList();
    }

    private void SelectNoColumns()
    {
        _selectedColumns.Clear();
        RefreshColumnsList();
    }

    private void RefreshColumnsList()
    {
        var currentSelected = _columnsList.SelectedItem;
        var columnItems = new List<string>();

        foreach (DataColumn column in _dataTable.Columns)
        {
            var prefix = _selectedColumns.Contains(column.ColumnName) ? "[✓] " : "[ ] ";
            columnItems.Add($"{prefix}{column.ColumnName}");
        }

        _columnsList.SetSource(new ObservableCollection<string>(columnItems));
        _columnsList.SelectedItem = Math.Max(0, Math.Min(currentSelected, columnItems.Count - 1));
    }

    private void OnOkClicked()
    {
        if (_selectedColumns.Count == 0)
        {
            MessageBox.ErrorQuery("Validation Error", "At least one column must be selected.", "OK");
            return;
        }

        Application.RequestStop();
    }

    private void OnCancelClicked()
    {
        Application.RequestStop();
    }

    private sealed class NoOpMatcher : ICollectionNavigatorMatcher
    {
        public bool IsCompatibleKey(Key key) => false;
        public bool IsMatch(string search, object value) => false;
    }
}
