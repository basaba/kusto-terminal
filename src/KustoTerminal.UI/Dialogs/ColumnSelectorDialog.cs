using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Input;
using Terminal.Gui.Drivers;

namespace KustoTerminal.UI.Dialogs
{
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

            // If no columns are currently selected, select all by default
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
            _instructionLabel = new Label()
            {
                Text = "Use Space to toggle column selection:",
                X = 1,
                Y = 1,
                Width = Dim.Fill()! - 2,
                Height = 1
            };

            _columnsList = new ListView()
            {
                X = 1,
                Y = 3,
                Width = Dim.Fill()! - 2,
                Height = Dim.Fill()! - 1,
                AllowsMarking = false,
                AllowsMultipleSelection = false
            };

            // Create list of columns with checkboxes
            RefreshColumnsList();

            _shortcutsLabel = new Label()
            {
                Text = "Space: Toggle | Ctrl+A: All | Ctrl+N: None | Enter: OK | Esc: Cancel",
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
            AddCommand(Command.Accept, () => { OnOkClicked(); return true; });
            
            KeyBindings.ReplaceCommands(Key.Esc, Command.Cancel);
            AddCommand(Command.Cancel, () => { OnCancelClicked(); return true; });

            _columnsList.KeyDown += (sender, key) =>
            {
                if (key == Key.Space)
                {
                    ToggleSelectedColumn();
                    key.Handled = true;
                }
                else if (key.KeyCode == (Key.A.KeyCode | KeyCode.CtrlMask))
                {
                    SelectAllColumns();
                    key.Handled = true;
                }
                else if (key.KeyCode == (Key.N.KeyCode | KeyCode.CtrlMask))
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
                return;

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
            // Ensure at least one column is selected
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
    }
}