using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Data.Odbc;
using System.Collections.Generic;
using Hrsuite.WebApi.Models;

namespace Hrsuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OTController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public OTController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("types")]
    public async Task<IActionResult> GetOTTypes()
    {
        string mysqlConnStr = _configuration.GetConnectionString("ConnectionTMS") ?? "";
        var list = new List<OTType>();

        try
        {
            if (!string.IsNullOrEmpty(mysqlConnStr))
            {
                using (var conn = new OdbcConnection(mysqlConnStr))
                {
                    await conn.OpenAsync();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT OT_ID, Name, ot_rate FROM dw_ot";
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string code = reader["OT_ID"]?.ToString()?.Trim() ?? "";
                                string name = reader["Name"]?.ToString()?.Trim() ?? "";
                                double rate = 1.0;
                                if (double.TryParse(reader["ot_rate"]?.ToString(), out double parsed))
                                {
                                    rate = parsed;
                                }

                                if (!string.IsNullOrEmpty(code))
                                {
                                    list.Add(new OTType { Code = code, Name = name, Multiplier = rate });
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetOTTypes warning: {ex.Message}");
        }

        // Fallback standard OT types if empty
        if (list.Count == 0)
        {
            list.Add(new OTType { Code = "1.0", Name = "Normal Overtime (1.0x)", Multiplier = 1.0 });
            list.Add(new OTType { Code = "1.5", Name = "Weekday Overtime (1.5x)", Multiplier = 1.5 });
            list.Add(new OTType { Code = "2.0", Name = "Rest Day / Public Holiday (2.0x)", Multiplier = 2.0 });
        }

        return Ok(list);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetOTHistory([FromQuery] string? userId, [FromQuery] int? year)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(new { Message = "userId is required." });
        }

        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        var list = new List<OTRecord>();

        try
        {
            using (var conn = new MySqlConnection(sqlConnStr))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    string sql = @"
                        SELECT OTNo, EMP_ID, NAME, OT_DATE, OT_ID, OT_name, OT_HOUR, 
                               OT_START, OT_END, s1, s2, OT_BREAK, OT_STATUS, OT_REASON, 
                               OT_Remarks, applevel, CreatedDate 
                        FROM aspnet_OTRecord 
                        WHERE EMP_ID = @EmpID";

                    if (year.HasValue)
                    {
                        sql += " AND YEAR(OT_DATE) = @Year";
                        cmd.Parameters.AddWithValue("@Year", year.Value);
                    }

                    sql += " ORDER BY OT_DATE DESC";

                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("@EmpID", userId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new OTRecord
                            {
                                OTNo = reader["OTNo"]?.ToString() ?? "",
                                Emp_ID = reader["EMP_ID"]?.ToString() ?? "",
                                EmpName = reader["NAME"]?.ToString() ?? "",
                                OT_Date = Convert.ToDateTime(reader["OT_DATE"]),
                                OT_ID = reader["OT_ID"]?.ToString() ?? "",
                                OT_Name = reader["OT_name"]?.ToString() ?? "",
                                OT_Hour = reader["OT_HOUR"]?.ToString() ?? "",
                                OT_Start = reader["OT_START"]?.ToString() ?? "",
                                OT_End = reader["OT_END"]?.ToString() ?? "",
                                S1 = reader["s1"]?.ToString() ?? "",
                                S2 = reader["s2"]?.ToString() ?? "",
                                OT_Break = reader["OT_BREAK"]?.ToString() ?? "",
                                OT_Status = reader["OT_STATUS"]?.ToString() ?? "",
                                OT_Reason = reader["OT_REASON"]?.ToString() ?? "",
                                OT_Remarks = reader["OT_Remarks"]?.ToString() ?? "",
                                AppLevel = reader["applevel"]?.ToString() ?? "",
                                CreatedDate = reader["CreatedDate"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedDate"]) : null
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

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingApprovals([FromQuery] string? supervisorId)
    {
        if (string.IsNullOrEmpty(supervisorId))
        {
            return BadRequest(new { Message = "supervisorId is required." });
        }

        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        var list = new List<OTRecord>();

        try
        {
            using (var conn = new MySqlConnection(sqlConnStr))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT OTNo, EMP_ID, NAME, OT_DATE, OT_ID, OT_name, OT_HOUR, 
                               OT_START, OT_END, s1, s2, OT_BREAK, OT_STATUS, OT_REASON, 
                               OT_Remarks, applevel, CreatedDate 
                        FROM aspnet_OTRecord 
                        WHERE OT_STATUS = 'PENDING' 
                          AND (
                              (applevel = '1' AND app1 = @SuperID AND (app1Pass = 'NO' OR app1Pass IS NULL))
                              OR (applevel = '2' AND app2 = @SuperID AND (app2Pass = 'NO' OR app2Pass IS NULL))
                              OR (SUPER_ID = @SuperID AND (app1Pass = 'NO' OR app1Pass IS NULL))
                          )
                        ORDER BY OT_DATE DESC";

                    cmd.Parameters.AddWithValue("@SuperID", supervisorId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new OTRecord
                            {
                                OTNo = reader["OTNo"]?.ToString() ?? "",
                                Emp_ID = reader["EMP_ID"]?.ToString() ?? "",
                                EmpName = reader["NAME"]?.ToString() ?? "",
                                OT_Date = Convert.ToDateTime(reader["OT_DATE"]),
                                OT_ID = reader["OT_ID"]?.ToString() ?? "",
                                OT_Name = reader["OT_name"]?.ToString() ?? "",
                                OT_Hour = reader["OT_HOUR"]?.ToString() ?? "",
                                OT_Start = reader["OT_START"]?.ToString() ?? "",
                                OT_End = reader["OT_END"]?.ToString() ?? "",
                                S1 = reader["s1"]?.ToString() ?? "",
                                S2 = reader["s2"]?.ToString() ?? "",
                                OT_Break = reader["OT_BREAK"]?.ToString() ?? "",
                                OT_Status = reader["OT_STATUS"]?.ToString() ?? "",
                                OT_Reason = reader["OT_REASON"]?.ToString() ?? "",
                                OT_Remarks = reader["OT_Remarks"]?.ToString() ?? "",
                                AppLevel = reader["applevel"]?.ToString() ?? "",
                                CreatedDate = reader["CreatedDate"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedDate"]) : null
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
    public async Task<IActionResult> ApplyOT([FromBody] OTApplyModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.EmpId) || string.IsNullOrEmpty(model.OTCode))
        {
            return BadRequest(new { Message = "Invalid OT application payload." });
        }

        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";

        try
        {
            string empName = "";
            string app1 = model.EmpId, app2 = "", app3 = "", app4 = "";
            string applevel = "1", appNo = "1";

            using (var conn = new MySqlConnection(sqlConnStr))
            {
                await conn.OpenAsync();

                // 1. Get employee name
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Name FROM aspnet_users WHERE ID = @ID";
                    cmd.Parameters.AddWithValue("@ID", model.EmpId);
                    empName = (await cmd.ExecuteScalarAsync())?.ToString() ?? "";
                }

                // 2. Get supervisor approval levels
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT app1, app2, app3, app4, appNo, applevel FROM aspnet_applevel WHERE ID = @ID AND GroupID = 'OT'";
                    cmd.Parameters.AddWithValue("@ID", model.EmpId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
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

                // 3. Generate unique OTNo
                string otNo = model.EmpId + DateTime.Now.ToString("yyMMddHHmmss");

                // 4. Insert records for date range
                DateTime current = model.DateFrom.Date;
                DateTime end = model.DateTo.Date;

                while (current <= end)
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO aspnet_OTRecord (
                                OTNo, OT_DATE, EMP_ID, NAME, OT_ID, OT_HOUR, OT_STATUS, OT_REF,
                                OT_REASON, SUPER_ID, OT_START, OT_END, s1, s2, OT_BREAK,
                                app1Pass, app1, app2Pass, app2, app3Pass, app3, app4Pass, app4,
                                applevel, applevelnumber, OT_Remarks, CreatedDate, CreatedBy
                            ) VALUES (
                                @OTNo, @OTDate, @EmpID, @Name, @OTID, @OTHour, 'PENDING', '1',
                                @Reason, @SuperID, @Start, @End, @S1, @S2, @Break,
                                'NO', @App1, 'NO', @App2, 'NO', @App3, 'NO', @App4,
                                @AppLevel, @AppNo, @Remarks, NOW(), @CreatedBy
                            )";

                        cmd.Parameters.AddWithValue("@OTNo", otNo);
                        cmd.Parameters.AddWithValue("@OTDate", current);
                        cmd.Parameters.AddWithValue("@EmpID", model.EmpId);
                        cmd.Parameters.AddWithValue("@Name", empName);
                        cmd.Parameters.AddWithValue("@OTID", model.OTCode);
                        cmd.Parameters.AddWithValue("@OTHour", model.TotalHours);
                        cmd.Parameters.AddWithValue("@Reason", model.Reason);
                        cmd.Parameters.AddWithValue("@SuperID", app1);
                        cmd.Parameters.AddWithValue("@Start", model.StartTime.Replace(":", ""));
                        cmd.Parameters.AddWithValue("@End", model.EndTime.Replace(":", ""));
                        cmd.Parameters.AddWithValue("@S1", model.S1);
                        cmd.Parameters.AddWithValue("@S2", model.S2);
                        cmd.Parameters.AddWithValue("@Break", model.BreakHours);
                        cmd.Parameters.AddWithValue("@App1", app1);
                        cmd.Parameters.AddWithValue("@App2", app2);
                        cmd.Parameters.AddWithValue("@App3", app3);
                        cmd.Parameters.AddWithValue("@App4", app4);
                        cmd.Parameters.AddWithValue("@AppLevel", applevel);
                        cmd.Parameters.AddWithValue("@AppNo", appNo);
                        cmd.Parameters.AddWithValue("@Remarks", model.Remarks);
                        cmd.Parameters.AddWithValue("@CreatedBy", model.EmpId);

                        await cmd.ExecuteNonQueryAsync();
                    }
                    current = current.AddDays(1);
                }
            }

            return Ok(new { Message = "Overtime application submitted successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error submitting overtime.", Details = ex.Message });
        }
    }

    [HttpPost("approve")]
    public async Task<IActionResult> ApproveOT([FromBody] OTApproveModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.OTNo) || string.IsNullOrEmpty(model.EmpId))
        {
            return BadRequest(new { Message = "Invalid request arguments." });
        }

        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        string mysqlConnStr = _configuration.GetConnectionString("ConnectionTMS") ?? "";

        try
        {
            string newStatus = model.Action.ToUpper() == "APPROVE" ? "APPROVED" : "REJECT";

            using (var conn = new MySqlConnection(sqlConnStr))
            {
                await conn.OpenAsync();

                // 1. Get OT record details
                string otId = "";
                string otHour = "";
                string otStart = "";
                string otEnd = "";
                string otBreak = "";
                string reason = "";
                string s1 = "", s2 = "";
                DateTime otDate = model.OTDate;

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT OT_ID, OT_HOUR, OT_START, OT_END, OT_BREAK, OT_REASON, s1, s2, OT_DATE FROM aspnet_OTRecord WHERE OTNo = @OTNo AND EMP_ID = @EmpID LIMIT 1";
                    cmd.Parameters.AddWithValue("@OTNo", model.OTNo);
                    cmd.Parameters.AddWithValue("@EmpID", model.EmpId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            otId = reader["OT_ID"]?.ToString() ?? "";
                            otHour = reader["OT_HOUR"]?.ToString() ?? "";
                            otStart = reader["OT_START"]?.ToString() ?? "";
                            otEnd = reader["OT_END"]?.ToString() ?? "";
                            otBreak = reader["OT_BREAK"]?.ToString() ?? "";
                            reason = reader["OT_REASON"]?.ToString() ?? "";
                            s1 = reader["s1"]?.ToString() ?? "";
                            s2 = reader["s2"]?.ToString() ?? "";
                            otDate = Convert.ToDateTime(reader["OT_DATE"]);
                        }
                    }
                }

                // 2. Update SQL Server record
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        UPDATE aspnet_OTRecord 
                        SET OT_STATUS = @Status, 
                            app1Pass = 'YES', 
                            dateapp1 = NOW(), 
                            Approved_date = NOW(), 
                            Approved_by = @ApprovedBy, 
                            OT_Remarks = CASE WHEN @Remarks <> '' THEN @Remarks ELSE OT_Remarks END 
                        WHERE OTNo = @OTNo AND EMP_ID = @EmpID";

                    cmd.Parameters.AddWithValue("@Status", newStatus);
                    cmd.Parameters.AddWithValue("@ApprovedBy", model.EmpId);
                    cmd.Parameters.AddWithValue("@Remarks", model.Remarks ?? "");
                    cmd.Parameters.AddWithValue("@OTNo", model.OTNo);
                    cmd.Parameters.AddWithValue("@EmpID", model.EmpId);

                    await cmd.ExecuteNonQueryAsync();
                }

                // 3. If approved, sync to MySQL dw_otform
                if (newStatus == "APPROVED" && !string.IsNullOrEmpty(mysqlConnStr))
                {
                    try
                    {
                        using (var myConn = new OdbcConnection(mysqlConnStr))
                        {
                            await myConn.OpenAsync();
                            using (var myCmd = myConn.CreateCommand())
                            {
                                myCmd.CommandText = @"
                                    INSERT INTO dw_otform (
                                        EMP_ID, OT_Date, OT_ID, tmstrtot, tmendot, hmbrkexc, 
                                        source, TM_OT, REMARK, REASON, nw
                                    ) VALUES (?, ?, ?, ?, ?, ?, 'eSYSTEM', ?, ?, ?, 'Y')";

                                myCmd.Parameters.Add(new OdbcParameter("EMP_ID", model.EmpId));
                                myCmd.Parameters.Add(new OdbcParameter("OT_Date", OdbcType.Date) { Value = otDate });
                                myCmd.Parameters.Add(new OdbcParameter("OT_ID", otId));
                                myCmd.Parameters.Add(new OdbcParameter("tmstrtot", otStart));
                                myCmd.Parameters.Add(new OdbcParameter("tmendot", otEnd));
                                myCmd.Parameters.Add(new OdbcParameter("hmbrkexc", otBreak));
                                myCmd.Parameters.Add(new OdbcParameter("TM_OT", otHour));
                                myCmd.Parameters.Add(new OdbcParameter("REMARK", model.Remarks ?? ""));
                                myCmd.Parameters.Add(new OdbcParameter("REASON", reason));

                                await myCmd.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"MySQL dw_otform sync warning: {ex.Message}");
                    }
                }
            }

            return Ok(new { Message = $"Overtime request successfully {(newStatus == "APPROVED" ? "approved" : "rejected")}." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error updating overtime status.", Details = ex.Message });
        }
    }
}
