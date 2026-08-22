using System;
using System.Collections.Generic;
using System.Data;

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
        public bool IsCached { get; set; }
        public DateTime? CachedAt { get; set; }
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
        public string ClusterUri { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
        public bool IsCommand { get; set; }
        public long DurationTicks { get; set; }
        public string? ClientRequestId { get; set; }
        public string ResultFileName { get; set; } = string.Empty;
    }
}