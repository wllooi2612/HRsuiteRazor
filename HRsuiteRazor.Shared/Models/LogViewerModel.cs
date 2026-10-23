using System;
using System.Collections.Generic;

namespace HRsuiteRazor.Shared.Models;

public class LogEntryModel
{
    public int Id { get; set; }
    public DateTime? Date { get; set; }
    public string Thread { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Logger { get; set; } = string.Empty;
    public string IP { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string UserID { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Exception { get; set; } = string.Empty;
}

public class LogQueryRequest
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string Level { get; set; } = "ALL";
    public string? SearchText { get; set; }
    public string? UserID { get; set; }
    public int PageIndex { get; set; } = 1; // 1-based
    public int PageSize { get; set; } = 50;
    public string SortBy { get; set; } = "Date";
    public bool SortDesc { get; set; } = true;
}

public class LogQueryResult
{
    public List<LogEntryModel> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public Dictionary<string, int> LevelCounts { get; set; } = new();
}
