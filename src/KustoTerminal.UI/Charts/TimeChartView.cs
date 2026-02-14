using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using KustoTerminal.Core.Models;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace KustoTerminal.UI.Charts;

/// <summary>
/// A Terminal.Gui View that renders a timechart using braille dot characters
/// for sub-character resolution line plotting.
/// </summary>
public class TimeChartView : View
{
    private TimeChartData? _chartData;
    private Color _backGroundColor;

    // Layout constants
    private const int YAxisLabelWidth = 12;
    private const int XAxisLabelHeight = 3;
    private const int TopPadding = 1; // for legend

    // Braille dot offsets: each character cell is 2 columns x 4 rows of dots
    // Braille base: U+2800
    // Dot positions (col, row) -> bit:
    //   (0,0)->0  (1,0)->3
    //   (0,1)->1  (1,1)->4
    //   (0,2)->2  (1,2)->5
    //   (0,3)->6  (1,3)->7
    private static readonly int[,] BrailleDotBits = {
        { 0, 1, 2, 6 },  // column 0, rows 0-3
        { 3, 4, 5, 7 }   // column 1, rows 0-3
    };

    // Series colors
    private static readonly Color[] SeriesColors = {
        Color.BrightBlue,
        Color.BrightGreen,
        Color.BrightRed,
        Color.BrightYellow,
        Color.BrightCyan,
        Color.BrightMagenta,
        Color.White,
        Color.Blue,
        Color.Green,
        Color.Red
    };

    public TimeChartView()
    {
        CanFocus = true;
        _backGroundColor = GetAttributeForRole(VisualRole.Normal).Background;
    }

    public void SetData(TimeChartData? data)
    {
        _chartData = data;
        SetNeedsDraw();
    }

    protected override bool OnDrawingContent()
    {
        var viewport = Viewport;
        
        if (_chartData == null || !_chartData.IsValid)
        {
            DrawCenteredText(viewport, "No chart data available");
            return true;
        }

        if (viewport.Width < 20 || viewport.Height < 8)
        {
            DrawCenteredText(viewport, "View too small for chart");
            return true;
        }

        DrawChart(viewport);
        return true;
    }

    private void DrawChart(System.Drawing.Rectangle viewport)
    {
        var chartData = _chartData!;

        // Calculate layout regions
        int legendY = 0;
        int chartStartY = TopPadding;
        int chartEndY = viewport.Height - XAxisLabelHeight;
        int chartStartX = YAxisLabelWidth;
        int chartEndX = viewport.Width;

        int chartWidth = chartEndX - chartStartX;
        int chartHeight = chartEndY - chartStartY;

        if (chartWidth < 4 || chartHeight < 2)
            return;

        // Draw legend
        DrawLegend(chartData, legendY, chartStartX, chartWidth);

        // Calculate Y range with nice padding
        double minVal = chartData.MinValue;
        double maxVal = chartData.MaxValue;
        if (Math.Abs(maxVal - minVal) < 1e-10)
        {
            minVal -= 1;
            maxVal += 1;
        }
        else
        {
            double padding = (maxVal - minVal) * 0.05;
            minVal -= padding;
            maxVal += padding;
        }

        // Draw Y axis labels
        DrawYAxisLabels(chartStartY, chartHeight, chartStartX, minVal, maxVal);

        // Draw X axis labels
        DrawXAxisLabels(chartData, chartEndY, chartStartX, chartWidth);

        // Draw axis lines
        DrawAxisLines(chartStartX, chartStartY, chartEndY, chartEndX);

        // Render each series using braille characters
        for (int s = 0; s < chartData.Series.Count; s++)
        {
            var series = chartData.Series[s];
            var color = SeriesColors[s % SeriesColors.Length];
            DrawSeriesBraille(series, chartData.TimePoints, chartStartX, chartStartY,
                chartWidth, chartHeight, minVal, maxVal, color);
        }
    }

    private void DrawLegend(TimeChartData chartData, int y, int startX, int chartWidth)
    {
        int x = startX;
        for (int s = 0; s < chartData.Series.Count; s++)
        {
            var series = chartData.Series[s];
            var color = SeriesColors[s % SeriesColors.Length];

            if (x + series.Name.Length + 4 > startX + chartWidth)
                break;

            // Draw color indicator
            SetAttribute(new Attribute(color, _backGroundColor));
            DrawText(x, y, "━━");
            x += 2;

            // Draw series name
            SetAttribute(new Attribute(Color.White, _backGroundColor));
            var name = series.Name.Length > 20 ? series.Name[..17] + "..." : series.Name;
            DrawText(x, y, $" {name}");
            x += name.Length + 2;

            // Separator
            if (s < chartData.Series.Count - 1)
            {
                DrawText(x, y, "  ");
                x += 2;
            }
        }
    }

