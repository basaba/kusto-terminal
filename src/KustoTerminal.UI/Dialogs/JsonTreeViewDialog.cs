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
        private TreeView _treeView = null!;
        private Label _statusLabel = null!;
        private Label _shortcutsLabel = null!;
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
                Width = Dim.Fill()! - 2,
                Height = 1
            };

            _treeView = new TreeView()
            {
                X = 1,
                Y = 2,
                Width = Dim.Fill()! - 2,
                Height = Dim.Fill()! - 1,
                CanFocus = true
            };

            _shortcutsLabel = new Label()
            {
                Text = "Ctrl+E: Expand All | Ctrl+R: Collapse All",
                X = 1,
                Y = Pos.Bottom(_treeView),
                Width = Dim.Fill()! - 2,
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
                else if (key.KeyCode == Key.Enter.KeyCode)
                {
                    OnShowPropertyDetails();
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
                var rootNode = BuildTreeFromJson("$", jsonDocument.RootElement, "$");
                
                _treeView.AddObject(rootNode);
                _treeView.ExpandAll();
                
                _statusLabel.Text = $"JSON parsed successfully - {GetJsonElementCount(jsonDocument.RootElement)} elements";
            }
            catch (JsonException ex)
            {
                _statusLabel.Text = $"Invalid JSON: {ex.Message}";
                
                // Create a simple text node with the raw content
                var errorNode = new JsonTreeNode("Raw Content", _jsonContent, "$.RawContent", null);
                _treeView.AddObject(errorNode);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error loading JSON: {ex.Message}";
            }
        }

        private JsonTreeNode BuildTreeFromJson(string name, JsonElement element, string jsonPath)
        {
            var node = new JsonTreeNode(name, GetJsonElementDisplayValue(element), jsonPath, element);

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        var childPath = jsonPath == "$" ? $"$.{property.Name}" : $"{jsonPath}.{property.Name}";
                        var childNode = BuildTreeFromJson(property.Name, property.Value, childPath);
                        node.Children.Add(childNode);
                    }
                    break;

                case JsonValueKind.Array:
                    int index = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        var childPath = $"{jsonPath}[{index}]";
                        var childNode = BuildTreeFromJson($"[{index}]", item, childPath);
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

        private void OnShowPropertyDetails()
        {
            try
            {
                var selectedObject = _treeView.SelectedObject;
                if (selectedObject is JsonTreeNode node)
                {
                    var detailsDialog = new PropertyDetailsDialog(node.Name, node.GetFullValue(), node.JsonPath);
                    Application.Run(detailsDialog);
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Show details failed: {ex.Message}";
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
        public string JsonPath { get; set; }
        public JsonElement? OriginalElement { get; set; }

        public JsonTreeNode(string name, string value, string jsonPath, JsonElement? originalElement)
            : base(GetDisplayText(name, value))
        {
            Name = name;
            Value = value;
            JsonPath = jsonPath;
            OriginalElement = originalElement;
        }

        private static string GetDisplayText(string name, string value)
        {
            return $"{name}: {value}";
        }

        public string GetFullValue()
        {
            if (OriginalElement.HasValue)
            {
                var element = OriginalElement.Value;
                if (element.ValueKind == JsonValueKind.Object || element.ValueKind == JsonValueKind.Array)
                {
                    // For objects and arrays, return formatted JSON
                    return JsonSerializer.Serialize(element, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                }
            }
            
            // For leaf nodes or when no element is available, return the original value
            return Value ?? string.Empty;
        }
    }

    public class PropertyDetailsDialog : Dialog
    {
        public PropertyDetailsDialog(string propertyName, string value, string jsonPath)
        {
            Title = "Property Details";
            Width = Dim.Percent(92);
            Height = 20;
            Modal = true;
            Arrangement = ViewArrangement.Resizable;

            var nameLabel = new Label()
            {
                Text = "Property Name:",
                X = 1,
                Y = 0,
                Width = Dim.Fill()! - 2,
                Height = 1
            };

            var nameText = new TextView()
            {
                Text = propertyName,
                X = 1,
                Y = 1,
                Width = Dim.Fill()! - 2,
                Height = 1,
                ReadOnly = true
            };

            var pathLabel = new Label()
            {
                Text = "JSON Path:",
                X = 1,
                Y = 2,
                Width = Dim.Fill()! - 2,
                Height = 1
            };

            var pathText = new TextView()
            {
                Text = jsonPath,
                X = 1,
                Y = 3,
                Width = Dim.Fill()! - 2,
                Height = 1,
                ReadOnly = true
            };

            var valueLabel = new Label()
            {
                Text = "Value:",
                X = 1,
                Y = 4,
                Width = Dim.Fill()! - 2,
                Height = 1
            };

            var valueText = new TextView()
            {
                Text = value,
                X = 1,
                Y = 5,
                Width = Dim.Fill()! - 2,
                Height = Dim.Fill(),
                ReadOnly = true,
                WordWrap = true
            };

            Add(nameLabel, nameText,pathLabel, pathText, valueLabel, valueText);
        }
    }
}