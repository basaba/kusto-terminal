using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using Kusto.Data;
using KustoTerminal.UI.Charts;
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
            var dataPointsCollection = new DataPointsCollection(dataPoints, dataPoints.First());
            
            // Create series for each value column
            var colors = GetSeriesColors();
            var colorIndex = 0;

            foreach (var valueColumn in _valueColumns.Take(5)) // Limit to 5 series for readability
            {
                var series = CreateBrailleSeries(dataPointsCollection, valueColumn, colors[colorIndex % colors.Length]);
                //var lineAnnotation = CreateLineAnnotation(dataPoints, valueColumn, colors[colorIndex % colors.Length]);
                
                _graphView.Series.Add(series);
                //_graphView.Annotations.Add(lineAnnotation);
                
                colorIndex++;
            }

            ConfigureAxisAndScale(dataPointsCollection);
        }

        private BrailleSeries CreateBrailleSeries(DataPointsCollection dataPointsCollection, string valueColumn, AttributeTerminalGui color)
        {
            var dataPoints = dataPointsCollection.Points;
            var points = dataPoints
                .Where(p => p.Values.ContainsKey(valueColumn))
                .Select(p => new PointF(
                    (float) dataPointsCollection.GetXCoordinate(p), 
                    (float) dataPointsCollection.GetYCoordinate(p, valueColumn)))
                .ToList();

            var brailleSeries = new BrailleSeries();
            brailleSeries.AddPoints(points);
            return brailleSeries;
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

        // private PathAnnotation CreateLineAnnotation(List<TimeSeriesPoint> dataPoints, string valueColumn, AttributeTerminalGui color)
        // {
        //     var points = dataPoints
        //         .Where(p => p.Values.ContainsKey(valueColumn))
        //         .Select(p => new PointF(
        //             (float)(p.Time - dataPoints.First().Time).TotalHours,
        //             p.Values[valueColumn]))
        //         .ToList();
        //
        //     return new PathAnnotation
        //     {
        //         Points = points,
        //         LineColor = color,
        //         BeforeSeries = false
        //     };
        // }

        private void ConfigureAxisAndScale(DataPointsCollection dataPointsCollection)
        {
            ConfigureAxisX(dataPointsCollection);
            ConfigureAxisY(dataPointsCollection);
        }

        private void ConfigureAxisX(DataPointsCollection dataPointsCollection)
        {
            _graphView.AxisX.Increment = 1;
            _graphView.AxisX.Minimum = 0;
            _graphView.AxisX.ShowLabelsEvery = 1;

            var dataRangeX = dataPointsCollection.GetXRange();
            var totalPixels = _graphView.Viewport.Width - _graphView.MarginLeft;
            _graphView.CellSize = new PointF((float)dataRangeX/totalPixels, 1f);

            var startTime = dataPointsCollection.PivotPoint.Time;
            
            _graphView.AxisX.LabelGetter = v => 
            {
                var time = startTime.AddHours(v.Value);
                return time.ToString();
            };
        }

        private void ConfigureAxisY(DataPointsCollection dataPointsCollection)
        {
            _graphView.AxisY.Increment = 1;
            _graphView.AxisY.Minimum = 0;
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
        
        private class DataPointsCollection
        {
            public DataPointsCollection(List<TimeSeriesPoint> points, TimeSeriesPoint pivotPoint)
            {
                Points = points;
                PivotPoint = new TimeSeriesPoint
                {
                    Time = pivotPoint.Time,
                    Values = pivotPoint.Values
                };
            }
            
            public List<TimeSeriesPoint> Points { get;  private set; } = null;
            public TimeSeriesPoint PivotPoint { get; private set; } = null;

            public double GetXCoordinate(TimeSeriesPoint timeSeriesPoint)
            {
                return TimespanToDouble(timeSeriesPoint.Time -  PivotPoint.Time);
            }

            public double GetYCoordinate(TimeSeriesPoint timeSeriesPoint, string valueColumn)
            {
                return timeSeriesPoint.Values[valueColumn];
            }

            public double GetXRange()
            {
                return TimespanToDouble(Points.Last().Time - Points.First().Time);
            }

            private static double TimespanToDouble(TimeSpan span)
            {
                return span.TotalHours;
            }
        }
    }
}
