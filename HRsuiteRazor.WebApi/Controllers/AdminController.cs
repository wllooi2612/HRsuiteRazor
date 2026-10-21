using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Data.Odbc;
using System.Collections.Generic;
using System.Text;
using Hrsuite.WebApi.Models;
using Hrsuite.WebApi.Helpers;

namespace Hrsuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AdminController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string GetMySqlConnectionString() =>
        _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";

    private string GetMyMySqlConnectionString() =>
        _configuration.GetConnectionString("ConnectionTMS") ?? "";

    private string GetEncryptionKey() =>
        _configuration["encryptionKey"] ?? "hrsuite";

    // --- EMPLOYEE LOOKUP ---
    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees()
    {
        var list = new List<EmployeeLookupModel>();

        try
        {
            using (var conn = new MySqlConnection(GetMySqlConnectionString()))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT DISTINCT emp_id, Name FROM aspnet_users WHERE emp_id IS NOT NULL AND emp_id <> '' ORDER BY emp_id";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new EmployeeLookupModel
                            {
                                EmpId = reader["emp_id"]?.ToString() ?? "",
                                Name = reader["Name"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Employee lookup error: {ex.Message}");
        }

        return Ok(list);
    }

    // --- USER MANAGEMENT ---
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var list = new List<AdminUserModel>();
        string connStr = GetMySqlConnectionString();

        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT ID, Name, Password, Remark, IsActive, Email, IsEmail, 
                               OTUserGroupID, emp_id, LeaveUserGroupID, AttnUserGroupID, 
                               PayslipUserGroupID, UserGroupID, superAdminLeave, 
                               ClaimUserGroupID, EAFORMGroupID, CoNo, IsAttn, password_payslip 
                        FROM aspnet_users 
                        ORDER BY ID";

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new AdminUserModel
                            {
                                ID = reader["ID"]?.ToString() ?? "",
                                Name = reader["Name"]?.ToString() ?? "",
                                Password = reader["Password"]?.ToString() ?? "",
                                Remark = reader["Remark"]?.ToString() ?? "",
                                IsActive = reader["IsActive"]?.ToString() ?? "",
                                Email = reader["Email"]?.ToString() ?? "",
                                IsEmail = reader["IsEmail"]?.ToString() ?? "",
                                OTUserGroupID = reader["OTUserGroupID"]?.ToString() ?? "",
                                EmpId = reader["emp_id"]?.ToString() ?? "",
                                LeaveUserGroupID = reader["LeaveUserGroupID"]?.ToString() ?? "",
                                AttnUserGroupID = reader["AttnUserGroupID"]?.ToString() ?? "",
                                PayslipUserGroupID = reader["PayslipUserGroupID"]?.ToString() ?? "",
                                UserGroupID = reader["UserGroupID"]?.ToString() ?? "",
                                SuperAdminLeave = reader["superAdminLeave"]?.ToString() ?? "",
                                ClaimUserGroupID = reader["ClaimUserGroupID"]?.ToString() ?? "",
                                EAFORMGroupID = reader["EAFORMGroupID"]?.ToString() ?? "",
                                CoNo = reader["CoNo"]?.ToString() ?? "",
                                IsAttn = reader["IsAttn"]?.ToString() ?? "",
                                PasswordPayslip = reader["password_payslip"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "SQL Server error.", Details = ex.Message });
        }
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] AdminUserModel user)
    {
        if (string.IsNullOrEmpty(user.ID) || string.IsNullOrEmpty(user.Name))
        {
            return BadRequest(new { Message = "ID and Name are required." });
        }

        string connStr = GetMySqlConnectionString();

        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                await conn.OpenAsync();

                // 1. Check if ID already exists
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM aspnet_users WHERE ID = @ID";
                    cmd.Parameters.AddWithValue("@ID", user.ID);
                    long count = Convert.ToInt64(await cmd.ExecuteScalarAsync() ?? 0);
                    if (count > 0)
                    {
                        return BadRequest(new { Message = "User ID already exists." });
                    }
                }

                // 2. Check if emp_id already used (if provided)
                if (!string.IsNullOrEmpty(user.EmpId))
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM aspnet_users WHERE emp_id = @EmpId";
                        cmd.Parameters.AddWithValue("@EmpId", user.EmpId);
                        long count = Convert.ToInt64(await cmd.ExecuteScalarAsync() ?? 0);
                        if (count > 0)
                        {
                            return BadRequest(new { Message = "Employee ID is already assigned to another user." });
                        }
                    }
                }

                // 3. Encrypt passwords
                string encKey = GetEncryptionKey();
                string encryptedPassword = CryptographyHelper.Encrypt(user.Password, encKey);
                string encryptedPayslipPassword = string.IsNullOrEmpty(user.PasswordPayslip) 
                    ? "" 
                    : CryptographyHelper.Encrypt(user.PasswordPayslip, encKey);

                // 4. Insert
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO aspnet_users (
                            ID, Name, Password, Remark, IsActive, Email, IsEmail, 
                            OTUserGroupID, emp_id, LeaveUserGroupID, AttnUserGroupID, 
                            PayslipUserGroupID, UserGroupID, superAdminLeave, 
                            ClaimUserGroupID, EAFORMGroupID, CoNo, IsAttn, password_payslip
                        ) VALUES (
                            @ID, @Name, @Password, @Remark, @IsActive, @Email, @IsEmail, 
                            @OTUserGroupID, @EmpId, @LeaveUserGroupID, @AttnUserGroupID, 
                            @PayslipUserGroupID, @UserGroupID, @SuperAdminLeave, 
                            @ClaimUserGroupID, @EAFORMGroupID, @CoNo, @IsAttn, @PasswordPayslip
                        )";

                    cmd.Parameters.AddWithValue("@ID", user.ID);
                    cmd.Parameters.AddWithValue("@Name", user.Name);
                    cmd.Parameters.AddWithValue("@Password", encryptedPassword);
                    cmd.Parameters.AddWithValue("@Remark", user.Remark ?? "");
                    cmd.Parameters.AddWithValue("@IsActive", user.IsActive ?? "Y");
                    cmd.Parameters.AddWithValue("@Email", user.Email ?? "");
                    cmd.Parameters.AddWithValue("@IsEmail", user.IsEmail ?? "N");
                    cmd.Parameters.AddWithValue("@OTUserGroupID", user.OTUserGroupID ?? "");
                    cmd.Parameters.AddWithValue("@EmpId", user.EmpId ?? "");
                    cmd.Parameters.AddWithValue("@LeaveUserGroupID", user.LeaveUserGroupID ?? "");
                    cmd.Parameters.AddWithValue("@AttnUserGroupID", user.AttnUserGroupID ?? "");
                    cmd.Parameters.AddWithValue("@PayslipUserGroupID", user.PayslipUserGroupID ?? "");
                    cmd.Parameters.AddWithValue("@UserGroupID", user.UserGroupID ?? "");
                    cmd.Parameters.AddWithValue("@SuperAdminLeave", user.SuperAdminLeave ?? "N");
                    cmd.Parameters.AddWithValue("@ClaimUserGroupID", user.ClaimUserGroupID ?? "");
                    cmd.Parameters.AddWithValue("@EAFORMGroupID", user.EAFORMGroupID ?? "");
                    cmd.Parameters.AddWithValue("@CoNo", user.CoNo ?? "");
                    cmd.Parameters.AddWithValue("@IsAttn", user.IsAttn ?? "N");
                    cmd.Parameters.AddWithValue("@PasswordPayslip", encryptedPayslipPassword);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await RecordEmailLogAsync("USERADD", "Y", user.Email, user.ID, user.EmpId);
            return Ok(new { Message = "User created successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "SQL Server error during insert.", Details = ex.Message });
        }
    }

    [JsonPropertyNullValue]
    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] AdminUserModel user)
    {
        string connStr = GetMySqlConnectionString();

        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                await conn.OpenAsync();

                // Check emp_id collision
                if (!string.IsNullOrEmpty(user.EmpId))
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM aspnet_users WHERE emp_id = @EmpId AND ID <> @ID";
                        cmd.Parameters.AddWithValue("@EmpId", user.EmpId);
                        cmd.Parameters.AddWithValue("@ID", id);
                        long count = Convert.ToInt64(await cmd.ExecuteScalarAsync() ?? 0);
                        if (count > 0)
                        {
                            return BadRequest(new { Message = "Employee ID is already assigned to another user." });
                        }
                    }
                }

                // Encrypt password if provided
                string encKey = GetEncryptionKey();
                string? encryptedPassword = null;
                if (!string.IsNullOrEmpty(user.Password))
                {
                    encryptedPassword = CryptographyHelper.Encrypt(user.Password, encKey);
                }

                string? encryptedPayslipPassword = null;
                if (!string.IsNullOrEmpty(user.PasswordPayslip))
                {
                    encryptedPayslipPassword = CryptographyHelper.Encrypt(user.PasswordPayslip, encKey);
                }

                using (var cmd = conn.CreateCommand())
                {
                    var queryBuilder = new StringBuilder(@"
                        UPDATE aspnet_users SET
                            Name = @Name,
                            Remark = @Remark,
                            IsActive = @IsActive,
                            Email = @Email,
                            IsEmail = @IsEmail,
                            emp_id = @EmpId,
                            OTUserGroupID = @OTUserGroupID,
                            LeaveUserGroupID = @LeaveUserGroupID,
                            AttnUserGroupID = @AttnUserGroupID,
                            PayslipUserGroupID = @PayslipUserGroupID,
                            UserGroupID = @UserGroupID,
                            superAdminLeave = @SuperAdminLeave,
                            ClaimUserGroupID = @ClaimUserGroupID,
                            EAFORMGroupID = @EAFORMGroupID,
                            CoNo = @CoNo,
                            IsAttn = @IsAttn");

                    if (encryptedPassword != null)
                    {
                        queryBuilder.Append(", Password = @Password");
                    }
                    if (encryptedPayslipPassword != null)
                    {
                        queryBuilder.Append(", password_payslip = @PasswordPayslip");
                    }

                    queryBuilder.Append(" WHERE ID = @ID");

                    cmd.CommandText = queryBuilder.ToString();
                    cmd.Parameters.AddWithValue("@ID", id);
                    cmd.Parameters.AddWithValue("@Name", user.Name);
                    cmd.Parameters.AddWithValue("@Remark", user.Remark ?? "");
                    cmd.Parameters.AddWithValue("@IsActive", user.IsActive ?? "Y");
                    cmd.Parameters.AddWithValue("@Email", user.Email ?? "");
                    cmd.Parameters.AddWithValue("@IsEmail", user.IsEmail ?? "N");
                    cmd.Parameters.AddWithValue("@OTUserGroupID", user.OTUserGroupID ?? "");
                    cmd.Parameters.AddWithValue("@EmpId", user.EmpId ?? "");
                    cmd.Parameters.AddWithValue("@LeaveUserGroupID", user.LeaveUserGroupID ?? "");
                    cmd.Parameters.AddWithValue("@AttnUserGroupID", user.AttnUserGroupID ?? "");
                    cmd.Parameters.AddWithValue("@PayslipUserGroupID", user.PayslipUserGroupID ?? "");
                    cmd.Parameters.AddWithValue("@UserGroupID", user.UserGroupID ?? "");
                    cmd.Parameters.AddWithValue("@SuperAdminLeave", user.SuperAdminLeave ?? "N");
                    cmd.Parameters.AddWithValue("@ClaimUserGroupID", user.ClaimUserGroupID ?? "");
                    cmd.Parameters.AddWithValue("@EAFORMGroupID", user.EAFORMGroupID ?? "");
                    cmd.Parameters.AddWithValue("@CoNo", user.CoNo ?? "");
                    cmd.Parameters.AddWithValue("@IsAttn", user.IsAttn ?? "N");

                    if (encryptedPassword != null)
                    {
                        cmd.Parameters.AddWithValue("@Password", encryptedPassword);
                    }
                    if (encryptedPayslipPassword != null)
                    {
                        cmd.Parameters.AddWithValue("@PasswordPayslip", encryptedPayslipPassword);
                    }

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await RecordEmailLogAsync("USERUPDATE", "Y", user.Email, user.ID, user.EmpId);
            return Ok(new { Message = "User updated successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "SQL Server error during update.", Details = ex.Message });
        }
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        string connStr = GetMySqlConnectionString();
        string email = "";
        string empId = "";

        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                await conn.OpenAsync();

                // Get details for logging
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Email, emp_id FROM aspnet_users WHERE ID = @ID";
                    cmd.Parameters.AddWithValue("@ID", id);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            email = reader["Email"]?.ToString() ?? "";
                            empId = reader["emp_id"]?.ToString() ?? "";
                        }
                    }
                }

                // Delete user
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM aspnet_users WHERE ID = @ID";
                    cmd.Parameters.AddWithValue("@ID", id);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await RecordEmailLogAsync("USERDEL", "Y", email, id, empId);
            return Ok(new { Message = "User deleted successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "SQL Server error during delete.", Details = ex.Message });
        }
    }

    [HttpPost("users/{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id)
    {
        string sqlConnStr = GetMySqlConnectionString();
        string mysqlConnStr = GetMyMySqlConnectionString();

        string empId = "";
        string tempPassword = "123456";

        try
        {
            using (var sqlConn = new MySqlConnection(sqlConnStr))
            {
                await sqlConn.OpenAsync();

                // 1. Get employee ID linked to this user (Reusing sqlConn)
                using (var cmd = sqlConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT emp_id FROM aspnet_users WHERE ID = @ID";
                    cmd.Parameters.AddWithValue("@ID", id);
                    var scalarVal = await cmd.ExecuteScalarAsync();
                    empId = scalarVal?.ToString() ?? "";
                }

                // 2. Fetch birthday & ic_new from MySQL to construct custom temp password
                if (!string.IsNullOrEmpty(empId))
                {
                    using (var conn = new OdbcConnection(mysqlConnStr))
                    {
                        await conn.OpenAsync();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT BIRTHDATE, ic_new FROM dw_emp WHERE Emp_ID = @EmpId";
                            cmd.Parameters.AddWithValue("@EmpId", empId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    var bdayObj = reader["BIRTHDATE"];
                                    string icNew = reader["ic_new"]?.ToString() ?? "";
                                    if (bdayObj != null && bdayObj != DBNull.Value)
                                    {
                                        DateTime bday = Convert.ToDateTime(bdayObj);
                                        string datePart = bday.ToString("yyMMdd");
                                        string icPart = icNew.Length >= 4 ? icNew.Substring(icNew.Length - 4) : icNew;
                                        tempPassword = datePart + icPart;
                                    }
                                }
                            }
                        }
                    }
                }

                // 3. Encrypt and save temp password
                string encKey = GetEncryptionKey();
                string encryptedPass = CryptographyHelper.Encrypt(tempPassword, encKey);

                // 4. Update password (Reusing sqlConn)
                using (var cmd = sqlConn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE aspnet_users SET Password = @Password WHERE ID = @ID";
                    cmd.Parameters.AddWithValue("@Password", encryptedPass);
                    cmd.Parameters.AddWithValue("@ID", id);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await RecordEmailLogAsync("RESETPWD", "Y", "", id, empId);
            return Ok(new { Message = "Password reset successfully.", TemporaryPassword = tempPassword });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error resetting password.", Details = ex.Message });
        }
    }

    // --- USER GROUP SETUP ---
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups()
    {
        var list = new List<UserGroupModel>();
        string connStr = GetMySqlConnectionString();

        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT UserGroupID, GroupID, UserGroup, Remark, ApprovalLevel FROM aspnet_usergroup ORDER BY UserGroupID";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new UserGroupModel
                            {
                                UserGroupID = reader["UserGroupID"]?.ToString() ?? "",
                                GroupID = reader["GroupID"]?.ToString() ?? "",
                                UserGroup = reader["UserGroup"]?.ToString() ?? "",
                                Remark = reader["Remark"]?.ToString() ?? "",
                                ApprovalLevel = reader["ApprovalLevel"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "SQL Server error querying groups.", Details = ex.Message });
        }
    }

    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies()
    {
        var list = new List<CompanyModel>();
        string connStr = GetMySqlConnectionString();

        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT DISTINCT CoNo, CompanyName FROM aspnet_general ORDER BY CoNo";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new CompanyModel
                            {
                                CoNo = reader["CoNo"]?.ToString() ?? "",
                                CompanyName = reader["CompanyName"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error querying companies.", Details = ex.Message });
        }
    }

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] UserGroupModel model)
    {
        if (string.IsNullOrEmpty(model.UserGroupID) || string.IsNullOrEmpty(model.GroupID))
        {
            return BadRequest(new { Message = "User Group ID and System Group ID are required." });
        }

        if (model.UserGroupID.Length > 12)
        {
            return BadRequest(new { Message = "User Group ID must be 12 characters or less." });
        }

        if (model.UserGroupID.Contains("-"))
        {
            return BadRequest(new { Message = "User Group ID cannot contain hyphens (-)." });
        }

        string connStr = GetMySqlConnectionString();

        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                await conn.OpenAsync();

                // Check duplicate UserGroupID + GroupID
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM aspnet_usergroup WHERE UserGroupID = @UserGroupID AND GroupID = @GroupID";
                    cmd.Parameters.AddWithValue("@UserGroupID", model.UserGroupID);
                    cmd.Parameters.AddWithValue("@GroupID", model.GroupID);
                    long count = Convert.ToInt64(await cmd.ExecuteScalarAsync() ?? 0);
                    if (count > 0)
                    {
                        return BadRequest(new { Message = "Group already exists." });
                    }
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO aspnet_usergroup (UserGroupID, GroupID, UserGroup, Remark, ApprovalLevel)
                        VALUES (@UserGroupID, @GroupID, @UserGroup, @Remark, @ApprovalLevel)";

                    cmd.Parameters.AddWithValue("@UserGroupID", model.UserGroupID);
                    cmd.Parameters.AddWithValue("@GroupID", model.GroupID);
                    cmd.Parameters.AddWithValue("@UserGroup", model.UserGroup ?? "");
                    cmd.Parameters.AddWithValue("@Remark", model.Remark ?? "");
                    cmd.Parameters.AddWithValue("@ApprovalLevel", model.ApprovalLevel ?? "N");

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            return Ok(new { Message = "User Group created successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "SQL Server error inserting group.", Details = ex.Message });
        }
    }

    [HttpPut("groups/{userGroupId}/{groupId}")]
    public async Task<IActionResult> UpdateGroup(string userGroupId, string groupId, [FromBody] UserGroupModel model)
    {
        string connStr = GetMySqlConnectionString();

        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        UPDATE aspnet_usergroup 
                        SET UserGroup = @UserGroup, Remark = @Remark, ApprovalLevel = @ApprovalLevel 
                        WHERE UserGroupID = @UserGroupID AND GroupID = @GroupID";

                    cmd.Parameters.AddWithValue("@UserGroupID", userGroupId);
                    cmd.Parameters.AddWithValue("@GroupID", groupId);
                    cmd.Parameters.AddWithValue("@UserGroup", model.UserGroup ?? "");
                    cmd.Parameters.AddWithValue("@Remark", model.Remark ?? "");
                    cmd.Parameters.AddWithValue("@ApprovalLevel", model.ApprovalLevel ?? "N");

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            return Ok(new { Message = "User Group updated successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "SQL Server error updating group.", Details = ex.Message });
        }
    }

    [HttpDelete("groups/{userGroupId}/{groupId}")]
    public async Task<IActionResult> DeleteGroup(string userGroupId, string groupId)
    {
        string connStr = GetMySqlConnectionString();

        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                await conn.OpenAsync();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Delete the usergroup
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = trans;
                            cmd.CommandText = "DELETE FROM aspnet_usergroup WHERE UserGroupID = @UserGroupID AND GroupID = @GroupID";
                            cmd.Parameters.AddWithValue("@UserGroupID", userGroupId);
                            cmd.Parameters.AddWithValue("@GroupID", groupId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // 2. Delete access rights
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = trans;
                            cmd.CommandText = "DELETE FROM aspnet_useraccessright WHERE UserID = @UserGroupID AND GroupID2 = @GroupID";
                            cmd.Parameters.AddWithValue("@UserGroupID", userGroupId);
                            cmd.Parameters.AddWithValue("@GroupID", groupId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // 3. Reset references in aspnet_users based on group type
                        string resetField = "";
                        switch (groupId.ToUpper())
                        {
                            case "LEAVE": resetField = "LeaveUserGroupID"; break;
                            case "OT": resetField = "OTUserGroupID"; break;
                            case "PAYSLIP": resetField = "PayslipUserGroupID"; break;
                            case "ATTENDANCE": resetField = "AttnUserGroupID"; break;
                            case "EAFORM": resetField = "EAFORMGroupID"; break;
                            case "CLAIM": resetField = "ClaimUserGroupID"; break;
                            case "EMPLOYEE": resetField = "EmpProfileCRUserGroupID"; break;
                            case "STAFF": resetField = "UserGroupID"; break;
                        }

                        if (!string.IsNullOrEmpty(resetField))
                        {
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = trans;
                                cmd.CommandText = $"UPDATE aspnet_users SET {resetField} = '' WHERE {resetField} = @UserGroupID";
                                cmd.Parameters.AddWithValue("@UserGroupID", userGroupId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        trans.Commit();
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
            return Ok(new { Message = "User Group deleted successfully and references reset." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "SQL Server error deleting group.", Details = ex.Message });
        }
    }

    // --- PERMISSION SETUP ---
    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions([FromQuery] string userGroupId, [FromQuery] string groupId)
    {
        if (string.IsNullOrEmpty(userGroupId) || string.IsNullOrEmpty(groupId))
        {
            return BadRequest(new { Message = "userGroupId and groupId are required parameters." });
        }

        string connStr = GetMySqlConnectionString();
        var masterPages = new List<PermissionPageModel>();

        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                await conn.OpenAsync();

                // 1. Fetch checkmarks first
                var selectedFormIds = new HashSet<int>();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT FormID FROM aspnet_useraccessright WHERE UserID = @UserGroupID AND GroupID2 = @GroupID AND AccessRight = 'Y'";
                    cmd.Parameters.AddWithValue("@UserGroupID", userGroupId);
                    cmd.Parameters.AddWithValue("@GroupID", groupId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            selectedFormIds.Add(Convert.ToInt32(reader["FormID"]));
                        }
                    }
                }

                // 2. Fetch master pages matching open groups
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT FormID, GroupID, Name 
                        FROM aspnet_masterfile 
                        WHERE GroupID IN (SELECT GroupID FROM aspnet_mastersystemgroup WHERE OpenGroup='Y') 
                          AND ShowPage = 'Y' AND IsDefault = 'N' AND IsParent = 'N'
                        ORDER BY GroupID, GroupCode, ItemCode";

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int formId = Convert.ToInt32(reader["FormID"]);
                            masterPages.Add(new PermissionPageModel
                            {
                                FormID = formId,
                                GroupID = reader["GroupID"]?.ToString() ?? "",
                                Name = reader["Name"]?.ToString() ?? "",
                                IsSelected = selectedFormIds.Contains(formId)
                            });
                        }
                    }
                }
            }
            return Ok(masterPages);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "SQL Server error querying permissions.", Details = ex.Message });
        }
    }

    [JsonPropertyNullValue]
    [HttpPut("permissions")]
    public async Task<IActionResult> SavePermissions([FromBody] SavePermissionsRequest request)
    {
        if (string.IsNullOrEmpty(request.UserGroupID) || string.IsNullOrEmpty(request.GroupID))
        {
            return BadRequest(new { Message = "UserGroupID and GroupID are required." });
        }

        string connStr = GetMySqlConnectionString();
        var selectedFormIds = new HashSet<int>(request.FormIDs);

        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                await conn.OpenAsync();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Delete existing access rights for this User Group ID + Group Code
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = trans;
                            cmd.CommandText = "DELETE FROM aspnet_useraccessright WHERE UserID = @UserID AND GroupID2 = @GroupID2";
                            cmd.Parameters.AddWithValue("@UserID", request.UserGroupID);
                            cmd.Parameters.AddWithValue("@GroupID2", request.GroupID);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // 2. Retrieve all pages from masterfile to insert mapped permissions
                        var allMasterPages = new List<dynamic>();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = trans;
                            cmd.CommandText = @"
                                SELECT FormID, GroupID, GroupCode, ItemCode, Name, ShowPage, 
                                       Link_Path, IconPath, IconName, IsParent, IsDefault, ParentSitemapID
                                FROM aspnet_masterfile";

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    allMasterPages.Add(new {
                                        FormID = Convert.ToInt32(reader["FormID"]),
                                        GroupID = reader["GroupID"]?.ToString() ?? "",
                                        GroupCode = Convert.ToInt32(reader["GroupCode"]),
                                        ItemCode = Convert.ToInt32(reader["ItemCode"]),
                                        Name = reader["Name"]?.ToString() ?? "",
                                        ShowPage = reader["ShowPage"]?.ToString() ?? "N",
                                        Link_Path = reader["Link_Path"]?.ToString() ?? "",
                                        IconPath = reader["IconPath"]?.ToString() ?? "",
                                        IconName = reader["IconName"]?.ToString() ?? "",
                                        IsParent = reader["IsParent"]?.ToString() ?? "N",
                                        IsDefault = reader["IsDefault"]?.ToString() ?? "N",
                                        ParentSitemapID = reader["ParentSitemapID"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["ParentSitemapID"])
                                    });
                                }
                            }
                        }

                        // 3. Separate standard pages, parents, and defaults
                        foreach (var page in allMasterPages)
                        {
                            // We process normal pages where ShowPage='Y' and IsDefault='N' and IsParent='N'
                            if (page.ShowPage == "Y" && page.IsDefault == "N" && page.IsParent == "N")
                            {
                                bool isChecked = selectedFormIds.Contains(page.FormID);
                                string right = isChecked ? "Y" : "N";

                                // Insert the main page right
                                await InsertAccessRightAsync(conn, trans, page, request.UserGroupID, request.GroupID, right);

                                // Query and insert its child pages (where ParentSitemapID = page.FormID and ShowPage='N')
                                foreach (var child in allMasterPages)
                                {
                                    if (child.ParentSitemapID == page.FormID && child.ShowPage == "N")
                                    {
                                        await InsertAccessRightAsync(conn, trans, child, request.UserGroupID, request.GroupID, right);
                                    }
                                }
                            }
                        }

                        // 4. Update parent pages (IsParent = 'Y') based on whether they have active child access rights (Step B N+1 eliminated)
                        var activeGroupCodes = new HashSet<int>();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = trans;
                            cmd.CommandText = @"SELECT DISTINCT GroupCode FROM aspnet_useraccessright
                                WHERE UserID = @UserID AND GroupID2 = @GroupID2 AND AccessRight = 'Y'";
                            cmd.Parameters.AddWithValue("@UserID", request.UserGroupID);
                            cmd.Parameters.AddWithValue("@GroupID2", request.GroupID);
                            using (var reader = await cmd.ExecuteReaderAsync())
                                while (await reader.ReadAsync())
                                    activeGroupCodes.Add(Convert.ToInt32(reader["GroupCode"]));
                        }

                        foreach (var parent in allMasterPages.Where(p => p.IsParent == "Y"))
                        {
                            bool hasActiveChildren = activeGroupCodes.Contains(parent.GroupCode);
                            await InsertAccessRightAsync(conn, trans, parent, request.UserGroupID, request.GroupID, hasActiveChildren ? "Y" : "N");
                        }

                        // 5. Default pages (IsDefault = 'Y') - always write with access right = 'Y'
                        foreach (var defPage in allMasterPages)
                        {
                            if (defPage.IsDefault == "Y")
                            {
                                await InsertAccessRightAsync(conn, trans, defPage, request.UserGroupID, request.GroupID, "Y");
                            }
                        }

                        trans.Commit();
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
            return Ok(new { Message = "Permissions saved successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "SQL Server error saving permissions.", Details = ex.Message });
        }
    }

    private async Task InsertAccessRightAsync(MySqlConnection conn, MySqlTransaction trans, dynamic page, string userGroupId, string groupId2, string right)
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = trans;
            cmd.CommandText = @"
                INSERT INTO aspnet_useraccessright (
                    FormID, UserID, AccessRight, GroupID, GroupCode, ItemCode, Name, 
                    ShowPage, Link_Path, IconShow, IconPath, IconName, sitemap, IsParent, GroupID2, IsDefault
                ) VALUES (
                    @FormID, @UserID, @AccessRight, @GroupID, @GroupCode, @ItemCode, @Name, 
                    @ShowPage, @Link_Path, @IconShow, @IconPath, @IconName, @sitemap, @IsParent, @GroupID2, @IsDefault
                )";

            cmd.Parameters.AddWithValue("@FormID", page.FormID);
            cmd.Parameters.AddWithValue("@UserID", userGroupId);
            cmd.Parameters.AddWithValue("@AccessRight", right);
            cmd.Parameters.AddWithValue("@GroupID", page.GroupID);
            cmd.Parameters.AddWithValue("@GroupCode", page.GroupCode);
            cmd.Parameters.AddWithValue("@ItemCode", page.ItemCode);
            cmd.Parameters.AddWithValue("@Name", page.Name);
            cmd.Parameters.AddWithValue("@ShowPage", page.ShowPage);
            cmd.Parameters.AddWithValue("@Link_Path", page.Link_Path);
            cmd.Parameters.AddWithValue("@IconShow", right);
            cmd.Parameters.AddWithValue("@IconPath", page.IconPath);
            cmd.Parameters.AddWithValue("@IconName", page.IconName);
            cmd.Parameters.AddWithValue("@sitemap", "0");
            cmd.Parameters.AddWithValue("@IsParent", page.IsParent);
            cmd.Parameters.AddWithValue("@GroupID2", groupId2);
            cmd.Parameters.AddWithValue("@IsDefault", page.IsDefault);

            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task RecordEmailLogAsync(string actType, string emailSend, string? emailId, string refId2, string? empId)
    {
        try
        {
            string sRefID = Guid.NewGuid().ToString("N").Substring(0, 32);
            string connStr = GetMySqlConnectionString();
            using (var conn = new MySqlConnection(connStr))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO EmailLog (RefID, EMailSend, TimeSend, Type, EmailID, RefID2, TotalTry, AssEmailSend, SplitNow, emp_id) 
                        VALUES (@RefID, @EMailSend, NOW(), @Type, @EmailID, @RefID2, 1, 'N', 'N', @EmpId)";
                    cmd.Parameters.AddWithValue("@RefID", sRefID);
                    cmd.Parameters.AddWithValue("@EMailSend", emailSend);
                    cmd.Parameters.AddWithValue("@Type", actType);
                    cmd.Parameters.AddWithValue("@EmailID", emailId ?? "");
                    cmd.Parameters.AddWithValue("@RefID2", refId2 ?? "");
                    cmd.Parameters.AddWithValue("@EmpId", empId ?? "");
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write EmailLog: {ex.Message}");
        }
    }
}

// Simple attribute helper to prevent JSON null conversion on updates
[System.AttributeUsage(System.AttributeTargets.Method)]
public class JsonPropertyNullValueAttribute : System.Attribute { }
