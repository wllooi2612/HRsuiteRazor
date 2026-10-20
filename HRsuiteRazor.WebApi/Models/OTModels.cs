using System;

namespace Hrsuite.WebApi.Models;

public class OTType
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Multiplier { get; set; } = 1.0;
}

public class OTRecord
{
    public string OTNo { get; set; } = string.Empty;
    public string Emp_ID { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public DateTime OT_Date { get; set; }
    public string OT_ID { get; set; } = string.Empty;
    public string OT_Name { get; set; } = string.Empty;
    public string OT_Hour { get; set; } = string.Empty;
    public string OT_Start { get; set; } = string.Empty;
    public string OT_End { get; set; } = string.Empty;
    public string S1 { get; set; } = string.Empty;
    public string S2 { get; set; } = string.Empty;
    public string OT_Break { get; set; } = string.Empty;
    public string OT_Status { get; set; } = string.Empty;
    public string OT_Reason { get; set; } = string.Empty;
    public string OT_Remarks { get; set; } = string.Empty;
    public string AppLevel { get; set; } = string.Empty;
    public DateTime? CreatedDate { get; set; }
}

public class OTApplyModel
{
    public string EmpId { get; set; } = string.Empty;
    public string OTCode { get; set; } = string.Empty;
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string StartTime { get; set; } = "18:00";
    public string EndTime { get; set; } = "20:00";
    public string S1 { get; set; } = string.Empty;
    public string S2 { get; set; } = string.Empty;
    public string BreakHours { get; set; } = "0";
    public string TotalHours { get; set; } = "2.0";
    public string Reason { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
}

public class OTApproveModel
{
    public string OTNo { get; set; } = string.Empty;
    public string EmpId { get; set; } = string.Empty;
    public DateTime OTDate { get; set; }
    public string Action { get; set; } = "APPROVE"; // APPROVE or REJECT
    public string Remarks { get; set; } = string.Empty;
}
