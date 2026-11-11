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
    public decimal TotalDays { get; set; } = 1;
    public string Leave_Status { get; set; } = string.Empty;
    public string Leave_Reason { get; set; } = string.Empty;
    public string AppLevel { get; set; } = string.Empty;

    // Fields matching eLeaveApprove.aspx & eLeavePostApprove.aspx
    public string File1 { get; set; } = string.Empty;
    public string File2 { get; set; } = string.Empty;
    public string File3 { get; set; } = string.Empty;
    public string TimeIn { get; set; } = "0000";
    public string s1 { get; set; } = string.Empty;
    public string TimeOut { get; set; } = "0000";
    public string s2 { get; set; } = string.Empty;
    public string Dept { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string ShiftGrp { get; set; } = string.Empty;
    public string Type { get; set; } = "FULL";

    // Additional fields matching GeteLeaveApprove_ObjLeaveApproval
    public bool CheckBoxOpen { get; set; } = true;
    public bool CheckBoxOpen2 { get; set; } = true;
    public string Remarks { get; set; } = string.Empty;
    public string LeaveName { get; set; } = string.Empty;
    public string SuperName { get; set; } = string.Empty;
    public string App1 { get; set; } = string.Empty;
    public string App1Pass { get; set; } = string.Empty;
    public string App2 { get; set; } = string.Empty;
    public string App2Pass { get; set; } = string.Empty;
    public string App3 { get; set; } = string.Empty;
    public string App3Pass { get; set; } = string.Empty;
    public string App4 { get; set; } = string.Empty;
    public string App4Pass { get; set; } = string.Empty;
    public DateTime? CreateDate { get; set; }
}