    private void DrawYAxisLabels(int chartStartY, int chartHeight, int chartStartX, double minVal, double maxVal)
    {
        SetAttribute(new Attribute(Color.Gray, _backGroundColor));

        int labelCount = Math.Min(chartHeight, 8);
        for (int i = 0; i <= labelCount; i++)
        {
            int y = chartStartY + chartHeight - 1 - (int)((double)i / labelCount * (chartHeight - 1));
            double value = minVal + (maxVal - minVal) * i / labelCount;

            string label = FormatNumber(value);
            label = label.PadLeft(YAxisLabelWidth - 1);
            if (label.Length > YAxisLabelWidth - 1)
                label = label[..(YAxisLabelWidth - 1)];

            DrawText(0, y, label);
        }
    }

    private void DrawXAxisLabels(TimeChartData chartData, int y, int chartStartX, int chartWidth)
    {
        SetAttribute(new Attribute(Color.Gray, _backGroundColor));

        var timePoints = chartData.TimePoints;
        if (timePoints.Count == 0)
            return;

        var firstTime = timePoints[0];
        var lastTime = timePoints[^1];
        var totalSpan = lastTime - firstTime;

        // Determine format based on time span
        string timeFormat = DetermineTimeFormat(totalSpan);
        string? dateFormat = DetermineDateFormat(totalSpan);

        // Calculate how many labels we can fit (each label ~16-20 chars wide with spacing)
        int labelWidth = 20;
        int maxLabels = Math.Max(2, chartWidth / labelWidth);
        maxLabels = Math.Min(maxLabels, timePoints.Count);

        // Pick actual data point indices that are evenly spaced
        var labelIndices = new List<int>();
        if (timePoints.Count <= maxLabels)
        {
            // Show all points if few enough
            for (int i = 0; i < timePoints.Count; i++)
                labelIndices.Add(i);
        }
        else
        {
            for (int i = 0; i < maxLabels; i++)
            {
                int idx = (int)Math.Round((double)i / (maxLabels - 1) * (timePoints.Count - 1));
                idx = Math.Clamp(idx, 0, timePoints.Count - 1);
                if (labelIndices.Count == 0 || labelIndices[^1] != idx)
                    labelIndices.Add(idx);
            }
        }

        // Track occupied x ranges to avoid label overlap
        var occupiedRanges = new List<(int start, int end)>();

        foreach (var idx in labelIndices)
        {
            var time = timePoints[idx];

            // Calculate x position based on the data point's position in the series
            double xFrac = timePoints.Count == 1 ? 0.5 : (double)idx / (timePoints.Count - 1);
            int xPos = chartStartX + (int)(xFrac * (chartWidth - 1));

            // Format the time label
            string label = time.ToString(timeFormat);

            // Center the label on its x position
            int labelX = xPos - label.Length / 2;
            if (labelX < chartStartX) labelX = chartStartX;
            if (labelX + label.Length > chartStartX + chartWidth)
                labelX = chartStartX + chartWidth - label.Length;

            // Check for overlap with already-placed labels
            bool overlaps = false;
            foreach (var (start, end) in occupiedRanges)
            {
                if (labelX < end + 1 && labelX + label.Length > start)
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps)
                continue;

            // Draw tick mark on the axis line
            DrawText(xPos, y, "┬");

            // Draw time label (line 1 below axis)
            DrawText(labelX, y + 1, label);

            // Optionally draw date label (line 2 below axis) for context
            if (dateFormat != null)
            {
                string dateLabel = time.ToString(dateFormat);
                int dateLabelX = xPos - dateLabel.Length / 2;
                if (dateLabelX < chartStartX) dateLabelX = chartStartX;
                if (dateLabelX + dateLabel.Length > chartStartX + chartWidth)
                    dateLabelX = chartStartX + chartWidth - dateLabel.Length;
                // Only draw if we have a 2nd row available
                DrawText(dateLabelX, y + 2, dateLabel);
            }

            occupiedRanges.Add((labelX, labelX + label.Length));
        }
    }

    private void DrawAxisLines(int chartStartX, int chartStartY, int chartEndY, int chartEndX)
    {
        SetAttribute(new Attribute(Color.DarkGray, _backGroundColor));

        // Vertical axis (left side)
        for (int y = chartStartY; y < chartEndY; y++)
        {
            DrawText(chartStartX - 1, y, "│");
        }

        // Horizontal axis (bottom)
        for (int x = chartStartX - 1; x < chartEndX; x++)
        {
            DrawText(x, chartEndY, "─");
        }

        // Corner
        DrawText(chartStartX - 1, chartEndY, "└");
    }

