using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Input;
using Terminal.Gui.Drivers;

namespace KustoTerminal.UI.Dialogs
{
    public class JsonTreeViewDialog : Dialog
    {
        private TreeView _treeView;
        private Label _statusLabel;
        private Label _shortcutsLabel;
        private readonly string _jsonContent;
        private readonly string _columnName;

        public JsonTreeViewDialog(string columnName, string jsonContent)
        {
            _columnName = columnName ?? "JSON Content";
            _jsonContent = jsonContent ?? string.Empty;

            Title = $"JSON Tree View: {_columnName}";
            Width = 100;
            Height = 30;
            Modal = true;

            InitializeComponents();
            SetupLayout();
            SetKeyboard();
            LoadJsonData();
        }

        private void InitializeComponents()
        {
            _statusLabel = new Label()
            {
                Text = "Loading JSON...",
                X = 1,
                Y = 1,
                Width = Dim.Fill() - 2,
                Height = 1
            };

            _treeView = new TreeView()
            {
                X = 1,
                Y = 2,
                Width = Dim.Fill() - 2,
                Height = Dim.Fill() - 4,
                CanFocus = true
            };

            _shortcutsLabel = new Label()
            {
                Text = "↑↓: Navigate | →: Expand | ←: Collapse | Ctrl+C: Copy Selected | Ctrl+E: Expand All | Ctrl+R: Collapse All | Esc: Close",
                X = 1,
                Y = Pos.Bottom(_treeView),
                Width = Dim.Fill() - 2,
                Height = 1
            };

            Add(_statusLabel, _treeView, _shortcutsLabel);
        }

        private void SetupLayout()
        {
            _treeView.SetFocus();
        }

        private void SetKeyboard()
        {
            KeyBindings.ReplaceCommands(Key.Esc, Command.Cancel);
            AddCommand(Command.Cancel, () => { OnCloseClicked(); return true; });

            _treeView.KeyDown += (sender, key) =>
            {
                if (key.KeyCode == (KeyCode.CtrlMask | Key.C.KeyCode))
                {
                    OnCopySelectedClicked();
                    key.Handled = true;
                }
                else if (key.KeyCode == (KeyCode.CtrlMask | Key.E.KeyCode))
                {
                    OnExpandAllClicked();
                    key.Handled = true;
                }
                else if (key.KeyCode == (KeyCode.CtrlMask | Key.R.KeyCode))
                {
                    OnCollapseAllClicked();
                    key.Handled = true;
                }
            };
        }

        private void LoadJsonData()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_jsonContent))
                {
                    _statusLabel.Text = "No JSON content to display";
                    return;
                }

                // Parse and build tree
                var jsonDocument = JsonDocument.Parse(_jsonContent);
                var rootNode = BuildTreeFromJson("", jsonDocument.RootElement);
                
                _treeView.AddObject(rootNode);
                _treeView.ExpandAll();
                
                _statusLabel.Text = $"JSON parsed successfully - {GetJsonElementCount(jsonDocument.RootElement)} elements";
            }
            catch (JsonException ex)
            {
                _statusLabel.Text = $"Invalid JSON: {ex.Message}";
                
                // Create a simple text node with the raw content
                var errorNode = new JsonTreeNode("Raw Content", _jsonContent);
                _treeView.AddObject(errorNode);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error loading JSON: {ex.Message}";
            }
        }

        private JsonTreeNode BuildTreeFromJson(string name, JsonElement element)
        {
            var node = new JsonTreeNode(name, GetJsonElementDisplayValue(element));

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        var childNode = BuildTreeFromJson(property.Name, property.Value);
                        node.Children.Add(childNode);
                    }
                    break;

                case JsonValueKind.Array:
                    int index = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        var childNode = BuildTreeFromJson($"[{index}]", item);
                        node.Children.Add(childNode);
                        index++;
                    }
                    break;
            }

            return node;
        }

        private string GetJsonElementDisplayValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => $"\"{element.GetString()}\"",
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                JsonValueKind.Object => string.Empty,
                JsonValueKind.Array => string.Empty,
                _ => element.GetRawText()
            };
        }

        private int GetJsonElementCount(JsonElement element)
        {
            int count = 1; // Count the element itself

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        count += GetJsonElementCount(property.Value);
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        count += GetJsonElementCount(item);
                    }
                    break;
            }

            return count;
        }

        private void OnCopySelectedClicked()
        {
            try
            {
                var selectedObject = _treeView.SelectedObject;
                if (selectedObject is JsonTreeNode node)
                {
                    Clipboard.Contents = node.Value;
                    _statusLabel.Text = "Selected value copied to clipboard";
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Copy failed: {ex.Message}";
            }
        }

        private void OnExpandAllClicked()
        {
            try
            {
                _treeView.ExpandAll();
                _statusLabel.Text = "All nodes expanded";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Expand all failed: {ex.Message}";
            }
        }

        private void OnCollapseAllClicked()
        {
            try
            {
                CollapseAllRecursively();
                _statusLabel.Text = "All nodes collapsed recursively";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Collapse all failed: {ex.Message}";
            }
        }

        private void CollapseAllRecursively()
        {
            // Get all root objects and collapse them recursively
            foreach (var rootObject in _treeView.Objects)
            {
                if (rootObject is JsonTreeNode rootNode)
                {
                    CollapseNodeRecursively(rootNode);
                }
            }
        }

        private void CollapseNodeRecursively(JsonTreeNode node)
        {
            // First collapse all children recursively
            foreach (var child in node.Children.OfType<JsonTreeNode>())
            {
                CollapseNodeRecursively(child);
            }
            
            // Then collapse this node if it has children
            if (node.Children.Any())
            {
                _treeView.Collapse(node);
            }
        }

        private void OnCloseClicked()
        {
            Application.RequestStop();
        }
    }

    public class JsonTreeNode : TreeNode
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public JsonTreeNode(string name, string value)
            : base(GetDisplayText(name, value))
        {
            Name = name;
            Value = value;
        }

        private static string GetDisplayText(string name, string value)
        {
            return $"{name}: {value}";
        }
    }
}