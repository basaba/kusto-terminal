using System;
using System.Collections.Generic;

namespace KustoTerminal.Core.Models;

/// <summary>
/// Represents a single series in a timechart
/// </summary>
public class ChartSeries
{
    public string Name { get; set; } = string.Empty;
    public List<double?> Values { get; set; } = new();
}

/// <summary>
/// Structured data extracted from a query result for timechart rendering
/// </summary>
public class TimeChartData
{
    /// <summary>
    /// Name of the datetime column used as X axis
    /// </summary>
    public string TimeColumnName { get; set; } = string.Empty;

    /// <summary>
    /// The time points for the X axis
    /// </summary>
    public List<DateTime> TimePoints { get; set; } = new();

    /// <summary>
    /// The data series (Y axis values)
    /// </summary>
    public List<ChartSeries> Series { get; set; } = new();

    /// <summary>
    /// Minimum Y value across all series
    /// </summary>
    public double MinValue
    {
        get
        {
            double min = double.MaxValue;
            foreach (var series in Series)
            {
                foreach (var v in series.Values)
                {
                    if (v.HasValue && v.Value < min)
                        min = v.Value;
                }
            }
            return min == double.MaxValue ? 0 : min;
        }
    }

    /// <summary>
    /// Maximum Y value across all series
    /// </summary>
    public double MaxValue
    {
        get
        {
            double max = double.MinValue;
            foreach (var series in Series)
            {
                foreach (var v in series.Values)
                {
                    if (v.HasValue && v.Value > max)
                        max = v.Value;
                }
            }
            return max == double.MinValue ? 0 : max;
        }
    }

    /// <summary>
    /// Whether this chart data is valid for rendering
    /// </summary>
    public bool IsValid => TimePoints.Count > 0 && Series.Count > 0;
}