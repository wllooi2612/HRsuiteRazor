namespace Hrsuite.WebApi.Models;

public class User
{
    public string ID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string IsActive { get; set; } = string.Empty;
    public string UserGroupID { get; set; } = string.Empty;
    public string LeaveUserGroupID { get; set; } = string.Empty;
    public string SuperAdminLeave { get; set; } = string.Empty;

    public bool IsAdmin => ID.Equals("admin", StringComparison.OrdinalIgnoreCase) || 
                           LeaveUserGroupID.Equals("LV ADMIN", StringComparison.OrdinalIgnoreCase) || 
                           LeaveUserGroupID.Equals("super admin", StringComparison.OrdinalIgnoreCase) || 
                           SuperAdminLeave.Equals("Y", StringComparison.OrdinalIgnoreCase);
}

