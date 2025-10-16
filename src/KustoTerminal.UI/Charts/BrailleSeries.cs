using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Terminal.Gui;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;

namespace KustoTerminal.UI.Charts;

/// <summary>
/// Represents a data point for the braille series
/// </summary>
public struct BrailleDataPoint
{
    public double X { get; }
    public double Y { get; }
    
    public BrailleDataPoint(double x, double y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// A series implementation that renders smooth lines using braille characters,
/// providing sub-character resolution for high-quality line charts in terminal environments.
/// This demonstrates the same technique used by termdash for smooth line rendering.
/// </summary>
public class BrailleSeries : ISeries
{
    private readonly List<BrailleDataPoint> _dataPoints;
    private string _name;
    private bool _visible;
    
    public BrailleSeries(string name = "")
    {
        _dataPoints = new List<BrailleDataPoint>();
        _name = name;
        _visible = true;
    }
    
    /// <summary>
    /// Gets or sets the name of the series
    /// </summary>
    public string Name
    {
        get => _name;
        set => _name = value ?? "";
    }
    
    /// <summary>
    /// Gets or sets whether the series is visible
    /// </summary>
    public bool Visible
    {
        get => _visible;
        set => _visible = value;
    }
    
    /// <summary>
    /// Gets the collection of data points
    /// </summary>
    public IReadOnlyList<BrailleDataPoint> DataPoints => _dataPoints;
    
    /// <summary>
    /// Adds a data point to the series
    /// </summary>
    public void AddPoint(double x, double y)
    {
        _dataPoints.Add(new BrailleDataPoint(x, y));
    }
    
    /// <summary>
    /// Adds multiple data points to the series
    /// </summary>
    public void AddPoints(IEnumerable<BrailleDataPoint> points)
    {
        _dataPoints.AddRange(points);
    }
    
    /// <summary>
    /// Clears all data points from the series
    /// </summary>
    public void Clear()
    {
        _dataPoints.Clear();
    }
    
    /// <summary>
    /// Gets the minimum and maximum X values in the series
    /// </summary>
    public (double min, double max) GetXRange()
    {
        if (_dataPoints.Count == 0) return (0, 1);
        return (_dataPoints.Min(p => p.X), _dataPoints.Max(p => p.X));
    }
    
    /// <summary>
    /// Gets the minimum and maximum Y values in the series
    /// </summary>
    public (double min, double max) GetYRange()
    {
        if (_dataPoints.Count == 0) return (0, 1);
        return (_dataPoints.Min(p => p.Y), _dataPoints.Max(p => p.Y));
    }
    
    /// <summary>
    /// Renders this series to a BrailleCanvas with the specified coordinate transformation
    /// </summary>
    public void RenderToCanvas(BrailleCanvas canvas, double xMin, double xMax, double yMin, double yMax)
    {
        if (!_visible || _dataPoints.Count < 2) return;
        
        var points = _dataPoints.ToList();
        
        for (int i = 0; i < points.Count - 1; i++)
        {
            var p1 = points[i];
            var p2 = points[i + 1];
            
            // Convert data coordinates to canvas coordinates
            int x1 = (int)((p1.X - xMin) / (xMax - xMin) * (canvas.Width - 1));
            int y1 = (int)((yMax - p1.Y) / (yMax - yMin) * (canvas.Height - 1)); // Flip Y for screen coordinates
            int x2 = (int)((p2.X - xMin) / (xMax - xMin) * (canvas.Width - 1));
            int y2 = (int)((yMax - p2.Y) / (yMax - yMin) * (canvas.Height - 1));
            
            // Ensure coordinates are within bounds
            x1 = Math.Max(0, Math.Min(canvas.Width - 1, x1));
            y1 = Math.Max(0, Math.Min(canvas.Height - 1, y1));
            x2 = Math.Max(0, Math.Min(canvas.Width - 1, x2));
            y2 = Math.Max(0, Math.Min(canvas.Height - 1, y2));
            
            canvas.DrawLine(x1, y1, x2, y2);
        }
    }

    public void DrawSeries(GraphView graph, Rectangle drawBounds, RectangleF graphBounds)
    {
        var canvas = new BrailleCanvas(drawBounds.Width, drawBounds.Height);
        RenderToCanvas(canvas, xMin: 0, xMax: 100, yMin: 0, yMax: 100);
        canvas.Render(graph);
    }
}

/// <summary>
/// Extension methods to help create BrailleSeries from various data sources
/// </summary>
public static class BrailleSeriesExtensions
{
    /// <summary>
    /// Creates a BrailleSeries from numeric data arrays
    /// </summary>
    public static BrailleSeries CreateFromArrays(double[] xValues, double[] yValues, string name = "")
    {
        if (xValues.Length != yValues.Length)
            throw new ArgumentException("X and Y arrays must have the same length");
        
        var series = new BrailleSeries(name);
        for (int i = 0; i < xValues.Length; i++)
        {
            series.AddPoint(xValues[i], yValues[i]);
        }
        
        return series;
    }
    
    /// <summary>
    /// Creates a BrailleSeries from a mathematical function
    /// </summary>
    public static BrailleSeries CreateFromFunction(Func<double, double> function, 
                                                  double xMin, double xMax, 
                                                  int points = 100, 
                                                  string name = "")
    {
        var series = new BrailleSeries(name);
        
        for (int i = 0; i <= points; i++)
        {
            double x = xMin + (xMax - xMin) * i / points;
            double y = function(x);
            series.AddPoint(x, y);
        }
        
        return series;
    }
    
    /// <summary>
    /// Creates a sine wave series for demonstration purposes
    /// </summary>
    public static BrailleSeries CreateSineWave(double frequency = 1.0, double amplitude = 1.0, 
                                             double xMin = 0, double xMax = 10, 
                                             int points = 200, string name = "Sine Wave")
    {
        return CreateFromFunction(x => amplitude * Math.Sin(frequency * x), xMin, xMax, points, name);
    }
    
    /// <summary>
    /// Creates a complex wave series combining multiple frequencies
    /// </summary>
    public static BrailleSeries CreateComplexWave(double xMin = 0, double xMax = 10, 
                                                 int points = 200, string name = "Complex Wave")
    {
        return CreateFromFunction(x => 
            Math.Sin(x) + 
            Math.Sin(x * 3) * 0.3 + 
            Math.Sin(x * 7) * 0.1 + 
            Math.Sin(x * 15) * 0.05, 
            xMin, xMax, points, name);
    }
}
