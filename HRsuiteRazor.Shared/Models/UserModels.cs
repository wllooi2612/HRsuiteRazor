namespace HRsuiteRazor.Shared.Models;

public class User
{
    public string ID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string IsActive { get; set; } = "Y";
    public string UserGroupID { get; set; } = string.Empty;
    public string LeaveUserGroupID { get; set; } = string.Empty;
    public string SuperAdminLeave { get; set; } = string.Empty;

    public bool IsAdmin => ID.Equals("admin", StringComparison.OrdinalIgnoreCase) || 
                           LeaveUserGroupID.Equals("LV ADMIN", StringComparison.OrdinalIgnoreCase) || 
                           LeaveUserGroupID.Equals("super admin", StringComparison.OrdinalIgnoreCase) || 
                           SuperAdminLeave.Equals("Y", StringComparison.OrdinalIgnoreCase);
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public User? User { get; set; }
    public string Token { get; set; } = string.Empty;
}

public class AdminUserModel
{
    public string ID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public string IsActive { get; set; } = "Y";
    public string Email { get; set; } = string.Empty;
    public string IsEmail { get; set; } = string.Empty;
    public string OTUserGroupID { get; set; } = string.Empty;
    public string EmpId { get; set; } = string.Empty;
    public string LeaveUserGroupID { get; set; } = string.Empty;
    public string AttnUserGroupID { get; set; } = string.Empty;
    public string PayslipUserGroupID { get; set; } = string.Empty;
    public string UserGroupID { get; set; } = string.Empty;
    public string SuperAdminLeave { get; set; } = string.Empty;
    public string ClaimUserGroupID { get; set; } = string.Empty;
    public string EAFORMGroupID { get; set; } = string.Empty;
    public string CoNo { get; set; } = string.Empty;
    public string IsAttn { get; set; } = string.Empty;
    public string PasswordPayslip { get; set; } = string.Empty;
}

public class UserGroupModel
{
    public string UserGroupID { get; set; } = string.Empty;
    public string GroupID { get; set; } = string.Empty;
    public string UserGroup { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public string ApprovalLevel { get; set; } = string.Empty;
}

public class CompanyModel
{
    public string CoNo { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
}

public class EmployeeLookupModel
{
    public string EmpId { get; set; } = string.Empty;
    public string ID { get => EmpId; set => EmpId = value; }
    public string Name { get; set; } = string.Empty;
}

public class PermissionPageModel
{
    public int FormID { get; set; }
    public string GroupID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

public class SavePermissionsRequest
{
    public string UserGroupID { get; set; } = string.Empty;
    public string GroupID { get; set; } = string.Empty;
    public List<int> FormIDs { get; set; } = new();
}

public class QdeEmployeeModel
{
    public string EmpId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
    public string DivisionId { get; set; } = string.Empty;
    public string DeptId { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime? Birthdate { get; set; }
    public string IcNew { get; set; } = string.Empty;
    public bool IsInMyHrsuite { get; set; }
}

public class BatchCreateUsersRequest
{
    public List<string> EmpIds { get; set; } = new();
    public string PasswordMode { get; set; } = "DOB_IC"; // "DOB_IC" or "DEFAULT"
    public string? DefaultPassword { get; set; } = "123456";
}

public class BatchCreateUsersResult
{
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> CreatedIds { get; set; } = new();
    public List<string> SkippedIds { get; set; } = new();
    public List<string> Messages { get; set; } = new();
}

public class LeaveUserAccessItem
{
    public string FormId { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public string Extra { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

public class LeaveUserAccessData
{
    public string UserId { get; set; } = string.Empty;
    public List<LeaveUserAccessItem> Departments { get; set; } = new();
    public List<LeaveUserAccessItem> Sections { get; set; } = new();
    public List<LeaveUserAccessItem> Categories { get; set; } = new();
    public List<LeaveUserAccessItem> Grades { get; set; } = new();
    public List<LeaveUserAccessItem> Supervisors { get; set; } = new();
    public List<LeaveUserAccessItem> JobTitles { get; set; } = new();
    public List<LeaveUserAccessItem> Factories { get; set; } = new();
    public List<LeaveUserAccessItem> Groups { get; set; } = new();
    public List<LeaveUserAccessItem> Emails { get; set; } = new();
}

public class SaveLeaveUserAccessRequest
{
    public string UserId { get; set; } = string.Empty;
    public List<SelectedAccessItem> SelectedItems { get; set; } = new();
}

public class SelectedAccessItem
{
    public string FormId { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
}

