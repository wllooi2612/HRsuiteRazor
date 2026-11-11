using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Data.Odbc;
using System.Collections.Generic;
using System.Text;
using Hrsuite.WebApi.Models;
using LeaveUserAccessItem = HRsuiteRazor.Shared.Models.LeaveUserAccessItem;
using LeaveUserAccessData = HRsuiteRazor.Shared.Models.LeaveUserAccessData;
using SaveLeaveUserAccessRequest = HRsuiteRazor.Shared.Models.SaveLeaveUserAccessRequest;
using SelectedAccessItem = HRsuiteRazor.Shared.Models.SelectedAccessItem;
using EmployeeLookupModel = HRsuiteRazor.Shared.Models.EmployeeLookupModel;
using LeaveApprovalLevelRow = HRsuiteRazor.Shared.Models.LeaveApprovalLevelRow;
using UpdateLeaveApprovalLevelRequest = HRsuiteRazor.Shared.Models.UpdateLeaveApprovalLevelRequest;
using LeaveUserGroupOption = HRsuiteRazor.Shared.Models.LeaveUserGroupOption;
using LeaveMaintenanceRow = HRsuiteRazor.Shared.Models.LeaveMaintenanceRow;
using UpdateLeaveMaintenanceRequest = HRsuiteRazor.Shared.Models.UpdateLeaveMaintenanceRequest;
using LeaveMaintenanceData = HRsuiteRazor.Shared.Models.LeaveMaintenanceData;
using LeaveTypeLookup = HRsuiteRazor.Shared.Models.LeaveTypeLookup;
using LeaveDocRuleRow = HRsuiteRazor.Shared.Models.LeaveDocRuleRow;
using SaveLeaveDocRuleRequest = HRsuiteRazor.Shared.Models.SaveLeaveDocRuleRequest;
using DeleteLeaveDocRulesRequest = HRsuiteRazor.Shared.Models.DeleteLeaveDocRulesRequest;

