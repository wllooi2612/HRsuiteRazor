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

public class LeaveApprovalLevelRow
{
    public string ID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    
    // Group Level
    public string App1 { get; set; } = string.Empty;
    public string AppName1 { get; set; } = string.Empty;
    public string App2 { get; set; } = string.Empty;
    public string AppName2 { get; set; } = string.Empty;
    public string App3 { get; set; } = string.Empty;
    public string AppName3 { get; set; } = string.Empty;
    public string App4 { get; set; } = string.Empty;
    public string AppName4 { get; set; } = string.Empty;
    public int AppLevel { get; set; } = 1;
    public string AppLevelName => AppLevel switch
    {
        1 => "ALL",
        2 => "One Of Them",
        3 => "1st/2nd with 3rd compulsory",
        4 => "1st/2nd + 3rd/4th next",
        _ => "ALL"
    };

    // Super Admin & Reminders
    public string SuperAdminLeave { get; set; } = "N";
    public string LeaveRemindFollowGeneral { get; set; } = "Y";
    public string LeaveEmailRemind { get; set; } = "N";
    public string LeaveRemindTimeIndividual { get; set; } = "00:00:00";

    // Individual Level
    public string UseIndividual { get; set; } = "N";
    public string App1_Individual { get; set; } = string.Empty;
    public string AppName1_Individual { get; set; } = string.Empty;
    public string App2_Individual { get; set; } = string.Empty;
    public string AppName2_Individual { get; set; } = string.Empty;
    public string App3_Individual { get; set; } = string.Empty;
    public string AppName3_Individual { get; set; } = string.Empty;
    public string App4_Individual { get; set; } = string.Empty;
    public string AppName4_Individual { get; set; } = string.Empty;
    public int AppLevel_Individual { get; set; } = 1;
    public string AppLevelName_Individual => AppLevel_Individual switch
    {
        1 => "ALL",
        2 => "One Of Them",
        3 => "Two Of Them (1 compulsory)",
        _ => "ALL"
    };
}

public class UpdateLeaveApprovalLevelRequest
{
    public string ID { get; set; } = string.Empty;
    public string App1 { get; set; } = string.Empty;
    public string App2 { get; set; } = string.Empty;
    public string App3 { get; set; } = string.Empty;
    public string App4 { get; set; } = string.Empty;
    public int AppLevel { get; set; } = 1;

    public string SuperAdminLeave { get; set; } = "N";
    public string LeaveRemindFollowGeneral { get; set; } = "Y";
    public string LeaveEmailRemind { get; set; } = "N";
    public string LeaveRemindTimeIndividual { get; set; } = "00:00:00";

    public string UseIndividual { get; set; } = "N";
    public string App1_Individual { get; set; } = string.Empty;
    public string App2_Individual { get; set; } = string.Empty;
    public string App3_Individual { get; set; } = string.Empty;
    public string App4_Individual { get; set; } = string.Empty;
    public int AppLevel_Individual { get; set; } = 1;
}

public class LeaveUserGroupOption
{
    public string UserGroupID { get; set; } = string.Empty;
    public string UserGroup { get; set; } = string.Empty;
    public string DisplayText => string.IsNullOrWhiteSpace(UserGroup) ? UserGroupID : $"{UserGroupID} - {UserGroup}";
}

public class LeaveMaintenanceRow
{
    public string UserGroupID { get; set; } = string.Empty;
    public string Leave_ID { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Display { get; set; } = "N"; // 'Y' / 'N'
    public string BalCount { get; set; } = "N"; // 'Y' / 'N'
    public string IndirectLeaveID { get; set; } = string.Empty;
    public string LeaveFirstYearNotAllow { get; set; } = "N"; // 'Y' / 'N'
    public string StopHalfDay { get; set; } = "N"; // 'Y' / 'N'
    public string FollowEsystem { get; set; } = "N"; // 'Y' / 'N'
    public string CompulsoryAttach { get; set; } = "N"; // 'Y' / 'N'

    // Days
    public string Monday { get; set; } = "N";
    public string Tuesday { get; set; } = "N";
    public string Wednesday { get; set; } = "N";
    public string Thursday { get; set; } = "N";
    public string Friday { get; set; } = "N";
    public string Saturday { get; set; } = "N";
    public string Sunday { get; set; } = "N";
    public string Restday { get; set; } = "N"; // Holiday
    public string IncludeDay { get; set; } = string.Empty; // formatted string: Mon, Tues, Wed, etc.

    // Special Approval Setting by Leave ID
    public string SpecialApproval { get; set; } = "N"; // 'Y' / 'N'
    public string App1 { get; set; } = string.Empty;
    public string App1Name { get; set; } = string.Empty;
    public string App2 { get; set; } = string.Empty;
    public string App2Name { get; set; } = string.Empty;
    public string App3 { get; set; } = string.Empty;
    public string App3Name { get; set; } = string.Empty;
    public string AppNo { get; set; } = "0";
    public int? AppLevel { get; set; } = 1;
    public string AppLevelName => AppLevel switch
    {
        1 => "ALL",
        2 => "One Of Them",
        3 => "two Of Them with one compulsory approval",
        _ => "ALL"
    };
}

public class UpdateLeaveMaintenanceRequest
{
    public string UserGroupID { get; set; } = string.Empty;
    public string Leave_ID { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Display { get; set; } = "N";
    public string BalCount { get; set; } = "N";
    public string IndirectLeaveID { get; set; } = string.Empty;
    public string LeaveFirstYearNotAllow { get; set; } = "N";
    public string StopHalfDay { get; set; } = "N";
    public string FollowEsystem { get; set; } = "N";
    public string CompulsoryAttach { get; set; } = "N";

    // Days
    public string Monday { get; set; } = "N";
    public string Tuesday { get; set; } = "N";
    public string Wednesday { get; set; } = "N";
    public string Thursday { get; set; } = "N";
    public string Friday { get; set; } = "N";
    public string Saturday { get; set; } = "N";
    public string Sunday { get; set; } = "N";
    public string Restday { get; set; } = "N";

    // Special Approval Setting
    public string SpecialApproval { get; set; } = "N";
    public string App1 { get; set; } = string.Empty;
    public string App2 { get; set; } = string.Empty;
    public string App3 { get; set; } = string.Empty;
    public int? AppLevel { get; set; } = 1;
}

public class LeaveMaintenanceData
{
    public List<LeaveUserGroupOption> UserGroups { get; set; } = new();
    public List<LeaveMaintenanceRow> Rows { get; set; } = new();
    public List<EmployeeLookupModel> Users { get; set; } = new();
    public List<LeaveTypeLookup> AvailableLeaveTypes { get; set; } = new();
}

public class LeaveTypeLookup
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
