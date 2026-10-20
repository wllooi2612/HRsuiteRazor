using System;

namespace Hrsuite.WebApi.Models;

public class LeaveRequest
{
    public string LeaveNo { get; set; } = string.Empty;
    public string Emp_ID { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string Leave_Type { get; set; } = string.Empty;
    public DateTime Leave_Date_From { get; set; }
    public DateTime Leave_Date_To { get; set; }
    public string Leave_Status { get; set; } = string.Empty;
    public string Leave_Reason { get; set; } = string.Empty;
    public string AppLevel { get; set; } = string.Empty;
}
