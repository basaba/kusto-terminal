using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using KustoTerminal.Core.Models;

namespace KustoTerminal.Core.Services;

/// <summary>
/// Detects timechart render hints in KQL queries and extracts chart data from query results
/// </summary>
public static class TimeChartDetector
{
    private static readonly Regex RenderTimechartRegex = new(
        @"\|\s*render\s+timechart\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Checks whether a KQL query contains a "| render timechart" instruction
    /// </summary>
    public static bool IsTimechartQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        return RenderTimechartRegex.IsMatch(query);
    }

    /// <summary>
    /// Extracts timechart data from a DataTable result.
    /// Expects the first datetime column as the X axis,
    /// numeric columns as Y series, and an optional string column as series splitter.
    /// </summary>
    public static TimeChartData? ExtractChartData(DataTable? dataTable)
    {
        if (dataTable == null || dataTable.Rows.Count == 0 || dataTable.Columns.Count < 2)
            return null;

        // Find the datetime column (typically the first one)
        DataColumn? timeColumn = null;
        var numericColumns = new List<DataColumn>();
        DataColumn? seriesSplitColumn = null;

        foreach (DataColumn col in dataTable.Columns)
        {
            if (timeColumn == null && IsDateTimeColumn(col, dataTable))
            {
                timeColumn = col;
            }
            else if (IsNumericColumn(col))
            {
                numericColumns.Add(col);
            }
            else if (seriesSplitColumn == null && IsStringColumn(col))
            {
                seriesSplitColumn = col;
            }
        }

        if (timeColumn == null || numericColumns.Count == 0)
            return null;

        // If there's a series split column, pivot the data
        if (seriesSplitColumn != null)
        {
            return ExtractPivotedChartData(dataTable, timeColumn, numericColumns, seriesSplitColumn);
        }

        // Simple case: each numeric column is a series
        return ExtractSimpleChartData(dataTable, timeColumn, numericColumns);
    }

    private static TimeChartData ExtractSimpleChartData(
        DataTable dataTable, DataColumn timeColumn, List<DataColumn> numericColumns)
    {
        var chartData = new TimeChartData
        {
            TimeColumnName = timeColumn.ColumnName
        };

        // Initialize series
        foreach (var col in numericColumns)
        {
            chartData.Series.Add(new ChartSeries { Name = col.ColumnName });
        }

        // Sort rows by time
        var sortedRows = dataTable.AsEnumerable()
            .OrderBy(r => GetDateTimeValue(r, timeColumn))
            .ToList();

        foreach (var row in sortedRows)
        {
            var timeValue = GetDateTimeValue(row, timeColumn);
            if (timeValue == null)
                continue;

            chartData.TimePoints.Add(timeValue.Value);

            for (int i = 0; i < numericColumns.Count; i++)
            {
                var numValue = GetNumericValue(row, numericColumns[i]);
                chartData.Series[i].Values.Add(numValue);
            }
        }

        return chartData.IsValid ? chartData : null!;
    }

    private static TimeChartData ExtractPivotedChartData(
        DataTable dataTable, DataColumn timeColumn, List<DataColumn> numericColumns, DataColumn seriesSplitColumn)
    {
        var chartData = new TimeChartData
        {
            TimeColumnName = timeColumn.ColumnName
        };

        // Group by time, then by series key
        var grouped = dataTable.AsEnumerable()
            .GroupBy(r => GetDateTimeValue(r, timeColumn))
            .Where(g => g.Key != null)
            .OrderBy(g => g.Key)
            .ToList();

        // Discover all unique series names
        var seriesNames = dataTable.AsEnumerable()
            .Select(r => r[seriesSplitColumn]?.ToString() ?? "(null)")
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        // For each numeric column + series split value, create a named series
        // If there's only one numeric column, use the split value as series name
        // Otherwise, use "NumCol - SplitValue"
        var seriesMap = new Dictionary<string, ChartSeries>();

        foreach (var seriesName in seriesNames)
        {
            foreach (var numCol in numericColumns)
            {
                var key = numericColumns.Count == 1
                    ? seriesName
                    : $"{numCol.ColumnName} - {seriesName}";

                var series = new ChartSeries { Name = key };
                seriesMap[GetSeriesKey(numCol.ColumnName, seriesName)] = series;
                chartData.Series.Add(series);
            }
        }

        // Fill data
        foreach (var timeGroup in grouped)
        {
            chartData.TimePoints.Add(timeGroup.Key!.Value);

            // Build a lookup for this time point: seriesKey -> value
            var valuesAtTime = new Dictionary<string, double?>();
            foreach (var row in timeGroup)
            {
                var splitValue = row[seriesSplitColumn]?.ToString() ?? "(null)";
                foreach (var numCol in numericColumns)
                {
                    var key = GetSeriesKey(numCol.ColumnName, splitValue);
                    valuesAtTime[key] = GetNumericValue(row, numCol);
                }
            }

            // Add values in series order
            foreach (var seriesName in seriesNames)
            {
                foreach (var numCol in numericColumns)
                {
                    var key = GetSeriesKey(numCol.ColumnName, seriesName);
                    var series = seriesMap[key];
                    series.Values.Add(valuesAtTime.GetValueOrDefault(key));
                }
            }
        }

        return chartData.IsValid ? chartData : null!;
    }

    private static string GetSeriesKey(string numColName, string seriesName)
        => $"{numColName}||{seriesName}";

    private static bool IsDateTimeColumn(DataColumn col, DataTable table)
    {
        if (col.DataType == typeof(DateTime) || col.DataType == typeof(DateTimeOffset))
            return true;

        // Sometimes datetime comes as string; try to detect by parsing a sample
        if (col.DataType == typeof(string) || col.DataType == typeof(object))
        {
            int parsed = 0;
            int sampled = 0;
            foreach (DataRow row in table.Rows)
            {
                if (sampled >= 5) break;
                var val = row[col]?.ToString();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    sampled++;
                    if (DateTime.TryParse(val, out _))
                        parsed++;
                }
            }
            return sampled > 0 && parsed == sampled;
        }

        return false;
    }

    private static bool IsNumericColumn(DataColumn col)
    {
        return col.DataType == typeof(int)
            || col.DataType == typeof(long)
            || col.DataType == typeof(float)
            || col.DataType == typeof(double)
            || col.DataType == typeof(decimal)
            || col.DataType == typeof(short)
            || col.DataType == typeof(byte)
            || col.DataType == typeof(uint)
            || col.DataType == typeof(ulong)
            || col.DataType == typeof(ushort);
    }

    private static bool IsStringColumn(DataColumn col)
    {
        return col.DataType == typeof(string);
    }

    private static DateTime? GetDateTimeValue(DataRow row, DataColumn col)
    {
        var val = row[col];
        if (val == null || val == DBNull.Value)
            return null;

        if (val is DateTime dt) return dt;
        if (val is DateTimeOffset dto) return dto.UtcDateTime;
        if (DateTime.TryParse(val.ToString(), out var parsed)) return parsed;

        return null;
    }

    private static double? GetNumericValue(DataRow row, DataColumn col)
    {
        var val = row[col];
        if (val == null || val == DBNull.Value)
            return null;

        try
        {
            return Convert.ToDouble(val);
        }
        catch
        {
            return null;
        }
    }
}