namespace Hrsuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaveController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public LeaveController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("types")]
    public IActionResult GetLeaveTypes([FromQuery] string? userId)
    {
        string mysqlConnStr = _configuration.GetConnectionString("ConnectionTMS") ?? "";
        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        var list = new List<LeaveType>();
        
        try
        {
            // 1. Fetch MySQL dw_leave lookup first
            var mysqlLeaves = new Dictionary<string, (string Name, string Cdlvepay)>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var conn = new OdbcConnection(mysqlConnStr))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT Leave_ID, Name, cdlvepay FROM dw_leave";
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string code = reader["Leave_ID"]?.ToString()?.Trim() ?? "";
                                string name = reader["Name"]?.ToString()?.Trim() ?? "";
                                string cdlvepay = reader["cdlvepay"]?.ToString()?.Trim() ?? "";
                                if (!string.IsNullOrEmpty(code))
                                {
                                    mysqlLeaves[code] = (name, cdlvepay);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback: try querying via MySQL connection if ODBC is unavailable (e.g. Linux / Railway)
                try
                {
                    string tmsMySql = GetTmsMySqlConnectionString();
                    using var conn = new MySqlConnection(tmsMySql);
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT Leave_ID, Name, cdlvepay FROM dw_leave";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string code = reader["Leave_ID"]?.ToString()?.Trim() ?? "";
                        string name = reader["Name"]?.ToString()?.Trim() ?? "";
                        string cdlvepay = reader["cdlvepay"]?.ToString()?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(code))
                        {
                            mysqlLeaves[code] = (name, cdlvepay);
                        }
                    }
                }
                catch { }
            }

            if (mysqlLeaves.Count == 0)
            {
                mysqlLeaves["AL"] = ("Annual Leave", "AL");
                mysqlLeaves["MC"] = ("Medical Leave", "MC");
                mysqlLeaves["EL"] = ("Emergency Leave", "EL");
                mysqlLeaves["HL"] = ("Hospitalisation Leave", "HL");
                mysqlLeaves["RL"] = ("Replacement Leave", "RL");
                mysqlLeaves["UL"] = ("Unpaid Leave", "UL");
                mysqlLeaves["ML"] = ("Maternity Leave", "ML");
                mysqlLeaves["PL"] = ("Paternity Leave", "PL");
                mysqlLeaves["CL"] = ("Compassionate Leave", "CL");
                mysqlLeaves["MR"] = ("Marriage Leave", "MR");
            }

            // If userId is not provided, return the default list from MySQL with 0 balances
            if (string.IsNullOrEmpty(userId))
            {
                foreach (var kvp in mysqlLeaves)
                {
                    list.Add(new LeaveType
                    {
                        Code = kvp.Key,
                        Name = kvp.Value.Name,
                        Balance = 0,
                        Entitle = 0,
                        Taken = 0,
                        Pending = 0
                    });
                }
                return Ok(list);
            }

            // 2. Fetch User Group ID from SQL Server
            string groupId = "";
            string replacementLeaveId = "";
            using (var conn = new MySqlConnection(sqlConnStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT LeaveUserGroupID FROM aspnet_users WHERE ID = @ID";
                    cmd.Parameters.AddWithValue("@ID", userId);
                    groupId = cmd.ExecuteScalar()?.ToString() ?? "";
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT ReplacementLeave_ID FROM aspnet_general LIMIT 1";
                    replacementLeaveId = cmd.ExecuteScalar()?.ToString()?.Trim() ?? "";
                }
            }

            if (string.IsNullOrEmpty(groupId))
            {
                // Fallback if user not found or group not assigned
                foreach (var kvp in mysqlLeaves)
                {
                    list.Add(new LeaveType
                    {
                        Code = kvp.Key,
                        Name = kvp.Value.Name
                    });
                }
                return Ok(list);
            }

            // 3. Query allowed leave IDs from SQL Server aspnet_leaveid
            var allowedLeaves = new List<(string LeaveID, string Display, string BalCount, string IndirectLeaveID)>();
            using (var conn = new MySqlConnection(sqlConnStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT LEAVE_ID, Display, BalCount, IndirectLeaveID FROM aspnet_leaveid WHERE UserGroupID = @GroupID AND Display = 'Y'";
                    cmd.Parameters.AddWithValue("@GroupID", groupId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            allowedLeaves.Add((
                                reader["LEAVE_ID"]?.ToString()?.Trim() ?? "",
                                reader["Display"]?.ToString()?.Trim() ?? "",
                                reader["BalCount"]?.ToString()?.Trim() ?? "",
                                reader["IndirectLeaveID"]?.ToString()?.Trim() ?? ""
                            ));
                        }
                    }
                }
            }

            // 4. For each allowed leave, calculate balances
            using (var connSql = new MySqlConnection(sqlConnStr))
            {
                connSql.Open();
                OdbcConnection? connMy = null;
                try
                {
                    connMy = new OdbcConnection(mysqlConnStr);
                    connMy.Open();
                }
                catch
                {
                    connMy = null;
                }

                try
                {
                    foreach (var item in allowedLeaves)
                    {
                        if (!mysqlLeaves.TryGetValue(item.LeaveID, out var mw))
                            continue;

                        string cdlvepay = mw.Cdlvepay;
                        string leaveName = mw.Name;

                        decimal balance = 0;
                        decimal taken = 0;
                        decimal entitle = 0;
                        decimal yeartot = 0;
                        decimal b_forward = 0;
                        decimal adjust = 0;
                        decimal exceed = 0;
                        decimal exdbfmth = 0;
                        decimal expadj = 0;

                        // Fetch MySQL entitle info
                        if (connMy != null && !string.IsNullOrEmpty(cdlvepay))
                        {
                            try
                            {
                                using (var cmd = connMy.CreateCommand())
                                {
                                    cmd.CommandText = "SELECT balance, taken, entitle, yeartot, b_forward, Adjust, exceed, exdbfmth, EXPADJ FROM entitle WHERE employeeid = ? AND leaveid = ?";
                                    cmd.Parameters.Add(new OdbcParameter("employeeid", userId));
                                    cmd.Parameters.Add(new OdbcParameter("leaveid", cdlvepay));
                                    using (var reader = cmd.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            balance = reader["balance"] != DBNull.Value ? Convert.ToDecimal(reader["balance"]) : 0;
                                            taken = reader["taken"] != DBNull.Value ? Convert.ToDecimal(reader["taken"]) : 0;
                                            entitle = reader["entitle"] != DBNull.Value ? Convert.ToDecimal(reader["entitle"]) : 0;
                                            yeartot = reader["yeartot"] != DBNull.Value ? Convert.ToDecimal(reader["yeartot"]) : 0;
                                            b_forward = reader["b_forward"] != DBNull.Value ? Convert.ToDecimal(reader["b_forward"]) : 0;
                                            adjust = reader["Adjust"] != DBNull.Value ? Convert.ToDecimal(reader["Adjust"]) : 0;
                                            exceed = reader["exceed"] != DBNull.Value ? Convert.ToDecimal(reader["exceed"]) : 0;
                                            exdbfmth = reader["exdbfmth"] != DBNull.Value ? Convert.ToDecimal(reader["exdbfmth"]) : 0;
                                            expadj = reader["EXPADJ"] != DBNull.Value ? Convert.ToDecimal(reader["EXPADJ"]) : 0;
                                        }
                                    }
                                }
                            }
                            catch { }
                        }

                    // Count pending from aspnet_leaverecord & aspnet_pre_leaverecord
                    decimal pending = 0;
                    using (var cmd = connSql.CreateCommand())
                    {
                        cmd.CommandText = "SELECT LEAVE_TYPE, COUNT(*) as CountVal FROM aspnet_leaverecord WHERE LEAVE_ID = @LeaveID AND EMP_ID = @EmpID AND LEAVE_STATUS = 'PENDING' GROUP BY LEAVE_TYPE";
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@LeaveID", item.LeaveID);
                        cmd.Parameters.AddWithValue("@EmpID", userId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string ltype = reader["LEAVE_TYPE"]?.ToString()?.ToUpper() ?? "";
                                int count = Convert.ToInt32(reader["CountVal"]);
                                pending += (ltype == "FULL") ? count : (count * 0.5m);
                            }
                        }
                    }
                    try
                    {
                        using (var cmd = connSql.CreateCommand())
                        {
                            cmd.CommandText = "SELECT LEAVE_TYPE, COUNT(*) as CountVal FROM aspnet_pre_leaverecord WHERE LEAVE_ID = @LeaveID AND EMP_ID = @EmpID AND LEAVE_STATUS = 'PENDING' GROUP BY LEAVE_TYPE";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@LeaveID", item.LeaveID);
                            cmd.Parameters.AddWithValue("@EmpID", userId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string ltype = reader["LEAVE_TYPE"]?.ToString()?.ToUpper() ?? "";
                                    int count = Convert.ToInt32(reader["CountVal"]);
                                    pending += (ltype == "FULL") ? count : (count * 0.5m);
                                }
                            }
                        }
                    }
                    catch { }

                    // Calculate indirect balance (dBalance2)
                    decimal balance2 = 0;
                    if (!string.IsNullOrEmpty(item.IndirectLeaveID))
                    {
                        var indirectLeaveIds = new List<string>();
                        using (var cmd = connSql.CreateCommand())
                        {
                            cmd.CommandText = "SELECT LEAVE_ID FROM aspnet_leaveid WHERE UserGroupID = @GroupID AND IndirectLeaveID = @IndirectID AND Display = 'Y' AND LEAVE_ID <> @IndirectID AND IndirectLeaveID <> LEAVE_ID";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@GroupID", groupId);
                            cmd.Parameters.AddWithValue("@IndirectID", item.IndirectLeaveID);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    indirectLeaveIds.Add(reader["LEAVE_ID"]?.ToString()?.Trim() ?? "");
                                }
                            }
                        }

                        foreach (var indId in indirectLeaveIds)
                        {
                            if (connMy != null && mysqlLeaves.TryGetValue(indId, out var indMw) && !string.IsNullOrEmpty(indMw.Cdlvepay))
                            {
                                try
                                {
                                    using (var cmd = connMy.CreateCommand())
                                    {
                                        cmd.CommandText = "SELECT balance FROM entitle WHERE employeeid = ? AND leaveid = ?";
                                        cmd.Parameters.Clear();
                                        cmd.Parameters.Add(new OdbcParameter("employeeid", userId));
                                        cmd.Parameters.Add(new OdbcParameter("leaveid", indMw.Cdlvepay));
                                        object val = cmd.ExecuteScalar();
                                        if (val != null && val != DBNull.Value)
                                        {
                                            balance2 += Convert.ToDecimal(val);
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }

                    // Calculate indirect pending
                    decimal pendingIndirect = 0;
                    if (!string.IsNullOrEmpty(item.IndirectLeaveID))
                    {
                        var indirectAllIds = new List<string>();
                        using (var cmd = connSql.CreateCommand())
                        {
                            cmd.CommandText = "SELECT LEAVE_ID FROM aspnet_leaveid WHERE UserGroupID = @GroupID AND IndirectLeaveID = @IndirectID AND Display = 'Y'";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@GroupID", groupId);
                            cmd.Parameters.AddWithValue("@IndirectID", item.IndirectLeaveID);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    indirectAllIds.Add(reader["LEAVE_ID"]?.ToString()?.Trim() ?? "");
                                }
                            }
                        }

                        foreach (var indAllId in indirectAllIds)
                        {
                            using (var cmd = connSql.CreateCommand())
                            {
                                cmd.CommandText = "SELECT LEAVE_TYPE, COUNT(*) as CountVal FROM aspnet_leaverecord WHERE LEAVE_ID = @LeaveID AND EMP_ID = @EmpID AND LEAVE_STATUS = 'PENDING' GROUP BY LEAVE_TYPE";
                                cmd.Parameters.Clear();
                                cmd.Parameters.AddWithValue("@LeaveID", indAllId);
                                cmd.Parameters.AddWithValue("@EmpID", userId);
                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        string ltype = reader["LEAVE_TYPE"]?.ToString()?.ToUpper() ?? "";
                                        int count = Convert.ToInt32(reader["CountVal"]);
                                        if (ltype == "FULL")
                                        {
                                            pendingIndirect += count;
                                        }
                                        else
                                        {
                                            pendingIndirect += count * 0.5m;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    decimal finalBalance = balance - balance2 - pending - pendingIndirect;

                    var subLeaves = new List<LeaveType>();
                    if (connMy != null && item.LeaveID.Equals(replacementLeaveId, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(cdlvepay))
                    {
                        try
                        {
                            using (var cmd = connMy.CreateCommand())
                            {
                                cmd.CommandText = "SELECT Recid, effdt, expdt, ent, bal FROM lverpl WHERE empid = ? AND lveid = ?";
                                cmd.Parameters.Clear();
                                cmd.Parameters.Add(new OdbcParameter("empid", userId));
                                cmd.Parameters.Add(new OdbcParameter("lveid", cdlvepay));
                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        string recid = reader["Recid"]?.ToString() ?? "";
                                        DateTime? effdt = reader["effdt"] != DBNull.Value ? Convert.ToDateTime(reader["effdt"]) : null;
                                        DateTime? expdt = reader["expdt"] != DBNull.Value ? Convert.ToDateTime(reader["expdt"]) : null;
                                        decimal ent = reader["ent"] != DBNull.Value ? Convert.ToDecimal(reader["ent"]) : 0;
                                        decimal bal = reader["bal"] != DBNull.Value ? Convert.ToDecimal(reader["bal"]) : 0;

                                        // Calculate taken for this replacement credit from sol & sol_h
                                        decimal rplTaken = 0;
                                        using (var cmdSol = connMy.CreateCommand())
                                        {
                                            cmdSol.CommandText = "SELECT SUM(value) FROM sol WHERE employeeid = ? AND RPLID = ?";
                                            cmdSol.Parameters.Clear();
                                            cmdSol.Parameters.Add(new OdbcParameter("employeeid", userId));
                                            cmdSol.Parameters.Add(new OdbcParameter("RPLID", recid));
                                            var solVal = cmdSol.ExecuteScalar();
                                            if (solVal != null && solVal != DBNull.Value)
                                            {
                                                rplTaken += Convert.ToDecimal(solVal);
                                            }
                                        }
                                        using (var cmdSolH = connMy.CreateCommand())
                                        {
                                            cmdSolH.CommandText = "SELECT SUM(value) FROM sol_h WHERE employeeid = ? AND RPLID = ?";
                                            cmdSolH.Parameters.Clear();
                                            cmdSolH.Parameters.Add(new OdbcParameter("employeeid", userId));
                                            cmdSolH.Parameters.Add(new OdbcParameter("RPLID", recid));
                                            var solHVal = cmdSolH.ExecuteScalar();
                                            if (solHVal != null && solHVal != DBNull.Value)
                                            {
                                                rplTaken += Convert.ToDecimal(solHVal);
                                            }
                                        }

                                        subLeaves.Add(new LeaveType
                                        {
                                            Code = item.LeaveID,
                                            Name = $"{leaveName} (Credit)",
                                            Entitle = ent,
                                            Taken = rplTaken,
                                            Pending = 0,
                                            Balance = bal,
                                            YearTot = ent,
                                            CarryForward = 0,
                                            Adjust = 0,
                                            EffectiveDate = effdt?.ToString("dd-MMM-yyyy"),
                                            ExpiryDate = expdt?.ToString("dd-MMM-yyyy"),
                                            IsReplacement = true
                                            // SubLeaves is empty for child records
                                        });
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    list.Add(new LeaveType
                    {
                        Code = item.LeaveID,
                        Name = leaveName,
                        IndirectLeaveId = item.IndirectLeaveID,
                        Entitle = entitle,
                        Taken = taken,
                        Pending = pending + pendingIndirect,
                        Balance = finalBalance,
                        YearTot = yeartot,
                        CarryForward = b_forward,
                        Adjust = adjust,
                        ExceedBf = exdbfmth,
                        Exceed = exceed,
                        EffectiveDate = null,
                        ExpiryDate = null,
                        IsReplacement = false,
                        SubLeaves = subLeaves
                    });
                }
            }
            finally
            {
                connMy?.Dispose();
            }
        }

        return Ok(list);
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { Message = "Error retrieving leave balances.", Details = ex.Message });
    }
}

    [HttpGet("history")]
    public IActionResult GetLeaveHistory([FromQuery] string? userId)
    {
        string connStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        var list = new List<LeaveRequest>();
        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    // Group individual day records into logical leave requests
                    if (string.IsNullOrEmpty(userId))
                    {
                        cmd.CommandText = @"
                            SELECT r.LeaveNo, r.Emp_ID, 
                                   COALESCE(MAX(r.NAME), MAX(u.Name), '') AS EmpName, 
                                   MAX(r.Leave_ID) AS Leave_ID, 
                                   COALESCE(MIN(r.dateLeavefrom), MIN(r.Leave_Date)) AS DateFrom, 
                                   COALESCE(MAX(r.dateLeaveto), MAX(r.Leave_Date)) AS DateTo, 
                                   r.Leave_Status, 
                                   MAX(r.leave_reason) AS Leave_Reason, 
                                   MAX(r.applevel) AS AppLevel,
                                   COUNT(DISTINCT r.Leave_Date) AS TotalDaysCount,
                                   COALESCE(MAX(r.LEAVE_TIME), '0000') AS TimeIn,
                                   COALESCE(MAX(r.LEAVE_TIME_TO), '0000') AS TimeOut,
                                   COALESCE(MAX(r.s1), '') AS s1,
                                   COALESCE(MAX(r.s2), '') AS s2,
                                   COALESCE(MAX(r.LEAVE_TYPE), 'FULL') AS DayType,
                                   COALESCE(MAX(r.picPath), '') AS File1,
                                   COALESCE(MAX(r.picName), '') AS File2,
                                   COALESCE(MAX(r.CDSCHDDY), '') AS ShiftGrp,
                                   COALESCE(MAX(r.CoNo), '') AS Dept
                            FROM aspnet_leaverecord r
                            LEFT JOIN aspnet_users u ON r.Emp_ID = u.ID
                            GROUP BY r.LeaveNo, r.Emp_ID, r.Leave_Status
                            ORDER BY MIN(r.Leave_Date) DESC";
                    }
                    else
                    {
                        cmd.CommandText = @"
                            SELECT r.LeaveNo, r.Emp_ID, 
                                   COALESCE(MAX(r.NAME), MAX(u.Name), '') AS EmpName, 
                                   MAX(r.Leave_ID) AS Leave_ID, 
                                   COALESCE(MIN(r.dateLeavefrom), MIN(r.Leave_Date)) AS DateFrom, 
                                   COALESCE(MAX(r.dateLeaveto), MAX(r.Leave_Date)) AS DateTo, 
                                   r.Leave_Status, 
                                   MAX(r.leave_reason) AS Leave_Reason, 
                                   MAX(r.applevel) AS AppLevel,
                                   COUNT(DISTINCT r.Leave_Date) AS TotalDaysCount,
                                   COALESCE(MAX(r.LEAVE_TIME), '0000') AS TimeIn,
                                   COALESCE(MAX(r.LEAVE_TIME_TO), '0000') AS TimeOut,
                                   COALESCE(MAX(r.s1), '') AS s1,
                                   COALESCE(MAX(r.s2), '') AS s2,
                                   COALESCE(MAX(r.LEAVE_TYPE), 'FULL') AS DayType,
                                   COALESCE(MAX(r.picPath), '') AS File1,
                                   COALESCE(MAX(r.picName), '') AS File2,
                                   COALESCE(MAX(r.CDSCHDDY), '') AS ShiftGrp,
                                   COALESCE(MAX(r.CoNo), '') AS Dept
                            FROM aspnet_leaverecord r
                            LEFT JOIN aspnet_users u ON r.Emp_ID = u.ID
                            WHERE r.Emp_ID = @EmpID
                            GROUP BY r.LeaveNo, r.Emp_ID, r.Leave_Status
                            ORDER BY MIN(r.Leave_Date) DESC";
                        cmd.Parameters.AddWithValue("@EmpID", userId);
                    }

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            decimal totalDays = 1.0m;
                            if (reader["TotalDaysCount"] != DBNull.Value && decimal.TryParse(reader["TotalDaysCount"].ToString(), out var td) && td > 0)
                            {
                                totalDays = td;
                            }
                            string dayType = reader["DayType"]?.ToString() ?? "FULL";
                            if (dayType.Contains("Half", StringComparison.OrdinalIgnoreCase) && totalDays <= 1)
                            {
                                totalDays = 0.5m;
                            }

                            list.Add(new LeaveRequest
                            {
                                LeaveNo = reader["LeaveNo"]?.ToString() ?? "",
                                Emp_ID = reader["Emp_ID"]?.ToString() ?? "",
                                EmpName = reader["EmpName"]?.ToString() ?? "",
                                Leave_Type = reader["Leave_ID"]?.ToString() ?? "",
                                Leave_Date_From = Convert.ToDateTime(reader["DateFrom"]),
                                Leave_Date_To = Convert.ToDateTime(reader["DateTo"]),
                                TotalDays = totalDays,
                                Leave_Status = reader["Leave_Status"]?.ToString() ?? "",
                                Leave_Reason = reader["Leave_Reason"]?.ToString() ?? "",
                                AppLevel = reader["AppLevel"]?.ToString() ?? "",
                                TimeIn = reader["TimeIn"]?.ToString() ?? "0000",
                                TimeOut = reader["TimeOut"]?.ToString() ?? "0000",
                                s1 = reader["s1"]?.ToString() ?? "",
                                s2 = reader["s2"]?.ToString() ?? "",
                                Type = dayType,
                                File1 = reader["File1"]?.ToString() ?? "",
                                File2 = reader["File2"]?.ToString() ?? "",
                                ShiftGrp = reader["ShiftGrp"]?.ToString() ?? "",
                                Dept = reader["Dept"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "SQL Server database error.", Details = ex.Message });
        }
    }

    [HttpGet("pending-count")]
    public async Task<IActionResult> GetPendingCount([FromQuery] string? supervisorId)
    {
        string connStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        string superId = supervisorId?.Trim() ?? "";
        try
        {
            using var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();

            bool isSuperAdmin = false;
            if (string.IsNullOrEmpty(superId) || 
                superId.Equals("admin", StringComparison.OrdinalIgnoreCase) || 
                superId.Equals("super", StringComparison.OrdinalIgnoreCase))
            {
                isSuperAdmin = true;
            }
            else
            {
                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT superAdminLeave FROM aspnet_applevel WHERE ID = @SuperID AND GroupID = 'LEAVE' LIMIT 1";
                checkCmd.Parameters.AddWithValue("@SuperID", superId);
                var superVal = (await checkCmd.ExecuteScalarAsync())?.ToString()?.Trim();
                if (string.Equals(superVal, "Y", StringComparison.OrdinalIgnoreCase))
                {
                    isSuperAdmin = true;
                }
            }

            using var cmd = conn.CreateCommand();
            if (isSuperAdmin)
            {
                cmd.CommandText = @"
                    SELECT COUNT(DISTINCT leaveNo) FROM (
                        SELECT leaveNo FROM aspnet_leaverecord_group WHERE UPPER(LEAVE_STATUS) = 'PENDING'
                        UNION
                        SELECT LeaveNo AS leaveNo FROM aspnet_leaverecord WHERE UPPER(LEAVE_STATUS) = 'PENDING'
                    ) AS combinedPending";
            }
            else
            {
                cmd.CommandText = @"
                    SELECT COUNT(DISTINCT leaveNo) FROM (
                        SELECT leaveNo FROM aspnet_leaverecord_group 
                        WHERE UPPER(LEAVE_STATUS) = 'PENDING' 
                          AND (app1 = @SuperID OR app2 = @SuperID OR app3 = @SuperID OR app4 = @SuperID)
                        UNION
                        SELECT LeaveNo AS leaveNo FROM aspnet_leaverecord 
                        WHERE UPPER(LEAVE_STATUS) = 'PENDING' 
                          AND (app1 = @SuperID OR app2 = @SuperID OR app3 = @SuperID OR app4 = @SuperID)
                    ) AS combinedPending";
                cmd.Parameters.AddWithValue("@SuperID", superId);
            }
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
            return Ok(new { count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error counting pending leaves.", Details = ex.Message });
        }
    }

    [HttpGet("pending")]
    public IActionResult GetPendingApprovals([FromQuery] string supervisorId)
    {
        if (string.IsNullOrEmpty(supervisorId))
        {
            return BadRequest(new { Message = "SupervisorId is required." });
        }

        string connStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        var list = new List<LeaveRequest>();
        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT r.LeaveNo, r.Emp_ID, MAX(u.Name) AS EmpName, MAX(r.Leave_ID) AS Leave_ID, 
                               MIN(r.Leave_Date) AS DateFrom, MAX(r.Leave_Date) AS DateTo, 
                               r.Leave_Status, MAX(r.leave_reason) AS Leave_Reason, MAX(r.applevel) AS AppLevel
                        FROM aspnet_leaverecord r
                        LEFT JOIN aspnet_users u ON r.Emp_ID = u.ID
                        WHERE r.Leave_Status = 'PENDING'
                          AND (r.app1 = @SuperID OR r.app2 = @SuperID OR r.app3 = @SuperID OR r.app4 = @SuperID)
                        GROUP BY r.LeaveNo, r.Emp_ID, r.Leave_Status
                        ORDER BY MIN(r.Leave_Date) ASC";
                    cmd.Parameters.AddWithValue("@SuperID", supervisorId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new LeaveRequest
                            {
                                LeaveNo = reader["LeaveNo"]?.ToString() ?? "",
                                Emp_ID = reader["Emp_ID"]?.ToString() ?? "",
                                EmpName = reader["EmpName"]?.ToString() ?? "",
                                Leave_Type = reader["Leave_ID"]?.ToString() ?? "",
                                Leave_Date_From = Convert.ToDateTime(reader["DateFrom"]),
                                Leave_Date_To = Convert.ToDateTime(reader["DateTo"]),
                                Leave_Status = reader["Leave_Status"]?.ToString() ?? "",
                                Leave_Reason = reader["Leave_Reason"]?.ToString() ?? "",
                                AppLevel = reader["AppLevel"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Database error.", Details = ex.Message });
        }
    }

    /// <summary>
    /// Legacy GeteLeaveApprove_ObjLeaveApproval (BLL_Class.vb) implementation for Supervisor Approval Grid.
    /// Handles PENDING, CANCEL APPROVED LEAVE, SuperAdminLeave override, multi-level approver permissions,
    /// total day calculation from daily rows, attachments from aspnet_leaverecordpic, and dw_emp lookups.
    /// </summary>
    [HttpGet("supervisor-approval")]
    public async Task<IActionResult> GetSupervisorApprovalLeaves(
        [FromQuery] string status = "PENDING",
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string atype = "Y",
        [FromQuery] string? supervisorId = null)
    {
        string statusUpper = (status ?? "PENDING").Trim().ToUpperInvariant();
        string atypeUpper = (atype ?? "Y").Trim().ToUpperInvariant();
        string superId = supervisorId?.Trim() ?? "";

        DateTime df = dateFrom ?? DateTime.Today;
        DateTime dt = dateTo ?? DateTime.Today;

        string connStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        string tmsConnStr = GetTmsMySqlConnectionString();

        var resultList = new List<LeaveRequest>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();

            // 1. Check Super Admin permission for LEAVE (aspnet_applevel)
            bool isSuperAdmin = false;
            if (string.IsNullOrEmpty(superId) || 
                superId.Equals("admin", StringComparison.OrdinalIgnoreCase) || 
                superId.Equals("super", StringComparison.OrdinalIgnoreCase))
            {
                isSuperAdmin = true;
            }
            else
            {
                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT superAdminLeave FROM aspnet_applevel WHERE ID = @SuperID AND GroupID = 'LEAVE' LIMIT 1";
                checkCmd.Parameters.AddWithValue("@SuperID", superId);
                var superVal = (await checkCmd.ExecuteScalarAsync())?.ToString()?.Trim();
                if (string.Equals(superVal, "Y", StringComparison.OrdinalIgnoreCase))
                {
                    isSuperAdmin = true;
                }
            }

            // 2. Query records matching requested status
            if (statusUpper == "CANCELLEAVE")
            {
                using var cmd = conn.CreateCommand();
                var sql = new StringBuilder(@"
                    SELECT g.*, 
                           COALESCE(u.Name, g.NAME) AS DisplayEmpName,
                           COALESCE(l.Name, g.leave_name) AS DisplayLeaveName
                    FROM aspnet_leaverecord_group g
                    LEFT JOIN aspnet_users u ON g.EMP_ID = u.ID
                    LEFT JOIN dw_leave l ON g.LEAVE_ID = l.Leave_ID
                    WHERE UPPER(g.LEAVE_STATUS) = 'APPROVE'");

                if (atypeUpper != "Y")
                {
                    sql.Append(@" AND (
                        (g.LEAVE_DATE_FROM BETWEEN @DateFrom AND @DateTo) 
                        OR (g.LEAVE_DATE_FROM <= @DateFrom AND g.LEAVE_DATE_TO >= @DateTo) 
                        OR (g.LEAVE_DATE_TO BETWEEN @DateFrom AND @DateTo)
                    )");
                    cmd.Parameters.AddWithValue("@DateFrom", df.ToString("yyyy-MM-dd 00:00:00"));
                    cmd.Parameters.AddWithValue("@DateTo", dt.ToString("yyyy-MM-dd 23:59:59"));
                }

                if (!isSuperAdmin && !string.IsNullOrEmpty(superId))
                {
                    sql.Append(" AND (g.app1 = @SuperID OR g.app2 = @SuperID OR g.app3 = @SuperID OR g.app4 = @SuperID)");
                    cmd.Parameters.AddWithValue("@SuperID", superId);
                }

                sql.Append(" ORDER BY g.LEAVE_DATE_FROM DESC LIMIT 200");
                cmd.CommandText = sql.ToString();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var req = MapFromGroupReader(reader);
                    req.CheckBoxOpen = true;
                    req.CheckBoxOpen2 = true;
                    if (seenKeys.Add(req.LeaveNo + "@" + req.Emp_ID))
                    {
                        resultList.Add(req);
                    }
                }
            }
            else
            {
                // Status is PENDING (or APPROVE, REJECT)
                using (var cmd = conn.CreateCommand())
                {
                    var sql = new StringBuilder(@"
                        SELECT g.*, 
                               COALESCE(u.Name, g.NAME) AS DisplayEmpName,
                               COALESCE(l.Name, g.leave_name) AS DisplayLeaveName
                        FROM aspnet_leaverecord_group g
                        LEFT JOIN aspnet_users u ON g.EMP_ID = u.ID
                        LEFT JOIN dw_leave l ON g.LEAVE_ID = l.Leave_ID
                        WHERE UPPER(g.LEAVE_STATUS) = @Status");

                    cmd.Parameters.AddWithValue("@Status", statusUpper);

                    if (atypeUpper != "Y")
                    {
                        sql.Append(@" AND (
                            (g.LEAVE_DATE_FROM BETWEEN @DateFrom AND @DateTo) 
                            OR (g.LEAVE_DATE_FROM <= @DateFrom AND g.LEAVE_DATE_TO >= @DateTo) 
                            OR (g.LEAVE_DATE_TO BETWEEN @DateFrom AND @DateTo)
                        )");
                        cmd.Parameters.AddWithValue("@DateFrom", df.ToString("yyyy-MM-dd 00:00:00"));
                        cmd.Parameters.AddWithValue("@DateTo", dt.ToString("yyyy-MM-dd 23:59:59"));
                    }

                    if (!isSuperAdmin && !string.IsNullOrEmpty(superId))
                    {
                        sql.Append(" AND (g.app1 = @SuperID OR g.app2 = @SuperID OR g.app3 = @SuperID OR g.app4 = @SuperID)");
                        cmd.Parameters.AddWithValue("@SuperID", superId);
                    }

                    sql.Append(" ORDER BY g.LEAVE_DATE_FROM ASC");
                    cmd.CommandText = sql.ToString();

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var req = MapFromGroupReader(reader);
                        if (seenKeys.Add(req.LeaveNo + "@" + req.Emp_ID))
                        {
                            resultList.Add(req);
                        }
                    }
                }

                // Also check aspnet_leaverecord for any missing applications matching status
                if (statusUpper == "PENDING")
                {
                    using var cmd = conn.CreateCommand();
                    var sql = new StringBuilder(@"
                        SELECT r.LeaveNo, r.Emp_ID, 
                               COALESCE(MAX(r.NAME), MAX(u.Name), '') AS DisplayEmpName, 
                               MAX(r.Leave_ID) AS LEAVE_ID, 
                               COALESCE(MIN(r.dateLeavefrom), MIN(r.Leave_Date)) AS LEAVE_DATE_From, 
                               COALESCE(MAX(r.dateLeaveto), MAX(r.Leave_Date)) AS LEAVE_DATE_To, 
                               r.Leave_Status, 
                               MAX(r.leave_reason) AS LEAVE_REASON, 
                               MAX(r.applevel) AS applevel,
                               COALESCE(MAX(r.LEAVE_TIME), '0000') AS LEAVE_TIME,
                               COALESCE(MAX(r.LEAVE_TIME_TO), '0000') AS LEAVE_TIME_TO,
                               COALESCE(MAX(r.s1), '') AS s1,
                               COALESCE(MAX(r.s2), '') AS s2,
                               COALESCE(MAX(r.LEAVE_TYPE), 'FULL') AS LEAVE_TYPE,
                               COALESCE(MAX(r.picPath), '') AS picPath,
                               COALESCE(MAX(r.picName), '') AS picName,
                               COALESCE(MAX(r.CDSCHDDY), '') AS CDSCHDDY,
                               COALESCE(MAX(r.CoNo), '') AS CoNo,
                               MAX(r.app1) AS app1, MAX(r.app2) AS app2, MAX(r.app3) AS app3, MAX(r.app4) AS app4,
                               MAX(r.app1Pass) AS app1Pass, MAX(r.app2Pass) AS app2Pass, MAX(r.app3Pass) AS app3Pass, MAX(r.app4Pass) AS app4Pass,
                               COALESCE(MAX(l.Name), MAX(r.leave_name), '') AS DisplayLeaveName,
                               MAX(r.super_name) AS super_name,
                               '' AS Remarks,
                               MAX(r.CreatedDate) AS CreatedDate
                        FROM aspnet_leaverecord r
                        LEFT JOIN aspnet_users u ON r.Emp_ID = u.ID
                        LEFT JOIN dw_leave l ON r.Leave_ID = l.Leave_ID
                        WHERE UPPER(r.LEAVE_STATUS) = 'PENDING'");

                    if (!isSuperAdmin && !string.IsNullOrEmpty(superId))
                    {
                        sql.Append(" AND (r.app1 = @SuperID OR r.app2 = @SuperID OR r.app3 = @SuperID OR r.app4 = @SuperID)");
                        cmd.Parameters.AddWithValue("@SuperID", superId);
                    }

                    sql.Append(" GROUP BY r.LeaveNo, r.Emp_ID, r.Leave_Status");
                    cmd.CommandText = sql.ToString();

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        string lno = reader["LeaveNo"]?.ToString() ?? "";
                        string empid = reader["Emp_ID"]?.ToString() ?? "";
                        if (seenKeys.Add(lno + "@" + empid))
                        {
                            resultList.Add(MapFromRecordReader(reader));
                        }
                    }
                }
            }

            // 3. Detail Enrichment (TotalDays, Attachments, Approval Permissions)
            if (resultList.Count > 0)
            {
                var leaveNos = resultList.Select(r => r.LeaveNo).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

                // a. Batch calculate TotalDays from aspnet_leaverecord
                var daysMap = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < leaveNos.Count; i += 80)
                {
                    var chunk = leaveNos.Skip(i).Take(80).ToList();
                    using var dayCmd = conn.CreateCommand();
                    var inClause = string.Join(",", chunk.Select((_, idx) => $"@ln_{idx}"));
                    dayCmd.CommandText = $@"
                        SELECT LeaveNo, Emp_ID,
                               SUM(CASE WHEN UPPER(TRIM(LEAVE_TYPE)) = 'FULL' THEN 1.0 ELSE 0.5 END) AS CalculatedDays
                        FROM aspnet_leaverecord
                        WHERE LeaveNo IN ({inClause})
                        GROUP BY LeaveNo, Emp_ID";
                    for (int j = 0; j < chunk.Count; j++)
                    {
                        dayCmd.Parameters.AddWithValue($"@ln_{j}", chunk[j]);
                    }
                    using var dayReader = await dayCmd.ExecuteReaderAsync();
                    while (await dayReader.ReadAsync())
                    {
                        string lno = dayReader["LeaveNo"]?.ToString() ?? "";
                        string empid = dayReader["Emp_ID"]?.ToString() ?? "";
                        decimal d = dayReader["CalculatedDays"] != DBNull.Value ? Convert.ToDecimal(dayReader["CalculatedDays"]) : 1.0m;
                        daysMap[lno + "@" + empid] = d;
                    }
                }

                // b. Batch load attachments from aspnet_leaverecordpic
                var picMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < leaveNos.Count; i += 80)
                {
                    var chunk = leaveNos.Skip(i).Take(80).ToList();
                    using var picCmd = conn.CreateCommand();
                    var inClause = string.Join(",", chunk.Select((_, idx) => $"@pln_{idx}"));
                    picCmd.CommandText = $@"
                        SELECT LeaveNo, picPath, picName
                        FROM aspnet_leaverecordpic
                        WHERE LeaveNo IN ({inClause})
                        ORDER BY NoPic ASC";
                    for (int j = 0; j < chunk.Count; j++)
                    {
                        picCmd.Parameters.AddWithValue($"@pln_{j}", chunk[j]);
                    }
                    using var picReader = await picCmd.ExecuteReaderAsync();
                    while (await picReader.ReadAsync())
                    {
                        string lno = picReader["LeaveNo"]?.ToString() ?? "";
                        string pPath = picReader["picPath"]?.ToString()?.Trim() ?? "";
                        string pName = picReader["picName"]?.ToString()?.Trim() ?? "";
                        string displayFile = !string.IsNullOrEmpty(pPath) ? pPath : pName;
                        if (displayFile.StartsWith("~/")) displayFile = displayFile.Substring(1);

                        if (!picMap.TryGetValue(lno, out var list))
                        {
                            list = new List<string>();
                            picMap[lno] = list;
                        }
                        if (list.Count < 3 && !string.IsNullOrEmpty(displayFile))
                        {
                            list.Add(displayFile);
                        }
                    }
                }

                // Apply to resultList
                foreach (var req in resultList)
                {
                    if (daysMap.TryGetValue(req.LeaveNo + "@" + req.Emp_ID, out var calcDays) && calcDays > 0)
                    {
                        req.TotalDays = calcDays;
                    }
                    else if (req.TotalDays <= 0)
                    {
                        var span = (req.Leave_Date_To.Date - req.Leave_Date_From.Date).TotalDays + 1;
                        req.TotalDays = (decimal)(span > 0 ? span : 1);
                    }

                    if (picMap.TryGetValue(req.LeaveNo, out var files))
                    {
                        if (files.Count > 0) req.File1 = files[0];
                        if (files.Count > 1) req.File2 = files[1];
                        if (files.Count > 2) req.File3 = files[2];
                    }

                    // c. Permission calculation (CheckBoxOpen, CheckBoxOpen2)
                    EvaluateApproverPermissions(req, isSuperAdmin, superId, statusUpper);
                }
            }

            // d. Batch employee lookup (Dept, Section, ShiftGrp) from dw_emp
            await EnrichEmployeesFromDwEmpAsync(resultList, tmsConnStr, conn);

            return Ok(resultList);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error executing GeteLeaveApprove_ObjLeaveApproval process.", Details = ex.Message });
        }
    }

    private static LeaveRequest MapFromGroupReader(System.Data.Common.DbDataReader reader)
    {
        DateTime df = reader["LEAVE_DATE_From"] != DBNull.Value ? Convert.ToDateTime(reader["LEAVE_DATE_From"]) : DateTime.Today;
        DateTime dt = reader["LEAVE_DATE_To"] != DBNull.Value ? Convert.ToDateTime(reader["LEAVE_DATE_To"]) : df;
        DateTime? created = reader["CreatedDate"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedDate"]) : (DateTime?)null;

        string picPath = reader["picPath"]?.ToString()?.Trim() ?? "";
        string picName = reader["picName"]?.ToString()?.Trim() ?? "";
        string displayFile = !string.IsNullOrEmpty(picPath) ? picPath : picName;
        if (displayFile.StartsWith("~/")) displayFile = displayFile.Substring(1);

        return new LeaveRequest
        {
            LeaveNo = reader["leaveNo"]?.ToString()?.Trim() ?? "",
            Emp_ID = reader["EMP_ID"]?.ToString()?.Trim() ?? "",
            EmpName = reader["DisplayEmpName"]?.ToString()?.Trim() ?? (reader["NAME"]?.ToString()?.Trim() ?? ""),
            Leave_Type = reader["LEAVE_ID"]?.ToString()?.Trim() ?? "",
            LeaveName = reader["DisplayLeaveName"]?.ToString()?.Trim() ?? (reader["leave_name"]?.ToString()?.Trim() ?? ""),
            Leave_Date_From = df,
            Leave_Date_To = dt,
            Leave_Status = reader["LEAVE_STATUS"]?.ToString()?.Trim() ?? "PENDING",
            Leave_Reason = reader["LEAVE_REASON"]?.ToString()?.Trim() ?? "",
            AppLevel = reader["applevel"]?.ToString()?.Trim() ?? "1",
            TimeIn = reader["LEAVE_TIME"]?.ToString()?.Trim() ?? "0000",
            TimeOut = reader["LEAVE_TIME_TO"]?.ToString()?.Trim() ?? "0000",
            s1 = reader["s1"]?.ToString()?.Trim() ?? "",
            s2 = reader["s2"]?.ToString()?.Trim() ?? "",
            Type = reader["LEAVE_TYPE"]?.ToString()?.Trim() ?? "FULL",
            ShiftGrp = reader["CDSCHDDY"]?.ToString()?.Trim() ?? "",
            Dept = reader["CoNo"]?.ToString()?.Trim() ?? "",
            SuperName = reader["super_name"]?.ToString()?.Trim() ?? "",
            App1 = reader["app1"]?.ToString()?.Trim() ?? "",
            App1Pass = reader["app1Pass"]?.ToString()?.Trim() ?? "NO",
            App2 = reader["app2"]?.ToString()?.Trim() ?? "",
            App2Pass = reader["app2Pass"]?.ToString()?.Trim() ?? "NO",
            App3 = reader["app3"]?.ToString()?.Trim() ?? "",
            App3Pass = reader["app3Pass"]?.ToString()?.Trim() ?? "NO",
            App4 = reader["app4"]?.ToString()?.Trim() ?? "",
            App4Pass = reader["app4Pass"]?.ToString()?.Trim() ?? "NO",
            Remarks = reader["Remarks"]?.ToString()?.Trim() ?? "",
            CreateDate = created,
            File1 = displayFile
        };
    }

    private static LeaveRequest MapFromRecordReader(System.Data.Common.DbDataReader reader)
    {
        DateTime df = reader["LEAVE_DATE_From"] != DBNull.Value ? Convert.ToDateTime(reader["LEAVE_DATE_From"]) : DateTime.Today;
        DateTime dt = reader["LEAVE_DATE_To"] != DBNull.Value ? Convert.ToDateTime(reader["LEAVE_DATE_To"]) : df;
        DateTime? created = reader["CreatedDate"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedDate"]) : (DateTime?)null;

        string picPath = reader["picPath"]?.ToString()?.Trim() ?? "";
        string picName = reader["picName"]?.ToString()?.Trim() ?? "";
        string displayFile = !string.IsNullOrEmpty(picPath) ? picPath : picName;
        if (displayFile.StartsWith("~/")) displayFile = displayFile.Substring(1);

        return new LeaveRequest
        {
            LeaveNo = reader["LeaveNo"]?.ToString()?.Trim() ?? "",
            Emp_ID = reader["Emp_ID"]?.ToString()?.Trim() ?? "",
            EmpName = reader["DisplayEmpName"]?.ToString()?.Trim() ?? (reader["NAME"]?.ToString()?.Trim() ?? ""),
            Leave_Type = reader["LEAVE_ID"]?.ToString()?.Trim() ?? "",
            LeaveName = reader["DisplayLeaveName"]?.ToString()?.Trim() ?? "",
            Leave_Date_From = df,
            Leave_Date_To = dt,
            Leave_Status = reader["Leave_Status"]?.ToString()?.Trim() ?? "PENDING",
            Leave_Reason = reader["LEAVE_REASON"]?.ToString()?.Trim() ?? "",
            AppLevel = reader["applevel"]?.ToString()?.Trim() ?? "1",
            TimeIn = reader["LEAVE_TIME"]?.ToString()?.Trim() ?? "0000",
            TimeOut = reader["LEAVE_TIME_TO"]?.ToString()?.Trim() ?? "0000",
            s1 = reader["s1"]?.ToString()?.Trim() ?? "",
            s2 = reader["s2"]?.ToString()?.Trim() ?? "",
            Type = reader["LEAVE_TYPE"]?.ToString()?.Trim() ?? "FULL",
            ShiftGrp = reader["CDSCHDDY"]?.ToString()?.Trim() ?? "",
            Dept = reader["CoNo"]?.ToString()?.Trim() ?? "",
            SuperName = reader["super_name"]?.ToString()?.Trim() ?? "",
            App1 = reader["app1"]?.ToString()?.Trim() ?? "",
            App1Pass = reader["app1Pass"]?.ToString()?.Trim() ?? "NO",
            App2 = reader["app2"]?.ToString()?.Trim() ?? "",
            App2Pass = reader["app2Pass"]?.ToString()?.Trim() ?? "NO",
            App3 = reader["app3"]?.ToString()?.Trim() ?? "",
            App3Pass = reader["app3Pass"]?.ToString()?.Trim() ?? "NO",
            App4 = reader["app4"]?.ToString()?.Trim() ?? "",
            App4Pass = reader["app4Pass"]?.ToString()?.Trim() ?? "NO",
            Remarks = reader["Remarks"]?.ToString()?.Trim() ?? "",
            CreateDate = created,
            File1 = displayFile
        };
    }

    private static void EvaluateApproverPermissions(LeaveRequest req, bool isSuperAdmin, string superId, string statusUpper)
    {
        if (statusUpper == "CANCELLEAVE")
        {
            bool isApprover = req.App1 == superId || req.App2 == superId || req.App3 == superId || req.App4 == superId;
            req.CheckBoxOpen = isApprover;
            req.CheckBoxOpen2 = isSuperAdmin || isApprover;
            return;
        }

        if (isSuperAdmin)
        {
            req.CheckBoxOpen = true;
            req.CheckBoxOpen2 = true;
            return;
        }

        // Exact GeteLeaveApprove_ObjLeaveApproval logic (BLL_Class.vb lines 10214-10292):
        string appLevel = req.AppLevel?.Trim() ?? "1";
        string app1 = req.App1?.Trim() ?? "";
        string app1Pass = req.App1Pass?.Trim()?.ToUpperInvariant() ?? "NO";
        string app2 = req.App2?.Trim() ?? "";
        string app2Pass = req.App2Pass?.Trim()?.ToUpperInvariant() ?? "NO";
        string app3 = req.App3?.Trim() ?? "";
        string app3Pass = req.App3Pass?.Trim()?.ToUpperInvariant() ?? "NO";
        string app4 = req.App4?.Trim() ?? "";
        string app4Pass = req.App4Pass?.Trim()?.ToUpperInvariant() ?? "NO";

        bool open = false;

        if (appLevel == "1")
        {
            if (app1 == superId && app1Pass == "NO" && app2Pass == "NO" && app3Pass == "NO")
                open = true;
            else if (app2 == superId && app1Pass == "YES" && app2Pass == "NO" && app3Pass == "NO")
                open = true;
            else if (app3 == superId && app1Pass == "YES" && app2Pass == "YES" && app3Pass == "NO")
                open = true;
            else if (app4 == superId && app1Pass == "YES" && app2Pass == "YES" && app3Pass == "YES")
                open = true;
        }
        else if (appLevel == "2")
        {
            if (app1Pass == "NO" && app2Pass == "NO" && app3Pass == "NO" && (app4Pass == "NO" || string.IsNullOrEmpty(app4Pass)))
                open = true;
        }
        else if (appLevel == "3")
        {
            if (app1 == superId && app1Pass == "NO")
                open = true;
            else if (app2 == superId && app2Pass == "NO")
                open = true;
            else if ((app3 == superId || app4 == superId) && (app1Pass == "YES" || app2Pass == "YES") && app3Pass == "NO")
                open = true;
        }
        else
        {
            if (app1 == superId || app2 == superId || app3 == superId || app4 == superId)
                open = true;
        }

        req.CheckBoxOpen = open;
        req.CheckBoxOpen2 = open;
    }

    private async Task EnrichEmployeesFromDwEmpAsync(List<LeaveRequest> list, string tmsConnStr, MySqlConnection mainConn)
    {
        var empIds = list.Select(l => l.Emp_ID).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        if (empIds.Count == 0) return;

        var empMap = new Dictionary<string, (string Dept, string Section, string ShiftGrp)>(StringComparer.OrdinalIgnoreCase);

        // 1. Try querying dw_emp from TMS MySQL
        try
        {
            using var tmsConn = new MySqlConnection(tmsConnStr);
            await tmsConn.OpenAsync();
            using var cmd = tmsConn.CreateCommand();
            cmd.CommandText = "SELECT Emp_ID, Dept_ID, SECTION_ID, wrkgrp_ID FROM dw_emp";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string id = reader["Emp_ID"]?.ToString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(id))
                {
                    empMap[id] = (
                        reader["Dept_ID"]?.ToString()?.Trim() ?? "",
                        reader["SECTION_ID"]?.ToString()?.Trim() ?? "",
                        reader["wrkgrp_ID"]?.ToString()?.Trim() ?? ""
                    );
                }
            }
        }
        catch { }

        // 2. Fallback to mainConn (myhrsuitelog) if dw_emp exists there
        if (empMap.Count == 0)
        {
            try
            {
                using var cmd = mainConn.CreateCommand();
                cmd.CommandText = "SELECT Emp_ID, Dept_ID, SECTION_ID, wrkgrp_ID FROM dw_emp";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string id = reader["Emp_ID"]?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(id))
                    {
                        empMap[id] = (
                            reader["Dept_ID"]?.ToString()?.Trim() ?? "",
                            reader["SECTION_ID"]?.ToString()?.Trim() ?? "",
                            reader["wrkgrp_ID"]?.ToString()?.Trim() ?? ""
                        );
                    }
                }
            }
            catch { }
        }

        // Apply to list
        foreach (var req in list)
        {
            if (empMap.TryGetValue(req.Emp_ID, out var info))
            {
                if (!string.IsNullOrEmpty(info.Dept)) req.Dept = info.Dept;
                if (!string.IsNullOrEmpty(info.Section)) req.Section = info.Section;
                if (!string.IsNullOrEmpty(info.ShiftGrp)) req.ShiftGrp = info.ShiftGrp;
            }
        }
    }

    [HttpPost("apply")]
    public IActionResult ApplyLeave([FromBody] LeaveApplyModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.EmpId) || string.IsNullOrEmpty(model.LeaveCode))
        {
            return BadRequest(new { Message = "Invalid leave application payload." });
        }

        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        
        try
        {
            // 1. Get Employee Name and Supervisor level from DB
            string empName = "";
            string app1 = model.EmpId, app2 = "", app3 = "", app4 = "";
            string applevel = "1", appNo = "1";

            using (var conn = new MySqlConnection(sqlConnStr))
            {
                conn.Open();
                
                // Get Name
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Name FROM aspnet_users WHERE ID = @ID";
                    cmd.Parameters.AddWithValue("@ID", model.EmpId);
                    empName = cmd.ExecuteScalar()?.ToString() ?? "";
                }

                // Get supervisor mapping
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT app1, app2, app3, app4, appNo, applevel FROM aspnet_applevel WHERE ID = @ID AND GroupID = 'LEAVE'";
                    cmd.Parameters.AddWithValue("@ID", model.EmpId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            app1 = reader["app1"]?.ToString() ?? model.EmpId;
                            app2 = reader["app2"]?.ToString() ?? "";
                            app3 = reader["app3"]?.ToString() ?? "";
                            app4 = reader["app4"]?.ToString() ?? "";
                            appNo = reader["appNo"]?.ToString() ?? "1";
                            applevel = reader["applevel"]?.ToString() ?? "1";
                        }
                    }
                }

                // Generate a unique LeaveNo (similar to yyMMddhhmmss)
                string leaveNo = model.EmpId + DateTime.Now.ToString("yyMMddHHmmss");

                // Insert one row per day in the range
                DateTime current = model.DateFrom.Date;
                DateTime end = model.DateTo.Date;

                while (current <= end)
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO aspnet_leaverecord (
                                leaveNo, LEAVE_DATE, EMP_ID, NAME, SUPER_ID, LEAVE_STATUS, LEAVE_REASON, LEAVE_ID, LEAVE_TYPE,
                                dateLeavefrom, dateLeaveto, app1, app2, app3, app4, app1Pass, app2Pass, app3Pass, app4Pass,
                                applevel, appNo, CreatedDate
                            ) VALUES (
                                @LeaveNo, @LeaveDate, @EmpID, @Name, @SuperID, 'PENDING', @Reason, @LeaveID, @LeaveType,
                                @DateFrom, @DateTo, @App1, @App2, @App3, @App4, 'NO', 'NO', 'NO', 'NO',
                                @AppLevel, @AppNo, NOW()
                            )";
                        
                        cmd.Parameters.AddWithValue("@LeaveNo", leaveNo);
                        cmd.Parameters.AddWithValue("@LeaveDate", current);
                        cmd.Parameters.AddWithValue("@EmpID", model.EmpId);
                        cmd.Parameters.AddWithValue("@Name", empName);
                        cmd.Parameters.AddWithValue("@SuperID", app1);
                        cmd.Parameters.AddWithValue("@Reason", model.Reason);
                        cmd.Parameters.AddWithValue("@LeaveID", model.LeaveCode);
                        cmd.Parameters.AddWithValue("@LeaveType", model.LeaveType);
                        cmd.Parameters.AddWithValue("@DateFrom", model.DateFrom);
                        cmd.Parameters.AddWithValue("@DateTo", model.DateTo);
                        cmd.Parameters.AddWithValue("@App1", app1);
                        cmd.Parameters.AddWithValue("@App2", app2);
                        cmd.Parameters.AddWithValue("@App3", app3);
                        cmd.Parameters.AddWithValue("@App4", app4);
                        cmd.Parameters.AddWithValue("@AppLevel", applevel);
                        cmd.Parameters.AddWithValue("@AppNo", appNo);

                        cmd.ExecuteNonQuery();
                    }
                    current = current.AddDays(1);
                }
            }

            return Ok(new { Message = "Leave request submitted successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error submitting leave.", Details = ex.Message });
        }
    }

    [HttpPost("approve")]
    public IActionResult ApproveLeave([FromBody] ApproveActionModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.LeaveNo) || string.IsNullOrEmpty(model.EmpId))
        {
            return BadRequest(new { Message = "Invalid request arguments." });
        }

        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        string mysqlConnStr = _configuration.GetConnectionString("ConnectionTMS") ?? "";

        try
        {
            string actionUpper = (model.Action ?? "").Trim().ToUpperInvariant();
            string newStatus = "REJECT";
            if (actionUpper == "APPROVE" || actionUpper == "APPROVED")
            {
                newStatus = "APPROVE";
            }
            else if (actionUpper == "CANCEL" || actionUpper == "CANCELLED" || actionUpper == "CANCEL_APPROVE")
            {
                newStatus = "CANCEL";
            }
            else
            {
                newStatus = "REJECT";
            }

            using (var conn = new MySqlConnection(sqlConnStr))
            {
                conn.Open();
                
                // Fetch leave request details to copy/remove to/from MySQL upon approval/cancellation
                string leaveId = "";
                DateTime dateFrom = DateTime.Today;
                DateTime dateTo = DateTime.Today;
                string leaveType = "FULL";

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT LEAVE_ID, dateLeavefrom, dateLeaveto, LEAVE_TYPE FROM aspnet_leaverecord WHERE LeaveNo = @LeaveNo AND Emp_ID = @EmpID LIMIT 1";
                    cmd.Parameters.AddWithValue("@LeaveNo", model.LeaveNo);
                    cmd.Parameters.AddWithValue("@EmpID", model.EmpId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            leaveId = reader["LEAVE_ID"]?.ToString() ?? "";
                            if (reader["dateLeavefrom"] != DBNull.Value)
                                dateFrom = Convert.ToDateTime(reader["dateLeavefrom"]);
                            if (reader["dateLeaveto"] != DBNull.Value)
                                dateTo = Convert.ToDateTime(reader["dateLeaveto"]);
                            leaveType = reader["LEAVE_TYPE"]?.ToString() ?? "FULL";
                        }
                    }
                }

                // Update SQL Server status
                using (var cmd = conn.CreateCommand())
                {
                    if (newStatus == "CANCEL")
                    {
                        cmd.CommandText = @"
                            UPDATE aspnet_leaverecord 
                            SET LEAVE_STATUS = 'CANCEL',
                                edit_status = 'CANCEL',
                                dateapp1 = NOW() 
                            WHERE LeaveNo = @LeaveNo AND Emp_ID = @EmpID";
                    }
                    else if (newStatus == "APPROVE")
                    {
                        cmd.CommandText = @"
                            UPDATE aspnet_leaverecord 
                            SET LEAVE_STATUS = 'APPROVE', 
                                app1Pass = 'YES',
                                dateapp1 = NOW() 
                            WHERE LeaveNo = @LeaveNo AND Emp_ID = @EmpID";
                    }
                    else
                    {
                        cmd.CommandText = @"
                            UPDATE aspnet_leaverecord 
                            SET LEAVE_STATUS = 'REJECT', 
                                app1Pass = 'NO',
                                dateapp1 = NOW() 
                            WHERE LeaveNo = @LeaveNo AND Emp_ID = @EmpID";
                    }
                    cmd.Parameters.AddWithValue("@LeaveNo", model.LeaveNo);
                    cmd.Parameters.AddWithValue("@EmpID", model.EmpId);
                    cmd.ExecuteNonQuery();
                }

                // Also synchronize aspnet_leaverecord_group
                using (var cmd = conn.CreateCommand())
                {
                    if (newStatus == "CANCEL")
                    {
                        cmd.CommandText = @"
                            UPDATE aspnet_leaverecord_group 
                            SET LEAVE_STATUS = 'CANCEL',
                                edit_status = 'CANCEL',
                                Remarks = CASE WHEN @Remarks <> '' THEN @Remarks ELSE Remarks END,
                                Approved_date = NOW(),
                                Approve_by = @SupervisorId
                            WHERE leaveNo = @LeaveNo AND EMP_ID = @EmpID";
                    }
                    else if (newStatus == "APPROVE")
                    {
                        cmd.CommandText = @"
                            UPDATE aspnet_leaverecord_group 
                            SET LEAVE_STATUS = 'APPROVE', 
                                app1Pass = 'YES',
                                dateapp1 = NOW(),
                                Remarks = CASE WHEN @Remarks <> '' THEN @Remarks ELSE Remarks END,
                                Approved_date = NOW(),
                                Approve_by = @SupervisorId
                            WHERE leaveNo = @LeaveNo AND EMP_ID = @EmpID";
                    }
                    else
                    {
                        cmd.CommandText = @"
                            UPDATE aspnet_leaverecord_group 
                            SET LEAVE_STATUS = 'REJECT', 
                                app1Pass = 'NO',
                                dateapp1 = NOW(),
                                Remarks = CASE WHEN @Remarks <> '' THEN @Remarks ELSE Remarks END,
                                Approved_date = NOW(),
                                Approve_by = @SupervisorId
                            WHERE leaveNo = @LeaveNo AND EMP_ID = @EmpID";
                    }
                    cmd.Parameters.AddWithValue("@LeaveNo", model.LeaveNo);
                    cmd.Parameters.AddWithValue("@EmpID", model.EmpId);
                    cmd.Parameters.AddWithValue("@Remarks", model.Remarks ?? "");
                    cmd.Parameters.AddWithValue("@SupervisorId", model.SupervisorId ?? "");
                    cmd.ExecuteNonQuery();
                }

                // If approved, insert into MySQL transaction table (dw_leaveform)
                if (newStatus == "APPROVE" && !string.IsNullOrEmpty(leaveId))
                {
                    double leaveDay = leaveType == "FULL" ? 1.0 : 0.5;
                    
                    try
                    {
                        using (var myConn = new OdbcConnection(mysqlConnStr))
                        {
                            myConn.Open();
                            using (var myCmd = myConn.CreateCommand())
                            {
                                myCmd.CommandText = @"
                                    INSERT INTO dw_leaveform (EMP_ID, LEAVE_ID, DATE_FROM, DATE_TO, TMFR, TMTO, LEAVE_DAY, REMARK, nw, TM_LEAVE) 
                                    VALUES (?, ?, ?, ?, '0000', '0000', ?, 'ESystem', 'Y', '0000')";
                                myCmd.Parameters.Add(new OdbcParameter("EMP_ID", model.EmpId));
                                myCmd.Parameters.Add(new OdbcParameter("LEAVE_ID", leaveId));
                                myCmd.Parameters.Add(new OdbcParameter("DATE_FROM", OdbcType.Date) { Value = dateFrom });
                                myCmd.Parameters.Add(new OdbcParameter("DATE_TO", OdbcType.Date) { Value = dateTo });
                                myCmd.Parameters.Add(new OdbcParameter("LEAVE_DAY", leaveDay));
                                myCmd.ExecuteNonQuery();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log MySQL insertion warning but don't fail the whole request
                        Console.WriteLine("MySQL insertion failed: " + ex.Message);
                    }
                }
                // If CANCEL, delete from MySQL transaction table (dw_leaveform) to restore employee balance
                else if (newStatus == "CANCEL" && !string.IsNullOrEmpty(leaveId))
                {
                    try
                    {
                        using (var myConn = new OdbcConnection(mysqlConnStr))
                        {
                            myConn.Open();
                            using (var myCmd = myConn.CreateCommand())
                            {
                                myCmd.CommandText = "DELETE FROM dw_leaveform WHERE EMP_ID = ? AND LEAVE_ID = ? AND DATE_FROM = ? AND DATE_TO = ?";
                                myCmd.Parameters.Add(new OdbcParameter("EMP_ID", model.EmpId));
                                myCmd.Parameters.Add(new OdbcParameter("LEAVE_ID", leaveId));
                                myCmd.Parameters.Add(new OdbcParameter("DATE_FROM", OdbcType.Date) { Value = dateFrom });
                                myCmd.Parameters.Add(new OdbcParameter("DATE_TO", OdbcType.Date) { Value = dateTo });
                                myCmd.ExecuteNonQuery();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("MySQL delete dw_leaveform failed: " + ex.Message);
                    }
                }
            }

            return Ok(new { Message = $"Leave request successfully {(newStatus == "APPROVE" ? "approved" : newStatus == "CANCEL" ? "cancelled" : "rejected")}." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error updating leave request status.", Details = ex.Message });
        }
    }

    [HttpPost("approve-batch")]
    public IActionResult ApproveBatchLeaves([FromBody] List<ApproveActionModel> models)
    {
        if (models == null || models.Count == 0)
        {
            return BadRequest(new { Message = "No items specified." });
        }

        int successCount = 0;
        foreach (var m in models)
        {
            var res = ApproveLeave(m);
            if (res is OkObjectResult) successCount++;
        }

        return Ok(new { Message = $"{successCount} of {models.Count} leave requests processed successfully.", Count = successCount });
    }

    private string GetTmsMySqlConnectionString()
    {
        var connStr = _configuration.GetConnectionString("ConnectionTMS_MySql");
        if (!string.IsNullOrEmpty(connStr)) return connStr;
        return "Server=localhost;Port=3309;Database=dw__app_demo2;User ID=dba;Password=sql8;AllowUserVariables=True;CharSet=utf8mb4;";
    }

    private async Task<List<LeaveUserAccessItem>> QueryTmsTableAsync(string formId, string tableName, string idCol, string nameCol, string remarkCol = "")
    {
        var items = new List<LeaveUserAccessItem>();
        string connStr = GetTmsMySqlConnectionString();

        try
        {
            using var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = string.IsNullOrEmpty(remarkCol)
                ? $"SELECT {idCol}, {nameCol} FROM {tableName} ORDER BY {idCol}"
                : $"SELECT {idCol}, {nameCol}, {remarkCol} FROM {tableName} ORDER BY {idCol}";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new LeaveUserAccessItem
                {
                    FormId = formId,
                    Id = reader[idCol]?.ToString()?.Trim() ?? "",
                    Name = reader[nameCol]?.ToString()?.Trim() ?? "",
                    Remark = !string.IsNullOrEmpty(remarkCol) ? (reader[remarkCol]?.ToString()?.Trim() ?? "") : ""
                });
            }
            return items;
        }
        catch
        {
            // Fallback: try querying via myhrsuitelog cross-database
            try
            {
                string myhrConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
                using var conn = new MySqlConnection(myhrConnStr);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = string.IsNullOrEmpty(remarkCol)
                    ? $"SELECT {idCol}, {nameCol} FROM `{tableName}` ORDER BY {idCol}"
                    : $"SELECT {idCol}, {nameCol}, {remarkCol} FROM `{tableName}` ORDER BY {idCol}";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new LeaveUserAccessItem
                    {
                        FormId = formId,
                        Id = reader[idCol]?.ToString()?.Trim() ?? "",
                        Name = reader[nameCol]?.ToString()?.Trim() ?? "",
                        Remark = !string.IsNullOrEmpty(remarkCol) ? (reader[remarkCol]?.ToString()?.Trim() ?? "") : ""
                    });
                }
            }
            catch { }
            return items;
        }
    }

    [HttpGet("user-access/users")]
    public async Task<IActionResult> GetUserAccessUsers()
    {
        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        var list = new List<EmployeeLookupModel>();
        try
        {
            using var conn = new MySqlConnection(sqlConnStr);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT ID, Name FROM aspnet_users WHERE IsActive = 'Y' ORDER BY ID";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new EmployeeLookupModel
                {
                    EmpId = reader["ID"]?.ToString() ?? "",
                    Name = reader["Name"]?.ToString() ?? ""
                });
            }
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Failed to load users", Details = ex.Message });
        }
    }

    [HttpGet("user-access")]
    public async Task<IActionResult> GetUserAccess([FromQuery] string? userId)
    {
        var data = new LeaveUserAccessData { UserId = userId ?? "" };
        try
        {
            // Fetch 8 master lists from TMS in parallel
            var tDepts = QueryTmsTableAsync("DEP_FROM", "dw_dept", "DEPT_ID", "NAME", "REMARK");
            var tSections = QueryTmsTableAsync("SEC_FROM", "dw_section", "SECTION_ID", "NAME", "REMARK");
            var tCategories = QueryTmsTableAsync("CAT_FROM", "dw_category", "CATEGORY_ID", "NAME", "REMARK");
            var tGrades = QueryTmsTableAsync("GRADE_FROM", "dw_grade", "GRADE_ID", "NAME", "REMARK");
            var tSupervisors = QueryTmsTableAsync("SUP_FROM", "dw_super", "SUPER_ID", "NAME", "REMARK");
            var tJobs = QueryTmsTableAsync("JOB_FROM", "dw_job", "JOB_ID", "name", "REMARK");
            var tFactories = QueryTmsTableAsync("FACTORY_FROM", "dw_factory", "FACTORY_ID", "NAME", "REMARK");
            var tGroups = QueryTmsTableAsync("GROUP_FROM", "dw_tms_wrkgrp", "wrkgrp_id", "dsc");

            await Task.WhenAll(tDepts, tSections, tCategories, tGrades, tSupervisors, tJobs, tFactories, tGroups);

            data.Departments = await tDepts;
            data.Sections = await tSections;
            data.Categories = await tCategories;
            data.Grades = await tGrades;
            data.Supervisors = await tSupervisors;
            data.JobTitles = await tJobs;
            data.Factories = await tFactories;
            data.Groups = await tGroups;

            // Fetch Emails from aspnet_users in myhrsuitelog
            string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
            using (var conn = new MySqlConnection(sqlConnStr))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT ID, Name, Email FROM aspnet_users WHERE IsActive = 'Y' ORDER BY ID";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    data.Emails.Add(new LeaveUserAccessItem
                    {
                        FormId = "Email_FROM",
                        Id = reader["ID"]?.ToString()?.Trim() ?? "",
                        Name = reader["Name"]?.ToString()?.Trim() ?? "",
                        Extra = reader["Email"]?.ToString()?.Trim() ?? "",
                        Remark = reader["Email"]?.ToString()?.Trim() ?? ""
                    });
                }
            }

            // If userId specified, load saved selections from aspnet_levelaccess
            if (!string.IsNullOrEmpty(userId))
            {
                var selectedSet = new HashSet<(string FormId, string Id)>();
                using (var conn = new MySqlConnection(sqlConnStr))
                {
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT Form_ID, ID FROM aspnet_levelaccess WHERE GroupID = 'LEAVE' AND UserID = @UserID";
                    cmd.Parameters.AddWithValue("@UserID", userId.Trim());
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        string f = reader["Form_ID"]?.ToString()?.Trim() ?? "";
                        string i = reader["ID"]?.ToString()?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(f) && !string.IsNullOrEmpty(i))
                        {
                            selectedSet.Add((f, i));
                        }
                    }
                }

                void MarkSelected(List<LeaveUserAccessItem> list)
                {
                    foreach (var item in list)
                    {
                        if (selectedSet.Contains((item.FormId, item.Id)))
                        {
                            item.IsSelected = true;
                        }
                    }
                }

                MarkSelected(data.Departments);
                MarkSelected(data.Sections);
                MarkSelected(data.Categories);
                MarkSelected(data.Grades);
                MarkSelected(data.Supervisors);
                MarkSelected(data.JobTitles);
                MarkSelected(data.Factories);
                MarkSelected(data.Groups);
                MarkSelected(data.Emails);
            }

            return Ok(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Failed to load user access data", Details = ex.Message });
        }
    }

    [HttpPost("user-access/save")]
    public async Task<IActionResult> SaveUserAccess([FromBody] SaveLeaveUserAccessRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new { Message = "UserId is required." });
        }

        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        try
        {
            using var conn = new MySqlConnection(sqlConnStr);
            await conn.OpenAsync();
            using var trans = await conn.BeginTransactionAsync();

            // Delete existing LEAVE permissions for this user
            using (var delCmd = conn.CreateCommand())
            {
                delCmd.Transaction = trans;
                delCmd.CommandText = "DELETE FROM aspnet_levelaccess WHERE GroupID = 'LEAVE' AND UserID = @UserID";
                delCmd.Parameters.AddWithValue("@UserID", request.UserId.Trim());
                await delCmd.ExecuteNonQueryAsync();
            }

            // Insert newly selected permissions
            if (request.SelectedItems != null && request.SelectedItems.Count > 0)
            {
                foreach (var item in request.SelectedItems)
                {
                    if (string.IsNullOrWhiteSpace(item.FormId) || string.IsNullOrWhiteSpace(item.Id)) continue;
                    using var insCmd = conn.CreateCommand();
                    insCmd.Transaction = trans;
                    insCmd.CommandText = @"
                        INSERT INTO aspnet_levelaccess (GroupID, UserID, Form_ID, ID, Remarks) 
                        VALUES ('LEAVE', @UserID, @FormID, @ID, @Remarks)";
                    insCmd.Parameters.AddWithValue("@UserID", request.UserId.Trim());
                    insCmd.Parameters.AddWithValue("@FormID", item.FormId.Trim());
                    insCmd.Parameters.AddWithValue("@ID", item.Id.Trim());
                    insCmd.Parameters.AddWithValue("@Remarks", item.FormId == "Email_FROM" ? "Email Record" : (item.Remarks ?? ""));
                    await insCmd.ExecuteNonQueryAsync();
                }
            }

            await trans.CommitAsync();
            return Ok(new { Success = true, Message = "User access saved successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Failed to save user access", Details = ex.Message });
        }
    }

    [HttpGet("approval-levels")]
    public async Task<IActionResult> GetApprovalLevels([FromQuery] string? search)
    {
        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        var list = new List<LeaveApprovalLevelRow>();

        try
        {
            using var conn = new MySqlConnection(sqlConnStr);
            await conn.OpenAsync();

            // 1. Fetch user map for resolving ID -> Name
            var userMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var cmdUsers = conn.CreateCommand())
            {
                cmdUsers.CommandText = "SELECT ID, Name FROM aspnet_users WHERE IsActive = 'Y' ORDER BY ID";
                using var readerUsers = await cmdUsers.ExecuteReaderAsync();
                while (await readerUsers.ReadAsync())
                {
                    string id = readerUsers["ID"]?.ToString()?.Trim() ?? "";
                    string name = readerUsers["Name"]?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(id)) userMap[id] = name;
                }
            }

            // 2. Fetch joined users & leave applevel
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT 
                        u.ID, 
                        u.Name, 
                        u.superAdminLeave,
                        u.leave_remind_follow_general,
                        u.leave_remind_time_individual,
                        u.leave_email_remind,
                        a.app1, a.app2, a.app3, a.app4, a.appNo, a.applevel,
                        a.app1_individual, a.app2_individual, a.app3_individual, a.app4_individual, a.appNo_individual, a.applevel_individual,
                        a.UseIndividual
                    FROM aspnet_users u
                    LEFT JOIN (SELECT * FROM aspnet_applevel WHERE GroupID = 'LEAVE') a ON u.ID = a.ID
                    WHERE u.IsActive = 'Y'
                    ORDER BY u.ID";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string id = reader["ID"]?.ToString()?.Trim() ?? "";
                    string name = reader["Name"]?.ToString()?.Trim() ?? "";

                    string a1 = reader["app1"]?.ToString()?.Trim() ?? "";
                    string a2 = reader["app2"]?.ToString()?.Trim() ?? "";
                    string a3 = reader["app3"]?.ToString()?.Trim() ?? "";
                    string a4 = reader["app4"]?.ToString()?.Trim() ?? "";

                    string a1_inv = reader["app1_individual"]?.ToString()?.Trim() ?? "";
                    string a2_inv = reader["app2_individual"]?.ToString()?.Trim() ?? "";
                    string a3_inv = reader["app3_individual"]?.ToString()?.Trim() ?? "";
                    string a4_inv = reader["app4_individual"]?.ToString()?.Trim() ?? "";

                    int appLevel = 1;
                    if (int.TryParse(reader["applevel"]?.ToString(), out int parsedLevel) && parsedLevel >= 1 && parsedLevel <= 4)
                        appLevel = parsedLevel;

                    int appLevel_inv = 1;
                    if (int.TryParse(reader["applevel_individual"]?.ToString(), out int parsedLevelInv) && parsedLevelInv >= 1 && parsedLevelInv <= 4)
                        appLevel_inv = parsedLevelInv;

                    list.Add(new LeaveApprovalLevelRow
                    {
                        ID = id,
                        Name = name,
                        App1 = a1,
                        AppName1 = userMap.TryGetValue(a1, out var n1) ? n1 : "",
                        App2 = a2,
                        AppName2 = userMap.TryGetValue(a2, out var n2) ? n2 : "",
                        App3 = a3,
                        AppName3 = userMap.TryGetValue(a3, out var n3) ? n3 : "",
                        App4 = a4,
                        AppName4 = userMap.TryGetValue(a4, out var n4) ? n4 : "",
                        AppLevel = appLevel,

                        SuperAdminLeave = reader["superAdminLeave"]?.ToString()?.Trim() ?? "N",
                        LeaveRemindFollowGeneral = reader["leave_remind_follow_general"]?.ToString()?.Trim() ?? "Y",
                        LeaveEmailRemind = reader["leave_email_remind"]?.ToString()?.Trim() ?? "N",
                        LeaveRemindTimeIndividual = reader["leave_remind_time_individual"]?.ToString()?.Trim() ?? "00:00:00",

                        UseIndividual = reader["UseIndividual"]?.ToString()?.Trim() ?? "N",
                        App1_Individual = a1_inv,
                        AppName1_Individual = userMap.TryGetValue(a1_inv, out var n1i) ? n1i : "",
                        App2_Individual = a2_inv,
                        AppName2_Individual = userMap.TryGetValue(a2_inv, out var n2i) ? n2i : "",
                        App3_Individual = a3_inv,
                        AppName3_Individual = userMap.TryGetValue(a3_inv, out var n3i) ? n3i : "",
                        App4_Individual = a4_inv,
                        AppName4_Individual = userMap.TryGetValue(a4_inv, out var n4i) ? n4i : "",
                        AppLevel_Individual = appLevel_inv
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string q = search.Trim();
                list = list.Where(x =>
                    x.ID.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.App1.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.App2.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.App1_Individual.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.App2_Individual.Contains(q, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Failed to load approval levels", Details = ex.Message });
        }
    }

    [HttpPost("approval-levels/save")]
    public async Task<IActionResult> SaveApprovalLevel([FromBody] UpdateLeaveApprovalLevelRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.ID))
        {
            return BadRequest(new { Message = "User ID is required." });
        }

        int appNo = 0;
        if (!string.IsNullOrEmpty(req.App4)) appNo = 4;
        else if (!string.IsNullOrEmpty(req.App3)) appNo = 3;
        else if (!string.IsNullOrEmpty(req.App2)) appNo = 2;
        else if (!string.IsNullOrEmpty(req.App1)) appNo = 1;

        int appNo_inv = 0;
        if (!string.IsNullOrEmpty(req.App4_Individual)) appNo_inv = 4;
        else if (!string.IsNullOrEmpty(req.App3_Individual)) appNo_inv = 3;
        else if (!string.IsNullOrEmpty(req.App2_Individual)) appNo_inv = 2;
        else if (!string.IsNullOrEmpty(req.App1_Individual)) appNo_inv = 1;

        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";

        try
        {
            using var conn = new MySqlConnection(sqlConnStr);
            await conn.OpenAsync();
            using var trans = await conn.BeginTransactionAsync();

            // 1. Delete existing aspnet_applevel where ID = @ID AND GroupID = 'LEAVE'
            using (var delCmd = conn.CreateCommand())
            {
                delCmd.Transaction = trans;
                delCmd.CommandText = "DELETE FROM aspnet_applevel WHERE ID = @ID AND GroupID = 'LEAVE'";
                delCmd.Parameters.AddWithValue("@ID", req.ID.Trim());
                await delCmd.ExecuteNonQueryAsync();
            }

            // 2. Insert updated record into aspnet_applevel
            using (var insCmd = conn.CreateCommand())
            {
                insCmd.Transaction = trans;
                insCmd.CommandText = @"
                    INSERT INTO aspnet_applevel 
                    (ID, GroupID, app1, app2, app3, app4, appNo, applevel, 
                     app1_individual, app2_individual, app3_individual, app4_individual, 
                     appNo_individual, applevel_individual, UseIndividual, superAdminLeave, 
                     CreatedBy, CreatedDate)
                    VALUES 
                    (@ID, 'LEAVE', @app1, @app2, @app3, @app4, @appNo, @applevel, 
                     @app1_inv, @app2_inv, @app3_inv, @app4_inv, 
                     @appNo_inv, @applevel_inv, @UseIndividual, @superAdminLeave, 
                     'Admin', NOW())";

                insCmd.Parameters.AddWithValue("@ID", req.ID.Trim());
                insCmd.Parameters.AddWithValue("@app1", req.App1?.Trim() ?? "");
                insCmd.Parameters.AddWithValue("@app2", req.App2?.Trim() ?? "");
                insCmd.Parameters.AddWithValue("@app3", req.App3?.Trim() ?? "");
                insCmd.Parameters.AddWithValue("@app4", req.App4?.Trim() ?? "");
                insCmd.Parameters.AddWithValue("@appNo", appNo.ToString());
                insCmd.Parameters.AddWithValue("@applevel", req.AppLevel);

                insCmd.Parameters.AddWithValue("@app1_inv", req.App1_Individual?.Trim() ?? "");
                insCmd.Parameters.AddWithValue("@app2_inv", req.App2_Individual?.Trim() ?? "");
                insCmd.Parameters.AddWithValue("@app3_inv", req.App3_Individual?.Trim() ?? "");
                insCmd.Parameters.AddWithValue("@app4_inv", req.App4_Individual?.Trim() ?? "");
                insCmd.Parameters.AddWithValue("@appNo_inv", appNo_inv.ToString());
                insCmd.Parameters.AddWithValue("@applevel_inv", req.AppLevel_Individual);

                insCmd.Parameters.AddWithValue("@UseIndividual", req.UseIndividual == "Y" ? "Y" : "N");
                insCmd.Parameters.AddWithValue("@superAdminLeave", req.SuperAdminLeave == "Y" ? "Y" : "N");

                await insCmd.ExecuteNonQueryAsync();
            }

            // 3. Update aspnet_users with SuperAdmin and Reminder preferences
            using (var updUserCmd = conn.CreateCommand())
            {
                updUserCmd.Transaction = trans;
                updUserCmd.CommandText = @"
                    UPDATE aspnet_users 
                    SET superAdminLeave = @superAdminLeave,
                        leave_remind_follow_general = @remindGeneral,
                        leave_email_remind = @emailRemind,
                        leave_remind_time_individual = @remindTime
                    WHERE ID = @ID";

                updUserCmd.Parameters.AddWithValue("@ID", req.ID.Trim());
                updUserCmd.Parameters.AddWithValue("@superAdminLeave", req.SuperAdminLeave == "Y" ? "Y" : "N");
                updUserCmd.Parameters.AddWithValue("@remindGeneral", req.LeaveRemindFollowGeneral == "Y" ? "Y" : "N");
                updUserCmd.Parameters.AddWithValue("@emailRemind", req.LeaveEmailRemind == "Y" ? "Y" : "N");
                updUserCmd.Parameters.AddWithValue("@remindTime", string.IsNullOrWhiteSpace(req.LeaveRemindTimeIndividual) ? "00:00:00" : req.LeaveRemindTimeIndividual.Trim());

                await updUserCmd.ExecuteNonQueryAsync();
            }

            await trans.CommitAsync();
            return Ok(new { Success = true, Message = $"Approval level configuration for user '{req.ID}' updated successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Failed to save approval level", Details = ex.Message });
        }
    }

    [HttpGet("maintenance-groups")]
    public async Task<IActionResult> GetLeaveMaintenanceGroups()
    {
        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        var list = new List<LeaveUserGroupOption>();

        try
        {
            using var conn = new MySqlConnection(sqlConnStr);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT UserGroupID, UserGroup FROM aspnet_usergroup WHERE GroupID = 'LEAVE' ORDER BY UserGroupID";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new LeaveUserGroupOption
                {
                    UserGroupID = reader["UserGroupID"]?.ToString()?.Trim() ?? "",
                    UserGroup = reader["UserGroup"]?.ToString()?.Trim() ?? ""
                });
            }
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Failed to load leave user groups", Details = ex.Message });
        }
    }

    [HttpGet("maintenance")]
    public async Task<IActionResult> GetLeaveMaintenanceData([FromQuery] string? userGroupId)
    {
        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        string odbcConnStr = _configuration.GetConnectionString("ConnectionTMS") ?? "";
        string tmsMySqlStr = _configuration.GetConnectionString("ConnectionTMS_MySql") ?? "";

        var result = new LeaveMaintenanceData();

        try
        {
            using var conn = new MySqlConnection(sqlConnStr);
            await conn.OpenAsync();

            // 1. Load User Groups (GroupID = 'LEAVE')
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT UserGroupID, UserGroup FROM aspnet_usergroup WHERE GroupID = 'LEAVE' ORDER BY UserGroupID";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.UserGroups.Add(new LeaveUserGroupOption
                    {
                        UserGroupID = reader["UserGroupID"]?.ToString()?.Trim() ?? "",
                        UserGroup = reader["UserGroup"]?.ToString()?.Trim() ?? ""
                    });
                }
            }

            // Determine effective userGroupId
            string effectiveGroupId = userGroupId?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(effectiveGroupId) && result.UserGroups.Count > 0)
            {
                effectiveGroupId = result.UserGroups[0].UserGroupID;
            }

            // 2. Load active users lookup for approver selection and name resolving
            var usersMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT ID, Name FROM aspnet_users WHERE (IsActive = 'Y' OR IsActive IS NULL OR IsActive = '') ORDER BY ID";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string id = reader["ID"]?.ToString()?.Trim() ?? "";
                    string name = reader["Name"]?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(id))
                    {
                        usersMap[id] = name;
                        result.Users.Add(new EmployeeLookupModel { ID = id, Name = name });
                    }
                }
            }

            // 3. Load Available Leave Types from dw_leave (ODBC -> TMS MySQL -> fallback aspnet_leaveid)
            var availableLeaves = new List<LeaveTypeLookup>();
            bool loadedFromTms = false;

            // Try TMS ODBC
            if (!string.IsNullOrWhiteSpace(odbcConnStr))
            {
                try
                {
                    using var odbcConn = new OdbcConnection(odbcConnStr);
                    await odbcConn.OpenAsync();
                    using var cmd = odbcConn.CreateCommand();
                    cmd.CommandText = "SELECT Leave_ID, Name FROM dw_leave ORDER BY Leave_ID";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string code = reader["Leave_ID"]?.ToString()?.Trim() ?? "";
                        string name = reader["Name"]?.ToString()?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(code))
                        {
                            availableLeaves.Add(new LeaveTypeLookup { Code = code, Name = name });
                        }
                    }
                    if (availableLeaves.Count > 0) loadedFromTms = true;
                }
                catch { /* Ignore and fallback */ }
            }

            // Try TMS MySql direct if not loaded
            if (!loadedFromTms && !string.IsNullOrWhiteSpace(tmsMySqlStr))
            {
                try
                {
                    using var tmsConn = new MySqlConnection(tmsMySqlStr);
                    await tmsConn.OpenAsync();
                    using var cmd = tmsConn.CreateCommand();
                    cmd.CommandText = "SELECT Leave_ID, Name FROM dw_leave ORDER BY Leave_ID";
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        string code = reader["Leave_ID"]?.ToString()?.Trim() ?? "";
                        string name = reader["Name"]?.ToString()?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(code))
                        {
                            availableLeaves.Add(new LeaveTypeLookup { Code = code, Name = name });
                        }
                    }
                    if (availableLeaves.Count > 0) loadedFromTms = true;
                }
                catch { /* Ignore and fallback */ }
            }

            // Fallback: distinct from aspnet_leaveid in myhrsuitelog
            if (!loadedFromTms || availableLeaves.Count == 0)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT DISTINCT Leave_ID, Name FROM aspnet_leaveid WHERE Leave_ID IS NOT NULL AND Leave_ID <> '' ORDER BY Leave_ID";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string code = reader["Leave_ID"]?.ToString()?.Trim() ?? "";
                    string name = reader["Name"]?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(code) && !availableLeaves.Any(a => a.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
                    {
                        availableLeaves.Add(new LeaveTypeLookup { Code = code, Name = name });
                    }
                }
            }

            result.AvailableLeaveTypes = availableLeaves;

            // 4. Load configured rows from aspnet_leaveid for effectiveGroupId
            var leaveSettings = new Dictionary<string, LeaveMaintenanceRow>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(effectiveGroupId))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT UserGroupID, Leave_ID, Name, Display, BalCount, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday, Restday,
                           IndirectLeaveID, LeaveFirstYearNotAllow, StopHalfDay, followEsystem, compulsory_attach,
                           special_approval, app1, app2, app3, appNo, applevel
                    FROM aspnet_leaveid
                    WHERE UserGroupID = @UserGroupID";
                cmd.Parameters.AddWithValue("@UserGroupID", effectiveGroupId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string leaveId = reader["Leave_ID"]?.ToString()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(leaveId)) continue;

                    var row = new LeaveMaintenanceRow
                    {
                        UserGroupID = effectiveGroupId,
                        Leave_ID = leaveId,
                        Description = reader["Name"]?.ToString()?.Trim() ?? "",
                        Display = reader["Display"]?.ToString()?.Trim() ?? "N",
                        BalCount = reader["BalCount"]?.ToString()?.Trim() ?? "N",
                        IndirectLeaveID = reader["IndirectLeaveID"]?.ToString()?.Trim() ?? "",
                        LeaveFirstYearNotAllow = reader["LeaveFirstYearNotAllow"]?.ToString()?.Trim() ?? "N",
                        StopHalfDay = reader["StopHalfDay"]?.ToString()?.Trim() ?? "N",
                        FollowEsystem = reader["followEsystem"]?.ToString()?.Trim() ?? "N",
                        CompulsoryAttach = reader["compulsory_attach"]?.ToString()?.Trim() ?? "N",
                        Monday = reader["Monday"]?.ToString()?.Trim() ?? "N",
                        Tuesday = reader["Tuesday"]?.ToString()?.Trim() ?? "N",
                        Wednesday = reader["Wednesday"]?.ToString()?.Trim() ?? "N",
                        Thursday = reader["Thursday"]?.ToString()?.Trim() ?? "N",
                        Friday = reader["Friday"]?.ToString()?.Trim() ?? "N",
                        Saturday = reader["Saturday"]?.ToString()?.Trim() ?? "N",
                        Sunday = reader["Sunday"]?.ToString()?.Trim() ?? "N",
                        Restday = reader["Restday"]?.ToString()?.Trim() ?? "N",
                        SpecialApproval = reader["special_approval"]?.ToString()?.Trim() ?? "N",
                        App1 = reader["app1"]?.ToString()?.Trim() ?? "",
                        App2 = reader["app2"]?.ToString()?.Trim() ?? "",
                        App3 = reader["app3"]?.ToString()?.Trim() ?? "",
                        AppNo = reader["appNo"]?.ToString()?.Trim() ?? "0",
                        AppLevel = reader["applevel"] != DBNull.Value ? Convert.ToInt32(reader["applevel"]) : 1
                    };

                    row.App1Name = usersMap.TryGetValue(row.App1, out var n1) ? n1 : row.App1;
                    row.App2Name = usersMap.TryGetValue(row.App2, out var n2) ? n2 : row.App2;
                    row.App3Name = usersMap.TryGetValue(row.App3, out var n3) ? n3 : row.App3;

                    // Build formatted IncludeDay string matching BLL
                    var dayParts = new List<string>();
                    if (row.Monday == "Y") dayParts.Add("Mon");
                    if (row.Tuesday == "Y") dayParts.Add("Tues");
                    if (row.Wednesday == "Y") dayParts.Add("Wed");
                    if (row.Thursday == "Y") dayParts.Add("Thur");
                    if (row.Friday == "Y") dayParts.Add("Fri");
                    if (row.Saturday == "Y") dayParts.Add("Sat");
                    if (row.Sunday == "Y") dayParts.Add("Sun");
                    if (row.Restday == "Y") dayParts.Add("Holiday");
                    row.IncludeDay = string.Join(", ", dayParts);

                    leaveSettings[leaveId] = row;
                }
            }

            // 5. Merge all available leave types with settings for this group (like legacy BLL)
            foreach (var lt in availableLeaves)
            {
                if (leaveSettings.TryGetValue(lt.Code, out var existingRow))
                {
                    if (string.IsNullOrEmpty(existingRow.Description))
                    {
                        existingRow.Description = lt.Name;
                    }
                    result.Rows.Add(existingRow);
                }
                else
                {
                    // New/Unconfigured row for this group
                    result.Rows.Add(new LeaveMaintenanceRow
                    {
                        UserGroupID = effectiveGroupId,
                        Leave_ID = lt.Code,
                        Description = lt.Name,
                        Display = "N",
                        BalCount = "N",
                        Monday = "N",
                        Tuesday = "N",
                        Wednesday = "N",
                        Thursday = "N",
                        Friday = "N",
                        Saturday = "N",
                        Sunday = "N",
                        Restday = "N",
                        IncludeDay = "",
                        LeaveFirstYearNotAllow = "N",
                        StopHalfDay = "N",
                        FollowEsystem = "N",
                        CompulsoryAttach = "N",
                        SpecialApproval = "N",
                        App1 = "",
                        App2 = "",
                        App3 = "",
                        AppNo = "0",
                        AppLevel = 1
                    });
                }
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Failed to load leave maintenance data", Details = ex.Message });
        }
    }

    [HttpPost("maintenance/save")]
    public async Task<IActionResult> SaveLeaveMaintenance([FromBody] UpdateLeaveMaintenanceRequest req)
    {
        if (req == null)
            return BadRequest(new { Message = "Request cannot be empty" });

        if (string.IsNullOrWhiteSpace(req.UserGroupID))
            return BadRequest(new { Message = "User Group ID is required." });

        if (string.IsNullOrWhiteSpace(req.Leave_ID))
            return BadRequest(new { Message = "Leave ID is required." });

        string app1 = req.App1?.Trim() ?? "";
        string app2 = req.App2?.Trim() ?? "";
        string app3 = req.App3?.Trim() ?? "";
        int level = req.AppLevel ?? 1;

        // Validation rules matching eLeaveMaintenance.aspx.vb
        if (req.SpecialApproval == "Y")
        {
            if (level == 1 || level == 2)
            {
                if (string.IsNullOrEmpty(app1) && !string.IsNullOrEmpty(app2))
                    return BadRequest(new { Message = "Error! App1 must fill in before App2." });

                if (string.IsNullOrEmpty(app2) && !string.IsNullOrEmpty(app3))
                    return BadRequest(new { Message = "Error! App2 must fill in before App3." });

                if (!string.IsNullOrEmpty(app1) && !string.IsNullOrEmpty(app2) && app1.Equals(app2, StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { Message = "Error! App1 and App2 not allow same person." });

                if (!string.IsNullOrEmpty(app1) && !string.IsNullOrEmpty(app3) && app1.Equals(app3, StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { Message = "Error! App1 and App3 not allow same person." });

                if (!string.IsNullOrEmpty(app2) && !string.IsNullOrEmpty(app3) && app2.Equals(app3, StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { Message = "Error! App2 and App3 not allow same person." });
            }
            else if (level == 3)
            {
                if (string.IsNullOrEmpty(app1) && string.IsNullOrEmpty(app2))
                    return BadRequest(new { Message = "Error! You must select App1 or App2." });

                if (string.IsNullOrEmpty(app3))
                    return BadRequest(new { Message = "Error! App3 is compulsory approval and not allow empty." });

                if (!string.IsNullOrEmpty(app1) && !string.IsNullOrEmpty(app2) && app1.Equals(app2, StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { Message = "Error! App1 and App2 not allow same person." });

                if (!string.IsNullOrEmpty(app1) && !string.IsNullOrEmpty(app3) && app1.Equals(app3, StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { Message = "Error! App1 and App3 not allow same person." });

                if (!string.IsNullOrEmpty(app2) && !string.IsNullOrEmpty(app3) && app2.Equals(app3, StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { Message = "Error! App2 and App3 not allow same person." });
            }
        }

        // Indirect leave ID validation: not allow equal to self
        string indirect = req.IndirectLeaveID?.Trim() ?? "";
        if (!string.IsNullOrEmpty(indirect) && indirect.Equals(req.Leave_ID.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { Message = $"Error! Indirect leave ID not allow equal to Leave ID: {req.Leave_ID}." });
        }

        // Compute AppNo
        string appNo = "0";
        if (!string.IsNullOrEmpty(app3)) appNo = "3";
        else if (!string.IsNullOrEmpty(app2)) appNo = "2";
        else if (!string.IsNullOrEmpty(app1)) appNo = "1";

        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";

        try
        {
            using var conn = new MySqlConnection(sqlConnStr);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO aspnet_leaveid (
                    UserGroupID, Leave_ID, Name, Display, BalCount,
                    Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday, Restday,
                    LeaveFirstYearNotAllow, StopHalfDay, followEsystem, compulsory_attach,
                    IndirectLeaveID, special_approval, app1, app2, app3, appNo, applevel
                ) VALUES (
                    @UserGroupID, @Leave_ID, @Name, @Display, @BalCount,
                    @Monday, @Tuesday, @Wednesday, @Thursday, @Friday, @Saturday, @Sunday, @Restday,
                    @LeaveFirstYearNotAllow, @StopHalfDay, @followEsystem, @compulsory_attach,
                    @IndirectLeaveID, @special_approval, @app1, @app2, @app3, @appNo, @applevel
                ) ON DUPLICATE KEY UPDATE
                    Name = VALUES(Name),
                    Display = VALUES(Display),
                    BalCount = VALUES(BalCount),
                    Monday = VALUES(Monday),
                    Tuesday = VALUES(Tuesday),
                    Wednesday = VALUES(Wednesday),
                    Thursday = VALUES(Thursday),
                    Friday = VALUES(Friday),
                    Saturday = VALUES(Saturday),
                    Sunday = VALUES(Sunday),
                    Restday = VALUES(Restday),
                    LeaveFirstYearNotAllow = VALUES(LeaveFirstYearNotAllow),
                    StopHalfDay = VALUES(StopHalfDay),
                    followEsystem = VALUES(followEsystem),
                    compulsory_attach = VALUES(compulsory_attach),
                    IndirectLeaveID = VALUES(IndirectLeaveID),
                    special_approval = VALUES(special_approval),
                    app1 = VALUES(app1),
                    app2 = VALUES(app2),
                    app3 = VALUES(app3),
                    appNo = VALUES(appNo),
                    applevel = VALUES(applevel);";

            cmd.Parameters.AddWithValue("@UserGroupID", req.UserGroupID.Trim());
            cmd.Parameters.AddWithValue("@Leave_ID", req.Leave_ID.Trim());
            cmd.Parameters.AddWithValue("@Name", req.Description?.Trim() ?? req.Leave_ID.Trim());
            cmd.Parameters.AddWithValue("@Display", req.Display == "Y" ? "Y" : "N");
            cmd.Parameters.AddWithValue("@BalCount", req.BalCount == "Y" ? "Y" : "N");
            cmd.Parameters.AddWithValue("@Monday", req.Monday == "Y" ? "Y" : "N");
            cmd.Parameters.AddWithValue("@Tuesday", req.Tuesday == "Y" ? "Y" : "N");
            cmd.Parameters.AddWithValue("@Wednesday", req.Wednesday == "Y" ? "Y" : "N");
            cmd.Parameters.AddWithValue("@Thursday", req.Thursday == "Y" ? "Y" : "N");
            cmd.Parameters.AddWithValue("@Friday", req.Friday == "Y" ? "Y" : "N");
            cmd.Parameters.AddWithValue("@Saturday", req.Saturday == "Y" ? "Y" : "N");
            cmd.Parameters.AddWithValue("@Sunday", req.Sunday == "Y" ? "Y" : "N");
            cmd.Parameters.AddWithValue("@Restday", req.Restday == "Y" ? "Y" : "N");
            cmd.Parameters.AddWithValue("@LeaveFirstYearNotAllow", req.LeaveFirstYearNotAllow == "Y" ? "Y" : "N");
            cmd.Parameters.AddWithValue("@StopHalfDay", req.StopHalfDay == "Y" ? "Y" : "N");
            cmd.Parameters.AddWithValue("@followEsystem", req.FollowEsystem == "Y" ? "Y" : "N");
            cmd.Parameters.AddWithValue("@compulsory_attach", req.CompulsoryAttach == "Y" ? "Y" : "N");
            cmd.Parameters.AddWithValue("@IndirectLeaveID", string.IsNullOrWhiteSpace(indirect) ? (object)DBNull.Value : indirect);
            cmd.Parameters.AddWithValue("@special_approval", req.SpecialApproval == "Y" ? "Y" : "N");
            cmd.Parameters.AddWithValue("@app1", string.IsNullOrWhiteSpace(app1) ? (object)DBNull.Value : app1);
            cmd.Parameters.AddWithValue("@app2", string.IsNullOrWhiteSpace(app2) ? (object)DBNull.Value : app2);
            cmd.Parameters.AddWithValue("@app3", string.IsNullOrWhiteSpace(app3) ? (object)DBNull.Value : app3);
            cmd.Parameters.AddWithValue("@appNo", appNo);
            cmd.Parameters.AddWithValue("@applevel", level);

            await cmd.ExecuteNonQueryAsync();

            return Ok(new { Success = true, Message = $"Leave configuration for '{req.Leave_ID}' ({req.UserGroupID}) saved successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Failed to save leave maintenance configuration", Details = ex.Message });
        }
    }

    // ==========================================
    // LEAVE DOCUMENT RULES (aspnet_rules)
    // ==========================================

    [HttpGet("doc-rules")]
    public async Task<IActionResult> GetLeaveDocRules([FromQuery] string group = "LEAVE")
    {
        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        var list = new List<LeaveDocRuleRow>();

        try
        {
            using var conn = new MySqlConnection(sqlConnStr);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT RuleID, `Condition`, `Action`, RouteID, GroupID, `explain`, Approval_allow
                FROM aspnet_rules
                WHERE GroupID = @GroupID
                ORDER BY RuleID ASC;";
            cmd.Parameters.AddWithValue("@GroupID", group);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new LeaveDocRuleRow
                {
                    RuleID = reader.GetInt32(0),
                    Condition = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Action = reader.IsDBNull(2) ? "N" : reader.GetString(2),
                    RouteID = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    GroupID = reader.IsDBNull(4) ? "LEAVE" : reader.GetString(4),
                    Explain = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    ApprovalAllow = reader.IsDBNull(6) ? null : reader.GetString(6)
                });
            }

            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Failed to load leave document rules", Details = ex.Message });
        }
    }

    [HttpPost("doc-rules/save")]
    public async Task<IActionResult> SaveLeaveDocRule([FromBody] SaveLeaveDocRuleRequest req)
    {
        if (req == null)
            return BadRequest(new { Message = "Invalid request payload." });

        if (string.IsNullOrWhiteSpace(req.Condition))
            return BadRequest(new { Message = "Condition cannot be empty." });

        string group = string.IsNullOrWhiteSpace(req.GroupID) ? "LEAVE" : req.GroupID.Trim();
        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";

        try
        {
            using var conn = new MySqlConnection(sqlConnStr);
            await conn.OpenAsync();

            int ruleId = req.RuleID;

            if (req.IsNew && ruleId <= 0)
            {
                using var cmdMax = conn.CreateCommand();
                cmdMax.CommandText = "SELECT COALESCE(MAX(RuleID), 0) + 1 FROM aspnet_rules WHERE GroupID = @GroupID;";
                cmdMax.Parameters.AddWithValue("@GroupID", group);
                ruleId = Convert.ToInt32(await cmdMax.ExecuteScalarAsync());
            }

            if (!req.IsNew && req.OriginalRuleID.HasValue && req.OriginalRuleID.Value != ruleId)
            {
                using var cmdDel = conn.CreateCommand();
                cmdDel.CommandText = "DELETE FROM aspnet_rules WHERE RuleID = @OrigRuleID AND GroupID = @GroupID;";
                cmdDel.Parameters.AddWithValue("@OrigRuleID", req.OriginalRuleID.Value);
                cmdDel.Parameters.AddWithValue("@GroupID", group);
                await cmdDel.ExecuteNonQueryAsync();
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO aspnet_rules (RuleID, `Condition`, `Action`, RouteID, GroupID, `explain`, Approval_allow)
                VALUES (@RuleID, @Condition, @Action, @RouteID, @GroupID, @Explain, @ApprovalAllow)
                ON DUPLICATE KEY UPDATE
                    `Condition` = VALUES(`Condition`),
                    `Action` = VALUES(`Action`),
                    RouteID = VALUES(RouteID),
                    `explain` = VALUES(`explain`),
                    Approval_allow = VALUES(Approval_allow);";

            cmd.Parameters.AddWithValue("@RuleID", ruleId);
            cmd.Parameters.AddWithValue("@Condition", req.Condition?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@Action", req.Action?.Trim() ?? "N");
            cmd.Parameters.AddWithValue("@RouteID", req.RouteID?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@GroupID", group);
            cmd.Parameters.AddWithValue("@Explain", req.Explain?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@ApprovalAllow", string.IsNullOrWhiteSpace(req.ApprovalAllow) ? (object)DBNull.Value : req.ApprovalAllow.Trim());

            await cmd.ExecuteNonQueryAsync();

            return Ok(new { Success = true, Message = $"Rule #{ruleId} saved successfully.", RuleID = ruleId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Failed to save leave document rule", Details = ex.Message });
        }
    }

    [HttpPost("doc-rules/delete")]
    public async Task<IActionResult> DeleteLeaveDocRules([FromBody] DeleteLeaveDocRulesRequest req)
    {
        if (req == null || req.RuleIDs == null || req.RuleIDs.Count == 0)
            return BadRequest(new { Message = "No rule IDs specified for deletion." });

        string group = string.IsNullOrWhiteSpace(req.GroupID) ? "LEAVE" : req.GroupID.Trim();
        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";

        try
        {
            using var conn = new MySqlConnection(sqlConnStr);
            await conn.OpenAsync();

            var paramNames = new List<string>();
            using var cmd = conn.CreateCommand();
            cmd.Parameters.AddWithValue("@GroupID", group);

            for (int i = 0; i < req.RuleIDs.Count; i++)
            {
                string p = $"@id{i}";
                paramNames.Add(p);
                cmd.Parameters.AddWithValue(p, req.RuleIDs[i]);
            }

            cmd.CommandText = $"DELETE FROM aspnet_rules WHERE GroupID = @GroupID AND RuleID IN ({string.Join(", ", paramNames)});";
            int affected = await cmd.ExecuteNonQueryAsync();

            return Ok(new { Success = true, Message = $"{affected} rule(s) deleted successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Failed to delete leave document rules", Details = ex.Message });
        }
    }
}

public class LeaveApplyModel
{
    public string EmpId { get; set; } = string.Empty;
    public string LeaveCode { get; set; } = string.Empty;
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string LeaveType { get; set; } = "FULL"; // FULL, 1st Half, 2nd Half
    public string Reason { get; set; } = string.Empty;
}

public class ApproveActionModel
{
    public string LeaveNo { get; set; } = string.Empty;
    public string EmpId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // APPROVE or REJECT
    public string Remarks { get; set; } = string.Empty;
    public string SupervisorId { get; set; } = string.Empty;
}
