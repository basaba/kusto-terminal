using System;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Terminal.Gui;
using Terminal.Gui.ViewBase;

namespace KustoTerminal.UI.Services
{
    public static class HtmlClipboardService
    {
        /// <summary>
        /// Copies a DataTable to the clipboard in HTML format with platform-specific handling
        /// </summary>
        public static bool CopyTableAsHtml(DataTable dataTable)
        {
            if (dataTable == null || dataTable.Columns.Count == 0)
            {
                return false;
            }

            try
            {
                var htmlContent = GenerateHtml(dataTable);
                var plainTextContent = GeneratePlainText(dataTable);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return SetClipboardWindows(htmlContent, plainTextContent);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return SetClipboardMacOS(htmlContent, plainTextContent);
                }
                else
                {
                    // For Linux or other platforms, fall back to plain text only
                    try
                    {
                        // Use TextCopy for cross-platform clipboard on Linux
                        System.IO.File.WriteAllText("/tmp/clipboard.txt", plainTextContent);
                        var processInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "/bin/sh",
                            Arguments = "-c \"cat /tmp/clipboard.txt | xclip -selection clipboard 2>/dev/null || cat /tmp/clipboard.txt | xsel --clipboard 2>/dev/null || cat /tmp/clipboard.txt\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var process = System.Diagnostics.Process.Start(processInfo);
                        process?.WaitForExit();
                    }
                    catch
                    {
                        // Silently fail on Linux if clipboard tools not available
                    }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Generates HTML formatted table from DataTable
        /// </summary>
        private static string GenerateHtml(DataTable dataTable)
        {
            var html = new StringBuilder();

            // HTML with inline CSS for styling
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='UTF-8'>");
            html.AppendLine("<style>");
            html.AppendLine("table { border-collapse: collapse; font-family: 'Segoe UI', Arial, sans-serif; font-size: 12px; }");
            html.AppendLine("th { background-color: #f0f0f0; font-weight: bold; text-align: left; padding: 8px; border: 1px solid #ddd; }");
            html.AppendLine("td { padding: 8px; border: 1px solid #ddd; }");
            html.AppendLine("tr:nth-child(even) { background-color: #f9f9f9; }");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("<table>");

            // Header row
            html.AppendLine("<thead><tr>");
            foreach (DataColumn column in dataTable.Columns)
            {
                html.Append("<th>");
                html.Append(EscapeHtml(column.ColumnName));
                html.AppendLine("</th>");
            }
            html.AppendLine("</tr></thead>");

            // Data rows
            html.AppendLine("<tbody>");
            foreach (DataRow row in dataTable.Rows)
            {
                html.AppendLine("<tr>");
                foreach (var item in row.ItemArray)
                {
                    html.Append("<td>");
                    var cellValue = item == null || item == DBNull.Value ? "" : item.ToString() ?? "";
                    html.Append(EscapeHtml(cellValue));
                    html.AppendLine("</td>");
                }
                html.AppendLine("</tr>");
            }
            html.AppendLine("</tbody>");

            html.AppendLine("</table>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        /// <summary>
        /// Generates plain text table from DataTable (tab-separated)
        /// </summary>
        private static string GeneratePlainText(DataTable dataTable)
        {
            var text = new StringBuilder();

            // Headers
            var headers = dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName);
            text.AppendLine(string.Join("\t", headers));

            // Rows
            foreach (DataRow row in dataTable.Rows)
            {
                var fields = row.ItemArray.Select(field => 
                    field == null || field == DBNull.Value ? "" : field.ToString() ?? "");
                text.AppendLine(string.Join("\t", fields));
            }

            return text.ToString();
        }

        /// <summary>
        /// Escapes special characters for HTML format
        /// </summary>
        private static string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;")
                .Replace("\n", "<br/>")
                .Replace("\t", "&nbsp;&nbsp;&nbsp;&nbsp;");
        }

        /// <summary>
        /// Sets clipboard on Windows with both HTML and plain text formats
        /// </summary>
        private static bool SetClipboardWindows(string htmlContent, string plainTextContent)
        {
#if WINDOWS
            try
            {
                // Use System.Windows.Forms.Clipboard for Windows
                var dataObject = new System.Windows.Forms.DataObject();
                dataObject.SetData(System.Windows.Forms.DataFormats.Html, htmlContent);
                dataObject.SetData(System.Windows.Forms.DataFormats.Text, plainTextContent);
                dataObject.SetData(System.Windows.Forms.DataFormats.UnicodeText, plainTextContent);
                
                System.Windows.Forms.Clipboard.SetDataObject(dataObject, true);
                return true;
            }
            catch
            {
                // Fallback: just return false as Windows Forms clipboard failed
                return false;
            }
#else
            // System.Windows.Forms not available on this platform
            return false;
#endif
        }

        /// <summary>
        /// Sets clipboard on macOS with both HTML and plain text formats
        /// </summary>
        private static bool SetClipboardMacOS(string htmlContent, string plainTextContent)
        {
            try
            {
                // Create temporary HTML file
                var htmlTempFile = System.IO.Path.GetTempFileName() + ".html";
                System.IO.File.WriteAllText(htmlTempFile, htmlContent);

                try
                {
                    // Use textutil to convert HTML to RTF and copy to pasteboard
                    // This allows pasting into rich text applications with formatting
                    var processInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/bin/sh",
                        Arguments = $"-c \"cat '{htmlTempFile}' | /usr/bin/textutil -stdin -format html -convert rtf -stdout | /usr/bin/pbcopy -Prefer rtf\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = System.Diagnostics.Process.Start(processInfo);
                    process?.WaitForExit();

                    if (process?.ExitCode == 0)
                    {
                        return true;
                    }
                }
                finally
                {
                    // Clean up temp file
                    try { System.IO.File.Delete(htmlTempFile); } catch { }
                }
            }
            catch
            {
                // Ignore errors and try fallback
            }

            // Fallback to plain text
            return TryPbcopyPlainText(plainTextContent);
        }

        /// <summary>
        /// Fallback to plain text using pbcopy
        /// </summary>
        private static bool TryPbcopyPlainText(string plainTextContent)
        {
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/pbcopy",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    process.StandardInput.Write(plainTextContent);
                    process.StandardInput.Close();
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }
}
