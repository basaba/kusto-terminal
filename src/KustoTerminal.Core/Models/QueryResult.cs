using System;
using System.Collections.Generic;
using System.Data;
using KustoTerminal.Language.Models;

namespace KustoTerminal.Core.Models
{
    public class QueryResult
    {
        public bool IsSuccess { get; set; }
        public string Query { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
        public string? ErrorMessage { get; set; }
        public DataTable? Data { get; set; }
        public string? ClientRequestId { get; set; }
        public RenderInfo? RenderInfo { get; set; }
        public int RowCount => Data?.Rows.Count ?? 0;
        public int ColumnCount => Data?.Columns.Count ?? 0;
        
        public static QueryResult Success(string query, DataTable data, TimeSpan duration, string? clientRequestId = null)
        {
            return new QueryResult
            {
                IsSuccess = true,
                Query = query,
                Data = data,
                Duration = duration,
                ClientRequestId = clientRequestId
            };
        }
        
        public static QueryResult Success(string query, DataTable data, TimeSpan duration, RenderInfo? renderInfo, string? clientRequestId = null)
        {
            return new QueryResult
            {
                IsSuccess = true,
                Query = query,
                Data = data,
                Duration = duration,
                RenderInfo = renderInfo,
                ClientRequestId = clientRequestId
            };
        }
        
        public static QueryResult Error(string query, string errorMessage, TimeSpan duration, string? clientRequestId = null)
        {
            return new QueryResult
            {
                IsSuccess = false,
                Query = query,
                ErrorMessage = errorMessage,
                Duration = duration,
                ClientRequestId = clientRequestId
            };
        }
    }

    public class QueryHistory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Query { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
        public bool IsSuccessful { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
