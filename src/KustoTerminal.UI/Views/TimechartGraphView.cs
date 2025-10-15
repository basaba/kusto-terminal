using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using Terminal.Gui;
using Terminal.Gui.Drawing;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using AttributeTerminalGui = Terminal.Gui.Drawing.Attribute;
using Color = System.Drawing.Color;
using ColorTerminalGui = Terminal.Gui.Drawing.Color;

namespace KustoTerminal.UI.Views
{
    public class TimechartGraphView : View
    {
        private GraphView _graphView;
        private DataTable? _data;
        private List<string> _timeColumns = new();
        private List<string> _valueColumns = new();
        private string? _detectedTimeColumn;

        public TimechartGraphView()
        {
            InitializeGraphView();
            SetupLayout();
        }

        private void InitializeGraphView()
        {
            _graphView = new GraphView()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                BorderStyle = LineStyle.Single,
                Title = "Time Chart"
            };

            // Setup margins for axis labels
            _graphView.MarginLeft = 8;
            _graphView.MarginBottom = 2;

            // Configure axes
            _graphView.AxisX.Text = "Time";
            _graphView.AxisY.Text = "Value";
            
            // Set up axis formatting will be done when data is loaded
        }

        private void SetupLayout()
        {
            Add(_graphView);
        }

        public bool CanDisplayData(DataTable data)
        {
            if (data == null || data.Rows.Count == 0 || data.Columns.Count < 2)
                return false;

            // Try to detect time columns
            DetectColumns(data);
            
            return !string.IsNullOrEmpty(_detectedTimeColumn) && _valueColumns.Any();
        }

        public void SetData(DataTable data)
        {
            _data = data;
            if (!CanDisplayData(data))
            {
                return;
            }

            try
            {
                RenderTimechart();
            }
            catch (Exception ex)
            {
                // If rendering fails, show error in graph title
                _graphView.Title = $"Error rendering chart: {ex.Message}";
            }
        }

        private void DetectColumns(DataTable data)
        {
            _timeColumns.Clear();
            _valueColumns.Clear();
            _detectedTimeColumn = null;

            foreach (DataColumn column in data.Columns)
            {
                var columnType = column.DataType;
                var columnName = column.ColumnName.ToLowerInvariant();

                // Detect time columns
                if (columnType == typeof(DateTime) || 
                    columnType == typeof(DateTimeOffset) ||
                    columnName.Contains("time") ||
                    columnName.Contains("date") ||
                    columnName == "timestamp")
                {
                    _timeColumns.Add(column.ColumnName);
                    if (_detectedTimeColumn == null)
                    {
                        _detectedTimeColumn = column.ColumnName;
                    }
                }
                // Detect numeric value columns
                else if (IsNumericType(columnType))
                {
                    _valueColumns.Add(column.ColumnName);
                }
            }

            // If no obvious time column found, check if we can parse the first column as datetime
            if (string.IsNullOrEmpty(_detectedTimeColumn) && data.Columns.Count > 0)
            {
                var firstColumn = data.Columns[0].ColumnName;
                if (CanParseAsDateTime(data, firstColumn))
                {
                    _detectedTimeColumn = firstColumn;
                    _timeColumns.Add(firstColumn);
                }
            }
        }

