namespace Hrsuite.WebApi.Models;

public class AdminUserModel
{
    public string ID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public string IsActive { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string IsEmail { get; set; } = string.Empty;
    public string OTUserGroupID { get; set; } = string.Empty;
    public string EmpId { get; set; } = string.Empty; // emp_id
    public string LeaveUserGroupID { get; set; } = string.Empty;
    public string AttnUserGroupID { get; set; } = string.Empty;
    public string PayslipUserGroupID { get; set; } = string.Empty;
    public string UserGroupID { get; set; } = string.Empty;
    public string SuperAdminLeave { get; set; } = string.Empty; // superAdminLeave
    public string ClaimUserGroupID { get; set; } = string.Empty;
    public string EAFORMGroupID { get; set; } = string.Empty;
    public string CoNo { get; set; } = string.Empty;
    public string IsAttn { get; set; } = string.Empty;
    public string PasswordPayslip { get; set; } = string.Empty; // password_payslip
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
    public string GroupID { get; set; } = string.Empty; // E.g. LEAVE, OT
    public List<int> FormIDs { get; set; } = new();
}
