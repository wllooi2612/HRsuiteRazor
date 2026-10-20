using System;

namespace Hrsuite.WebApi.Models;

public class CalendarEventModel
{
    public string Date { get; set; } = string.Empty; // "yyyy-MM-dd"
    public string Type { get; set; } = string.Empty; // "PENDING", "APPROVE", "REJECT", "HOLIDAY", "WORKDAY", "RESTDAY", "OFFDAY"
    public string Description { get; set; } = string.Empty;
}

public class DashboardSummaryModel
{
    public int PendingLeavesCount { get; set; }
    public string WelcomeMessage { get; set; } = string.Empty;
}