        private static bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(float) || 
                   type == typeof(double) || type == typeof(decimal) || type == typeof(short) ||
                   type == typeof(byte) || type == typeof(uint) || type == typeof(ulong) ||
                   type == typeof(ushort) || type == typeof(sbyte);
        }

        private bool CanParseAsDateTime(DataTable data, string columnName)
        {
            // Check first few rows to see if they can be parsed as datetime
            var sampleSize = Math.Min(5, data.Rows.Count);
            var parsableCount = 0;

            for (int i = 0; i < sampleSize; i++)
            {
                var value = data.Rows[i][columnName]?.ToString();
                if (!string.IsNullOrEmpty(value) && 
                    (DateTime.TryParse(value, out _) || DateTimeOffset.TryParse(value, out _)))
                {
                    parsableCount++;
                }
            }

            return parsableCount >= sampleSize * 0.8; // 80% of samples should be parsable
        }

        private void RenderTimechart()
        {
            if (_data == null || string.IsNullOrEmpty(_detectedTimeColumn))
                return;

            _graphView.Reset();
            _graphView.Series.Clear();
            _graphView.Annotations.Clear();

            var dataPoints = ExtractTimeSeriesData();
            if (!dataPoints.Any())
                return;

            // Create series for each value column
            var colors = GetSeriesColors();
            var colorIndex = 0;

            foreach (var valueColumn in _valueColumns.Take(5)) // Limit to 5 series for readability
            {
                var series = CreateScatterSeries(dataPoints, valueColumn, colors[colorIndex % colors.Length]);
                var lineAnnotation = CreateLineAnnotation(dataPoints, valueColumn, colors[colorIndex % colors.Length]);
                
                _graphView.Series.Add(series);
                _graphView.Annotations.Add(lineAnnotation);
                
                colorIndex++;
            }

            ConfigureAxisAndScale(dataPoints);
        }

        private List<TimeSeriesPoint> ExtractTimeSeriesData()
        {
            var points = new List<TimeSeriesPoint>();

            foreach (DataRow row in _data!.Rows)
            {
                var timeValue = ParseTimeValue(row[_detectedTimeColumn!]);
                if (!timeValue.HasValue)
                    continue;

                var point = new TimeSeriesPoint { Time = timeValue.Value };

                foreach (var valueColumn in _valueColumns)
                {
                    var value = ParseNumericValue(row[valueColumn]);
                    if (value.HasValue)
                    {
                        point.Values[valueColumn] = value.Value;
                    }
                }

                if (point.Values.Any())
                {
                    points.Add(point);
                }
            }

            return points.OrderBy(p => p.Time).ToList();
        }

        private DateTime? ParseTimeValue(object value)
        {
            if (value == null || value == DBNull.Value)
                return null;

            if (value is DateTime dt)
                return dt;

            if (value is DateTimeOffset dto)
                return dto.DateTime;

            var stringValue = value.ToString();
            if (DateTime.TryParse(stringValue, out var parsed))
                return parsed;

            if (DateTimeOffset.TryParse(stringValue, out var parsedOffset))
                return parsedOffset.DateTime;

            return null;
        }

        private float? ParseNumericValue(object value)
        {
            if (value == null || value == DBNull.Value)
                return null;

            if (float.TryParse(value.ToString(), out var result))
                return result;

            return null;
        }

        private ScatterSeries CreateScatterSeries(List<TimeSeriesPoint> dataPoints, string valueColumn, AttributeTerminalGui color)
        {
            var points = dataPoints
                .Where(p => p.Values.ContainsKey(valueColumn))
                .Select(p => new PointF(
                    (float)(p.Time - dataPoints.First().Time).TotalHours, 
                    p.Values[valueColumn]))
                .ToList();

            return new ScatterSeries
            {
                Points = points,
                Fill = new GraphCellToRender(new Rune('●'), color)
            };
        }

        private PathAnnotation CreateLineAnnotation(List<TimeSeriesPoint> dataPoints, string valueColumn, AttributeTerminalGui color)
        {
            var points = dataPoints
                .Where(p => p.Values.ContainsKey(valueColumn))
                .Select(p => new PointF(
                    (float)(p.Time - dataPoints.First().Time).TotalHours,
                    p.Values[valueColumn]))
                .ToList();

            return new PathAnnotation
            {
                Points = points,
                LineColor = color,
                BeforeSeries = false
            };
        }

        private void ConfigureAxisAndScale(List<TimeSeriesPoint> dataPoints)
        {
            if (!dataPoints.Any())
                return;

            var minTime = dataPoints.First().Time;
            var maxTime = dataPoints.Last().Time;
            var timeSpan = maxTime - minTime;

            // Configure time axis using viewport dimensions for better scaling
            var totalHours = (float)timeSpan.TotalHours;
            if (totalHours > 0)
            {
                // Use viewport width to determine appropriate cell size for time axis
                // This ensures the graph fits properly in the available screen space
                var viewportWidth = Math.Max(1, _graphView.Viewport.Width - _graphView.MarginLeft - 1);
                var cellSizeX = Math.Max(0.01f, totalHours / viewportWidth);
                
                // Calculate increment based on viewport width to show reasonable number of labels
                var desiredLabels = Math.Max(5, viewportWidth / 10); // Aim for labels every 10 characters or so
                _graphView.AxisX.Increment = Math.Max(1, totalHours / desiredLabels);
                _graphView.AxisX.ShowLabelsEvery = 1;
                
                // Configure value axis using viewport height
                var allValues = dataPoints.SelectMany(p => p.Values.Values).ToList();
                if (allValues.Any())
                {
                    var minValue = allValues.Min();
                    var maxValue = allValues.Max();
                    var valueRange = maxValue - minValue;

                    if (valueRange > 0)
                    {
                        // Use viewport height to determine appropriate cell size for value axis
                        var viewportHeight = Math.Max(1, _graphView.Viewport.Height - _graphView.MarginBottom - 1);
                        var cellSizeY = Math.Max(0.1f, valueRange / viewportHeight);
                        
                        // Set the cell size using both calculated dimensions
                        _graphView.CellSize = new PointF(cellSizeX, cellSizeY);
                        
                        // Calculate increment for Y axis based on viewport height
                        var desiredYLabels = Math.Max(5, viewportHeight / 3); // Aim for labels every 3 rows
                        _graphView.AxisY.Increment = Math.Max(0.1f, valueRange / desiredYLabels);
                        _graphView.AxisY.ShowLabelsEvery = 1;
                        _graphView.AxisY.Minimum = minValue - valueRange * 0.1f;
                    }
                    else
                    {
                        // Fallback for when there's no value range
                        _graphView.CellSize = new PointF(cellSizeX, 1);
                        _graphView.AxisY.Increment = 1;
                        _graphView.AxisY.ShowLabelsEvery = 1;
                        _graphView.AxisY.Minimum = minValue - 1;
                    }
                }
                else
                {
                    // Fallback when no values available
                    _graphView.CellSize = new PointF(cellSizeX, 1);
                }
            }

            // Store the base time for label formatting
            _baseTime = minTime;
            
            // Set up axis formatting now that we have the base time
            SetupAxisFormatting();
        }

        private DateTime _baseTime;

        private void SetupAxisFormatting()
        {
            _graphView.AxisX.LabelGetter = v => 
            {
                var time = _baseTime.AddHours(v.Value);
                return time.ToString("HH:mm");
            };
            
            _graphView.AxisY.LabelGetter = v => v.Value.ToString("F1");
        }

        private static AttributeTerminalGui[] GetSeriesColors()
        {
            return new[]
            {
                new AttributeTerminalGui(ColorTerminalGui.BrightCyan, StandardColor.RaisinBlack),
                new AttributeTerminalGui(ColorTerminalGui.BrightMagenta, ColorTerminalGui.Black),
                new AttributeTerminalGui(ColorTerminalGui.BrightYellow, ColorTerminalGui.Black),
                new AttributeTerminalGui(ColorTerminalGui.BrightGreen, ColorTerminalGui.Black),
                new AttributeTerminalGui(ColorTerminalGui.BrightRed, ColorTerminalGui.Black),
                new AttributeTerminalGui(ColorTerminalGui.BrightBlue, ColorTerminalGui.Black),
                new AttributeTerminalGui(ColorTerminalGui.White, ColorTerminalGui.Black)
            };
        }

        private class TimeSeriesPoint
        {
            public DateTime Time { get; set; }
            public Dictionary<string, float> Values { get; set; } = new();
        }
    }
}
