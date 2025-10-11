using System;
using System.Collections.Generic;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Input;
using Terminal.Gui.Drivers;

namespace KustoTerminal.UI.Dialogs
{
    public class ShareDialog : Dialog
    {
        private CheckBox _queryCheckBox;
        private CheckBox _resultCheckBox;
        private Label _instructionLabel;
        private Label _shortcutsLabel;

        public bool CopyQuery { get; private set; }
        public bool CopyResult { get; private set; }
        public bool WasCanceled { get; private set; } = true;

        private readonly bool _hasQuery;
        private readonly bool _hasResult;

        public ShareDialog(bool hasQuery = true, bool hasResult = true)
        {
            _hasQuery = true;
            _hasResult = true;

            Title = "Share Options";
            Width = 50;
            Height = 10;

            InitializeComponents();
            SetupLayout();
            SetKeyboard();
        }

        private void InitializeComponents()
        {
            _instructionLabel = new Label()
            {
                Text = "Select what to copy to clipboard:",
                X = 1,
                Y = 1,
                Width = Dim.Fill() - 2,
                Height = 1
            };

            _queryCheckBox = new CheckBox()
            {
                Text = "Query",
                X = 3,
                Y = 3,
                Width = Dim.Fill() - 4,
                Height = 1
            };

            _resultCheckBox = new CheckBox()
            {
                Text = "Result",
                X = 3,
                Y = 4,
                Width = Dim.Fill() - 4,
                Height = 1
            };

            // Set initial checked state based on availability
            if (_hasQuery)
            {
                _queryCheckBox.CheckedState = CheckState.Checked;
            }
            else
            {
                _queryCheckBox.Enabled = false;
            }

            if (_hasResult)
            {
                _resultCheckBox.CheckedState = CheckState.Checked;
            }
            else
            {
                _resultCheckBox.Enabled = false;
            }

            _shortcutsLabel = new Label()
            {
                Text = "Space: Toggle | Enter: OK | Esc: Cancel",
                X = 1,
                Y = Pos.Bottom(_resultCheckBox) + 1,
                Width = Dim.Fill() - 2,
                Height = 1
            };

            Add(_instructionLabel, _queryCheckBox, _resultCheckBox, _shortcutsLabel);
        }

        private void SetupLayout()
        {
            // Focus the first enabled checkbox
            if (_hasQuery)
            {
                _queryCheckBox.SetFocus();
            }
            else if (_hasResult)
            {
                _resultCheckBox.SetFocus();
            }
        }

        private void SetKeyboard()
        {
            KeyBindings.ReplaceCommands(Key.Enter, Command.Accept);
            AddCommand(Command.Accept, () => { OnOkClicked(); return true; });
            
            KeyBindings.ReplaceCommands(Key.Esc, Command.Cancel);
            AddCommand(Command.Cancel, () => { OnCancelClicked(); return true; });
        }

        private void OnOkClicked()
        {
            // Validate that at least one option is selected
            if (_queryCheckBox.CheckedState != CheckState.Checked && _resultCheckBox.CheckedState != CheckState.Checked)
            {
                MessageBox.ErrorQuery("Validation Error", "Please select at least one option to copy.", "OK");
                return;
            }

            CopyQuery = _queryCheckBox.CheckedState == CheckState.Checked;
            CopyResult = _resultCheckBox.CheckedState == CheckState.Checked;
            WasCanceled = false;

            Application.RequestStop();
        }

        private void OnCancelClicked()
        {
            WasCanceled = true;
            Application.RequestStop();
        }
    }
}