    private void DrawSeriesBraille(ChartSeries series, List<DateTime> timePoints,
        int chartStartX, int chartStartY, int chartWidth, int chartHeight,
        double minVal, double maxVal, Color color)
    {
        if (series.Values.Count < 2 || chartWidth <= 0 || chartHeight <= 0)
            return;

        // Braille canvas: each cell is 2 dots wide, 4 dots tall
        int canvasDotsX = chartWidth * 2;
        int canvasDotsY = chartHeight * 4;

        // Create braille canvas (initialized to 0)
        var canvas = new int[chartWidth, chartHeight];

        double yRange = maxVal - minVal;
        if (yRange <= 0) yRange = 1;

        // Map each data point to a dot position and draw lines between consecutive points
        var points = new List<(double dx, double dy)>();
        for (int i = 0; i < series.Values.Count && i < timePoints.Count; i++)
        {
            if (!series.Values[i].HasValue)
                continue;

            double xFrac = timePoints.Count == 1 ? 0.5 : (double)i / (timePoints.Count - 1);
            double yFrac = (series.Values[i]!.Value - minVal) / yRange;

            double dx = xFrac * (canvasDotsX - 1);
            double dy = (1 - yFrac) * (canvasDotsY - 1);

            points.Add((dx, dy));
        }

        // Draw lines between consecutive points using Bresenham-style dot plotting
        for (int p = 0; p < points.Count - 1; p++)
        {
            PlotBrailleLine(canvas, chartWidth, chartHeight,
                (int)Math.Round(points[p].dx), (int)Math.Round(points[p].dy),
                (int)Math.Round(points[p + 1].dx), (int)Math.Round(points[p + 1].dy));
        }

        // Also plot the actual data points (to ensure they're visible even for single points)
        foreach (var pt in points)
        {
            SetBrailleDot(canvas, chartWidth, chartHeight, (int)Math.Round(pt.dx), (int)Math.Round(pt.dy));
        }

        // Render braille characters
        SetAttribute(new Attribute(color, _backGroundColor));

        for (int cy = 0; cy < chartHeight; cy++)
        {
            for (int cx = 0; cx < chartWidth; cx++)
            {
                if (canvas[cx, cy] != 0)
                {
                    char brailleChar = (char)(0x2800 + canvas[cx, cy]);
                    DrawText(chartStartX + cx, chartStartY + cy, brailleChar.ToString());
                }
            }
        }
    }

    private static void PlotBrailleLine(int[,] canvas, int canvasW, int canvasH,
        int x0, int y0, int x1, int y1)
    {
        // Bresenham's line algorithm in braille dot space
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            SetBrailleDot(canvas, canvasW, canvasH, x0, y0);

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private static void SetBrailleDot(int[,] canvas, int canvasW, int canvasH, int dotX, int dotY)
    {
        // Convert dot coordinates to cell coordinates
        int cellX = dotX / 2;
        int cellY = dotY / 4;

        if (cellX < 0 || cellX >= canvasW || cellY < 0 || cellY >= canvasH)
            return;

        int subX = dotX % 2;
        int subY = dotY % 4;

        canvas[cellX, cellY] |= (1 << BrailleDotBits[subX, subY]);
    }

    private void DrawCenteredText(System.Drawing.Rectangle viewport, string text)
    {
        int x = Math.Max(0, (viewport.Width - text.Length) / 2);
        int y = viewport.Height / 2;

        SetAttribute(new Attribute(Color.Gray, _backGroundColor));
        DrawText(x, y, text);
    }

    /// <summary>
    /// Draw text at the given cell position, clipping to the viewport.
    /// Uses the Terminal.Gui v2 drawing API via Move + AddRune.
    /// </summary>
    private void DrawText(int x, int y, string text)
    {
        var vp = Viewport;
        if (y < 0 || y >= vp.Height)
            return;

        Move(x, y);
        var driver = Application.Driver;
        if (driver == null)
            return;

        for (int i = 0; i < text.Length; i++)
        {
            int cx = x + i;
            if (cx < 0)
                continue;
            if (cx >= vp.Width)
                break;

            driver.AddRune(new System.Text.Rune(text[i]));
        }
    }

    private static string FormatNumber(double value)
    {
        double absValue = Math.Abs(value);
        if (absValue >= 1_000_000_000)
            return (value / 1_000_000_000).ToString("0.##") + "B";
        if (absValue >= 1_000_000)
            return (value / 1_000_000).ToString("0.##") + "M";
        if (absValue >= 1_000)
            return (value / 1_000).ToString("0.##") + "K";
        if (absValue < 0.01 && absValue > 0)
            return value.ToString("0.####");
        if (absValue < 1)
            return value.ToString("0.##");
        return value.ToString("0.##");
    }

    private static string DetermineTimeFormat(TimeSpan totalSpan)
    {
        if (totalSpan.TotalDays > 365)
            return "yyyy-MM-dd";
        if (totalSpan.TotalDays > 7)
            return "MM-dd HH:mm";
        if (totalSpan.TotalDays > 1)
            return "ddd HH:mm";
        if (totalSpan.TotalHours > 1)
            return "HH:mm";
        return "HH:mm:ss";
    }

    /// <summary>
    /// Returns a secondary date format for context beneath the time label,
    /// or null if the primary format already includes enough date info.
    /// </summary>
    private static string? DetermineDateFormat(TimeSpan totalSpan)
    {
        if (totalSpan.TotalDays > 365)
            return null; // primary already has year+month
        if (totalSpan.TotalDays > 7)
            return null; // primary already has month+day+time
        if (totalSpan.TotalDays > 1)
            return "yyyy-MM-dd";
        if (totalSpan.TotalHours > 1)
            return "yyyy-MM-dd";
        return "yyyy-MM-dd";
    }
}