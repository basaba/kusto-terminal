using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace KustoTerminal.UI.Services;

/// <summary>
/// Builds a KQL <c>where</c> pipe step from a set of selected cells in a <see cref="DataTable"/>.
/// Cells are addressed as <see cref="Point"/> values where <c>X</c> is the column index and
/// <c>Y</c> is the row index, matching <c>Terminal.Gui.Views.TableView.GetAllSelectedCells()</c>.
/// </summary>
public static class KustoFilterBuilder
{
    /// <summary>
    /// Returns a <c>where ...</c> clause (no leading pipe) derived from the selected cells,
    /// or <c>null</c> when nothing meaningful can be derived.
    /// Shapes produced:
    ///   - Single column, N rows  -> <c>where Col in (v1, v2, ...)</c> (with <c>or isnull(Col)</c> when needed)
    ///   - Single row,    N cols  -> <c>where C1 == v1 and C2 == v2</c>
    ///   - N rows,        N cols  -> <c>where (C1==v1 and C2==v2) or (C1==v3 and C2==v4)</c>
    /// </summary>
    public static string? BuildWhereClause(DataTable table, IReadOnlyList<Point> cells)
    {
        if (table == null || cells == null || cells.Count == 0)
            return null;

        // De-duplicate (col,row) pairs and clip to table bounds.
        var safeCells = cells
            .Where(p => p.Y >= 0 && p.Y < table.Rows.Count && p.X >= 0 && p.X < table.Columns.Count)
            .Distinct()
            .ToList();

        if (safeCells.Count == 0)
            return null;

        var distinctCols = safeCells.Select(p => p.X).Distinct().ToList();

        // Single-column case: use `in (...)` for a compact predicate.
        if (distinctCols.Count == 1)
        {
            var col = table.Columns[distinctCols[0]];
            var values = safeCells
                .Select(p => (object?)table.Rows[p.Y][p.X])
                .ToList();
            return BuildInClause(col, values);
        }

        // Group by row → per-row conjunction over the selected columns of that row.
        var rowPredicates = safeCells
            .GroupBy(p => p.Y)
            .Select(g =>
            {
                var parts = g
                    .OrderBy(p => p.X)
                    .Select(p => BuildCellEquality(table.Columns[p.X], table.Rows[p.Y][p.X]))
                    .ToList();
                return parts.Count == 1 ? parts[0] : "(" + string.Join(" and ", parts) + ")";
            })
            .Distinct()
            .ToList();

        if (rowPredicates.Count == 0)
            return null;

        if (rowPredicates.Count == 1)
            return "where " + rowPredicates[0].Trim('(', ')');

        return "where " + string.Join("\n   or ", rowPredicates);
    }

    private static string BuildInClause(DataColumn col, IList<object?> rawValues)
    {
        var colExpr = FormatColumnName(col.ColumnName);
        var hasNull = rawValues.Any(IsNull);
        var nonNull = rawValues
            .Where(v => !IsNull(v))
            .Select(v => FormatLiteral(col, v!))
            .Distinct()
            .ToList();

        string body;
        if (nonNull.Count == 0)
            return $"where isnull({colExpr})";

        if (nonNull.Count == 1)
            body = $"{colExpr} == {nonNull[0]}";
        else
            body = $"{colExpr} in ({string.Join(", ", nonNull)})";

        if (hasNull)
            body += $" or isnull({colExpr})";

        return "where " + body;
    }

    private static string BuildCellEquality(DataColumn col, object? value)
    {
        var colExpr = FormatColumnName(col.ColumnName);
        if (IsNull(value))
            return $"isnull({colExpr})";
        return $"{colExpr} == {FormatLiteral(col, value!)}";
    }

    private static bool IsNull(object? value) => value is null || value is DBNull;

    private static string FormatColumnName(string name)
    {
        if (!string.IsNullOrEmpty(name) && Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$"))
            return name;
        return "[\"" + EscapeStringLiteral(name ?? string.Empty) + "\"]";
    }

    private static string FormatLiteral(DataColumn col, object value)
    {
        var t = col.DataType;

        if (t == typeof(string))
            return "\"" + EscapeStringLiteral((string)value) + "\"";

        if (t == typeof(bool))
            return ((bool)value) ? "true" : "false";

        if (t == typeof(byte) || t == typeof(sbyte)
            || t == typeof(short) || t == typeof(ushort)
            || t == typeof(int) || t == typeof(uint)
            || t == typeof(long) || t == typeof(ulong))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0";
        }

        if (t == typeof(float))
            return ((float)value).ToString("R", CultureInfo.InvariantCulture);

        if (t == typeof(double))
            return ((double)value).ToString("R", CultureInfo.InvariantCulture);

        if (t == typeof(decimal))
            return "decimal(" + ((decimal)value).ToString(CultureInfo.InvariantCulture) + ")";

        if (t == typeof(DateTime))
        {
            var dt = ((DateTime)value).ToUniversalTime();
            return "datetime(" + dt.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture) + ")";
        }

        if (t == typeof(DateTimeOffset))
        {
            var dt = ((DateTimeOffset)value).UtcDateTime;
            return "datetime(" + dt.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture) + ")";
        }

        if (t == typeof(TimeSpan))
            return "timespan(" + XmlConvert.ToString((TimeSpan)value) + ")";

        if (t == typeof(Guid))
            return "guid(" + value + ")";

        // dynamic / object / fallback: treat as opaque string literal.
        var s = value.ToString() ?? string.Empty;
        return "\"" + EscapeStringLiteral(s) + "\"";
    }

    private static string EscapeStringLiteral(string s)
    {
        // Kusto string escapes inside "..." match common C-style: \\, \", \n, \r, \t.
        var sb = new System.Text.StringBuilder(s.Length + 2);
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"':  sb.Append("\\\""); break;
                case '\n': sb.Append("\\n");  break;
                case '\r': sb.Append("\\r");  break;
                case '\t': sb.Append("\\t");  break;
                default:   sb.Append(ch);     break;
            }
        }
        return sb.ToString();
    }
}
