namespace HRsuiteRazor.Shared.Models;

public class LeaveType
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IndirectLeaveId { get; set; } = string.Empty;
    public decimal Entitle { get; set; }
    public decimal Taken { get; set; }
    public decimal Pending { get; set; }
    public decimal Balance { get; set; }
    public decimal YearTot { get; set; }
    public decimal CarryForward { get; set; }
    public decimal Adjust { get; set; }
    public decimal ExceedBf { get; set; }
    public decimal Exceed { get; set; }

    public string? EffectiveDate { get; set; }
    public string? ExpiryDate { get; set; }
    public bool IsReplacement { get; set; }
    public List<LeaveType> SubLeaves { get; set; } = new();
}

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
}

public class LeaveApplyModel
{
    public string EmpId { get; set; } = string.Empty;
    public string LeaveCode { get; set; } = string.Empty;
    public DateTime DateFrom { get; set; } = DateTime.Today;
    public DateTime DateTo { get; set; } = DateTime.Today;
    public decimal Days { get; set; } = 1.0m;
    public string Reason { get; set; } = string.Empty;
    public string DayType { get; set; } = "FULL"; // FULL, FIRST_HALF, SECOND_HALF
    public string? FileAttachmentBase64 { get; set; }
    public string? FileName { get; set; }
}

public class ApproveActionModel
{
    public string Action { get; set; } = "APPROVE"; // APPROVE or REJECT
    public string LeaveNo { get; set; } = string.Empty;
    public string EmpId { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public string SupervisorId { get; set; } = string.Empty;
}
