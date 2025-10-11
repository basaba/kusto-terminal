using System;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Terminal.Gui;
using Terminal.Gui.ViewBase;

namespace KustoTerminal.UI.Services
{
    public static class ClipboardService
    {
        public static void SetClipboardWithHtml(string htmlContent)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    SetClipboardWithHtmlWindows(htmlContent);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    SetClipboardWithHtmlMacOS(htmlContent);
                }
                else
                {
                    // For Linux or other platforms, fall back to plain text only
                    //return CopyTextToClipboard(plainTextContent);
                }
            }
            catch
            { }
        }
        
        public static string GenerateHtmlWithQuery(string queryText = null, DataTable dataTable = null)
        {
            var html = new StringBuilder();

            // HTML with inline CSS for styling
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='UTF-8'>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; }");
            html.AppendLine(".query { background-color: #f5f5f5; padding: 12px; margin-bottom: 20px; border-left: 4px solid #0078d4; font-family: 'Consolas', 'Monaco', monospace; font-size: 13px; white-space: pre-wrap; }");
            html.AppendLine(".query-label { font-weight: bold; margin-bottom: 8px; color: #0078d4; }");
            html.AppendLine("table { border-collapse: collapse; font-family: 'Segoe UI', Arial, sans-serif; font-size: 12px; }");
            html.AppendLine("th { background-color: #f0f0f0; font-weight: bold; text-align: left; padding: 8px; border: 1px solid #ddd; }");
            html.AppendLine("td { padding: 8px; border: 1px solid #ddd; }");
            html.AppendLine("tr:nth-child(even) { background-color: #f9f9f9; }");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            // Query section
            if (!string.IsNullOrEmpty(queryText))
            {
                html.AppendLine("<div class='query-label'>Query:</div>");
                html.AppendLine("<div class='query'>");
                html.Append(EscapeHtml(queryText));
                html.AppendLine("</div>"); 
            }
            
            if (dataTable != null)
            {
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
            }
            // Table section

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
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
        private static bool SetClipboardWithHtmlWindows(string htmlContent)
        {
#if WINDOWS
            try
            {
                // Convert HTML to Windows Clipboard HTML Format (CF_HTML)
                var htmlClipboardFormat = ConvertToClipboardHtmlFormat(htmlContent);
                
                // Use System.Windows.Forms.Clipboard for Windows
                var dataObject = new System.Windows.Forms.DataObject();
                dataObject.SetData(System.Windows.Forms.DataFormats.Html, htmlClipboardFormat);
                
                var thread = new Thread(() =>
                {
                    System.Windows.Forms.Clipboard.SetDataObject(dataObject, true);
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
                return true;
            }
            catch (Exception e)
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
        /// Converts HTML content to Windows Clipboard HTML Format (CF_HTML)
        /// </summary>
        private static string ConvertToClipboardHtmlFormat(string htmlContent)
        {
            // Windows clipboard HTML format requires specific headers with byte offsets
            // Format: Version:0.9\r\nStartHTML:xxxxxxxxxx\r\nEndHTML:xxxxxxxxxx\r\nStartFragment:xxxxxxxxxx\r\nEndFragment:xxxxxxxxxx\r\n
            
            const string header = "Version:0.9\r\n";
            const string startHtmlMarker = "StartHTML:";
            const string endHtmlMarker = "EndHTML:";
            const string startFragmentMarker = "StartFragment:";
            const string endFragmentMarker = "EndFragment:";
            
            const string htmlPrefix = "<!DOCTYPE html>\r\n<html>\r\n<body>\r\n<!--StartFragment-->";
            const string htmlSuffix = "<!--EndFragment-->\r\n</body>\r\n</html>";
            
            // Build the complete HTML with fragment markers
            var sb = new StringBuilder();
            
            // Reserve space for headers (10 digits each for offsets)
            var headerTemplate = header +
                               startHtmlMarker + "0000000000\r\n" +
                               endHtmlMarker + "0000000000\r\n" +
                               startFragmentMarker + "0000000000\r\n" +
                               endFragmentMarker + "0000000000\r\n";
            
            var headerLength = Encoding.UTF8.GetByteCount(headerTemplate);
            var prefixLength = Encoding.UTF8.GetByteCount(htmlPrefix);
            var contentLength = Encoding.UTF8.GetByteCount(htmlContent);
            var suffixLength = Encoding.UTF8.GetByteCount(htmlSuffix);
            
            var startHtml = headerLength;
            var endHtml = headerLength + prefixLength + contentLength + suffixLength;
            var startFragment = headerLength + prefixLength;
            var endFragment = startFragment + contentLength;
            
            // Build the final clipboard format
            sb.Append(header);
            sb.Append(startHtmlMarker).Append(startHtml.ToString("D10")).Append("\r\n");
            sb.Append(endHtmlMarker).Append(endHtml.ToString("D10")).Append("\r\n");
            sb.Append(startFragmentMarker).Append(startFragment.ToString("D10")).Append("\r\n");
            sb.Append(endFragmentMarker).Append(endFragment.ToString("D10")).Append("\r\n");
            sb.Append(htmlPrefix);
            sb.Append(htmlContent);
            sb.Append(htmlSuffix);
            
            return sb.ToString();
        }

        /// <summary>
        /// Sets clipboard on macOS with both HTML and plain text formats
        /// </summary>
        private static bool SetClipboardWithHtmlMacOS(string htmlContent)
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

            return false;
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
