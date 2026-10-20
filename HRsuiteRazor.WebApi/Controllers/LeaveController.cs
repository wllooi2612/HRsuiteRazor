using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Data.Odbc;
using System.Collections.Generic;
using System.Text;
using Hrsuite.WebApi.Models;

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

            // 3. Query allowed leave IDs from SQL Server aspnet_LeaveID
            var allowedLeaves = new List<(string LeaveID, string Display, string BalCount, string IndirectLeaveID)>();
            using (var conn = new MySqlConnection(sqlConnStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT LEAVE_ID, Display, BalCount, IndirectLeaveID FROM aspnet_LeaveID WHERE UserGroupID = @GroupID AND Display = 'Y'";
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
            using (var connMy = new OdbcConnection(mysqlConnStr))
            {
                connSql.Open();
                connMy.Open();

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
                    if (!string.IsNullOrEmpty(cdlvepay))
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

                    // Count pending from ASPNET_leaveRecord & ASPNET_Pre_LeaveRecord
                    decimal pending = 0;
                    using (var cmd = connSql.CreateCommand())
                    {
                        cmd.CommandText = "SELECT LEAVE_TYPE, COUNT(*) as CountVal FROM ASPNET_leaveRecord WHERE LEAVE_ID = @LeaveID AND EMP_ID = @EmpID AND LEAVE_STATUS = 'PENDING' GROUP BY LEAVE_TYPE";
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
                            cmd.CommandText = "SELECT LEAVE_TYPE, COUNT(*) as CountVal FROM ASPNET_Pre_LeaveRecord WHERE LEAVE_ID = @LeaveID AND EMP_ID = @EmpID AND LEAVE_STATUS = 'PENDING' GROUP BY LEAVE_TYPE";
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
                            cmd.CommandText = "SELECT LEAVE_ID FROM aspnet_LeaveID WHERE UserGroupID = @GroupID AND IndirectLeaveID = @IndirectID AND Display = 'Y' AND LEAVE_ID <> @IndirectID AND IndirectLeaveID <> LEAVE_ID";
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
                            if (mysqlLeaves.TryGetValue(indId, out var indMw) && !string.IsNullOrEmpty(indMw.Cdlvepay))
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
                        }
                    }

                    // Calculate indirect pending
                    decimal pendingIndirect = 0;
                    if (!string.IsNullOrEmpty(item.IndirectLeaveID))
                    {
                        var indirectAllIds = new List<string>();
                        using (var cmd = connSql.CreateCommand())
                        {
                            cmd.CommandText = "SELECT LEAVE_ID FROM aspnet_LeaveID WHERE UserGroupID = @GroupID AND IndirectLeaveID = @IndirectID AND Display = 'Y'";
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
                                cmd.CommandText = "SELECT LEAVE_TYPE, COUNT(*) as CountVal FROM ASPNET_leaveRecord WHERE LEAVE_ID = @LeaveID AND EMP_ID = @EmpID AND LEAVE_STATUS = 'PENDING' GROUP BY LEAVE_TYPE";
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
                    if (item.LeaveID.Equals(replacementLeaveId, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(cdlvepay))
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

            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error retrieving leave balances.", Details = ex.Message });
        }
    }

    [HttpGet("history")]
    public IActionResult GetLeaveHistory([FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(new { Message = "UserId is required." });
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
                    // Group individual day records into logical leave requests
                    cmd.CommandText = @"
                        SELECT r.LeaveNo, r.Emp_ID, MAX(u.Name) AS EmpName, MAX(r.Leave_ID) AS Leave_ID, 
                               MIN(r.Leave_Date) AS DateFrom, MAX(r.Leave_Date) AS DateTo, 
                               r.Leave_Status, MAX(r.leave_reason) AS Leave_Reason, MAX(r.applevel) AS AppLevel
                        FROM ASPNET_leaveRecord r
                        LEFT JOIN aspnet_users u ON r.Emp_ID = u.ID
                        WHERE r.Emp_ID = @EmpID
                        GROUP BY r.LeaveNo, r.Emp_ID, r.Leave_Status
                        ORDER BY MIN(r.Leave_Date) DESC";
                    cmd.Parameters.AddWithValue("@EmpID", userId);

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
            return StatusCode(500, new { Message = "SQL Server database error.", Details = ex.Message });
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
                        FROM ASPNET_leaveRecord r
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
                            INSERT INTO ASPNET_leaveRecord (
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
            string newStatus = model.Action.ToUpper() == "APPROVE" ? "APPROVE" : "REJECT";

            using (var conn = new MySqlConnection(sqlConnStr))
            {
                conn.Open();
                
                // Fetch leave request details to copy to MySQL upon approval
                string leaveId = "";
                DateTime dateFrom = DateTime.Today;
                DateTime dateTo = DateTime.Today;
                string leaveType = "FULL";

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT LEAVE_ID, dateLeavefrom, dateLeaveto, LEAVE_TYPE FROM ASPNET_leaveRecord WHERE LeaveNo = @LeaveNo AND Emp_ID = @EmpID LIMIT 1";
                    cmd.Parameters.AddWithValue("@LeaveNo", model.LeaveNo);
                    cmd.Parameters.AddWithValue("@EmpID", model.EmpId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            leaveId = reader["LEAVE_ID"]?.ToString() ?? "";
                            dateFrom = Convert.ToDateTime(reader["dateLeavefrom"]);
                            dateTo = Convert.ToDateTime(reader["dateLeaveto"]);
                            leaveType = reader["LEAVE_TYPE"]?.ToString() ?? "FULL";
                        }
                    }
                }

                // Update SQL Server status
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        UPDATE ASPNET_leaveRecord 
                        SET LEAVE_STATUS = @Status, 
                            dateapp1 = NOW(), 
                            app1Pass = 'YES' 
                        WHERE LeaveNo = @LeaveNo AND Emp_ID = @EmpID";
                    cmd.Parameters.AddWithValue("@Status", newStatus);
                    cmd.Parameters.AddWithValue("@LeaveNo", model.LeaveNo);
                    cmd.Parameters.AddWithValue("@EmpID", model.EmpId);
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
            }

            return Ok(new { Message = $"Leave request successfully {(newStatus == "APPROVE" ? "approved" : "rejected")}." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error updating leave request status.", Details = ex.Message });
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
}
