using System.Data;
using System.Text;
using KustoTerminal.Core.Models;
using KustoTerminal.Language.Models;
using KustoTerminal.Language.Services;
using Kusto.Language.Editor;

namespace KustoTerminal.UI.SyntaxHighlighting;

public class HtmlSyntaxHighlighter
{
    private readonly LanguageService _languageService;

    public HtmlSyntaxHighlighter(LanguageService languageService)
    {
        _languageService = languageService;
    }
    
    public string GenerateHtmlWithQuery(string queryText, DataTable dataTable, KustoConnection connection)
    {
        var result = GenerateHtmlWithQueryImpl(queryText, dataTable, connection);
#if WINDOWS
        result = ConvertToClipboardHtmlFormat(result);
#endif
        return result;
    }

    private string GetHighlightedHtml(string queryText, KustoConnection connection)
    {
        var textModel = new TextModel(queryText);
        var clusterName = connection.GetClusterNameFromUrl();
        var classificationResult = _languageService.GetClassifications(textModel, clusterName, connection.Database);
        
        return ApplyClassificationsToHtml(queryText, classificationResult.Classifications);
    }

    /// <summary>
    /// Applies syntax highlighting classifications to generate HTML with colored spans
    /// </summary>
    private static string ApplyClassificationsToHtml(string queryText, Classification[] classifications)
    {
        var html = new StringBuilder();
        var currentPos = 0;

        // Sort classifications by start position to process them in order
        var sortedClassifications = classifications.OrderBy(c => c.Start).ToArray();

        foreach (var classification in sortedClassifications)
        {
            // Add any unclassified text before this classification
            if (classification.Start > currentPos)
            {
                var unclassifiedText = queryText.Substring(currentPos, classification.Start - currentPos);
                html.Append(EscapeHtml(unclassifiedText));
            }

            // Add the classified text with color styling
            var classifiedText = queryText.Substring(classification.Start, classification.Length);
            var colorHex = GetHtmlColorForClassification(classification.Kind);
            
            if (!string.IsNullOrEmpty(colorHex))
            {
                html.Append($"<span style=\"color: {colorHex};\">");
                html.Append(EscapeHtml(classifiedText));
                html.Append("</span>");
            }
            else
            {
                html.Append(EscapeHtml(classifiedText));
            }

            currentPos = classification.Start + classification.Length;
        }

        // Add any remaining unclassified text
        if (currentPos < queryText.Length)
        {
            var remainingText = queryText.Substring(currentPos);
            html.Append(EscapeHtml(remainingText));
        }

        return html.ToString();
    }

    /// <summary>
    /// Gets HTML hex color for classification using the centralized mapper
    /// </summary>
    private static string GetHtmlColorForClassification(ClassificationKind kind)
    {
        return ClassificationColorMapper.GetHtmlColorForClassification(kind);
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
    
    private string GenerateHtmlWithQueryImpl(string queryText, DataTable dataTable, KustoConnection connection)
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
        html.AppendLine(".cluster-info { padding: 8px; margin-bottom: 15px; border-left: 3px solid #0078d4; font-size: 10px; }");
        html.AppendLine(".cluster-label { font-weight: bold; color: #0078d4; }");
        html.AppendLine("table { border-collapse: collapse; font-family: 'Segoe UI', Arial, sans-serif; font-size: 12px; }");
        html.AppendLine("th { background-color: #f0f0f0; font-weight: bold; text-align: left; padding: 8px; border: 1px solid #ddd; }");
        html.AppendLine("td { padding: 8px; border: 1px solid #ddd; }");
        html.AppendLine("tr:nth-child(even) { background-color: #f9f9f9; }");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        
        // Cluster information section
        var clusterUrl = connection.ClusterUri;
        if (!string.IsNullOrEmpty(clusterUrl))
        {
            html.AppendLine("<div class='cluster-info'>");
            html.Append(EscapeHtml(clusterUrl));
            html.AppendLine("</div>");
        }
        
        // Query section
        if (!string.IsNullOrEmpty(queryText))
        {
            html.AppendLine("<div class='query'>");
            if (connection != null)
            {
                // Apply syntax highlighting to the query
                html.Append(GenerateHighlightedQueryHtml(queryText, connection));
            }
            else
            {
                // Fallback to plain text without highlighting
                html.Append(EscapeHtml(queryText));
            }
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
    /// Generates syntax-highlighted HTML for a Kusto query
    /// </summary>
    private string GenerateHighlightedQueryHtml(string queryText, KustoConnection connection)
    {
        try
        {
            return GetHighlightedHtml(queryText, connection);
        }
        catch
        {
            // Fallback to plain text if syntax highlighting fails
            return EscapeHtml(queryText);
        }
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
}
