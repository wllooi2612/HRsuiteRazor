using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Data.Odbc;
using System.Collections.Generic;
using System.Text;
using Hrsuite.WebApi.Models;
using System.Data;

namespace Hrsuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HomeController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public HomeController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string GetMySqlConnectionString() =>
        _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";

    private string GetMyMySqlConnectionString() =>
        _configuration.GetConnectionString("ConnectionTMS") ?? "";

    [HttpGet("calendar-events")]
    public IActionResult GetCalendarEvents([FromQuery] string userId, [FromQuery] string startDate, [FromQuery] string endDate)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(new { Message = "UserId is required." });
        }

        if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
        {
            return BadRequest(new { Message = "startDate and endDate are required." });
        }

        string sqlConnStr = GetMySqlConnectionString();
        string mysqlConnStr = GetMyMySqlConnectionString();

        var events = new List<CalendarEventModel>();

        try
        {
            DateTime startDt = DateTime.Parse(startDate);
            DateTime endDt = DateTime.Parse(endDate);

            string sqlStart = startDt.ToString("yyyy-MM-dd");
            string sqlEnd = endDt.ToString("yyyy-MM-dd");

            // 1. Fetch leave records from SQL Server
            var leaveRecords = new List<dynamic>();
            using (var conn = new MySqlConnection(sqlConnStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT LeaveNo, Leave_Date, Leave_Status, Leave_ID, leave_reason 
                        FROM aspnet_LeaveRecord 
                        WHERE Emp_ID = @EmpID AND Leave_Date BETWEEN @StartDate AND @EndDate";
                    cmd.Parameters.AddWithValue("@EmpID", userId);
                    cmd.Parameters.AddWithValue("@StartDate", startDt);
                    cmd.Parameters.AddWithValue("@EndDate", endDt);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            leaveRecords.Add(new {
                                LeaveNo = reader["LeaveNo"]?.ToString() ?? "",
                                LeaveDate = Convert.ToDateTime(reader["Leave_Date"]),
                                LeaveStatus = reader["Leave_Status"]?.ToString() ?? "",
                                LeaveId = reader["Leave_ID"]?.ToString() ?? "",
                                LeaveReason = reader["leave_reason"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }

            // 2. Fetch leave names and holidays from MySQL
            var leaveNames = new Dictionary<string, string>();
            var holidays = new List<dynamic>();

            using (var conn = new OdbcConnection(mysqlConnStr))
            {
                conn.Open();

                // Fetch all leave names for quick lookup
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Leave_ID, NAME FROM dw_leave";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string lid = reader["Leave_ID"]?.ToString() ?? "";
                            string name = reader["NAME"]?.ToString() ?? "";
                            leaveNames[lid] = name;
                        }
                    }
                }

                // Fetch holidays in range
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT holiday_id, Name FROM dw_holiday WHERE holiday_id BETWEEN ? AND ?";
                    cmd.Parameters.AddWithValue("?", sqlStart);
                    cmd.Parameters.AddWithValue("?", sqlEnd);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            holidays.Add(new {
                                HolidayId = reader["holiday_id"]?.ToString() ?? "",
                                Name = reader["Name"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }

            // Map leaves to events
            foreach (var leave in leaveRecords)
            {
                string status = leave.LeaveStatus.ToUpper();
                string leaveName = leaveNames.ContainsKey(leave.LeaveId) ? leaveNames[leave.LeaveId] : leave.LeaveId;
                string desc = $"Status: ({leave.LeaveStatus})\nLeave: ({leaveName})\nReason: {leave.LeaveReason}";

                events.Add(new CalendarEventModel
                {
                    Date = leave.LeaveDate.ToString("yyyy-MM-dd"),
                    Type = status, // "PENDING", "APPROVE", "REJECT"
                    Description = desc
                });
            }

            // Map holidays to events
            foreach (var hol in holidays)
            {
                events.Add(new CalendarEventModel
                {
                    Date = DateTime.Parse(hol.HolidayId).ToString("yyyy-MM-dd"),
                    Type = "HOLIDAY",
                    Description = $"Holiday: {hol.Name}"
                });
            }

            // 3. Check if Time Management Schedule (OpenMysqlSchd) is enabled in SQL Server
            string openTimeManSchd = "N";
            using (var conn = new MySqlConnection(sqlConnStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT OpenMysqlSchd FROM aspnet_general WHERE ID = '1'";
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        openTimeManSchd = result.ToString() ?? "N";
                    }
                }
            }

            // 4. Fetch schedule details from MySQL if enabled
            if (openTimeManSchd.Equals("Y", StringComparison.OrdinalIgnoreCase))
            {
                using (var conn = new OdbcConnection(mysqlConnStr))
                {
                    conn.Open();

                    // Get wrkgrp_ID (work group) from history
                    string wrkgrpId = "";
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT wrkgrp_ID FROM dw_history WHERE emp_id = ? ORDER BY dt_effect DESC LIMIT 1";
                        cmd.Parameters.AddWithValue("?", userId);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            wrkgrpId = result.ToString() ?? "";
                        }
                    }

                    if (!string.IsNullOrEmpty(wrkgrpId))
                    {
                        // Fetch all schedule codes
                        var daySchdMap = new Dictionary<string, (string Type, string Name)>();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT dayschd_id, type, name FROM dw_dayschd";
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string dsid = reader["dayschd_id"]?.ToString() ?? "";
                                    string type = reader["type"]?.ToString() ?? "";
                                    string name = reader["name"]?.ToString() ?? "";
                                    daySchdMap[dsid] = (type, name);
                                }
                            }
                        }

                        // Fetch special schedules (dw_dayschd_sp)
                        var specialSchd = new Dictionary<string, string>();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT dt, dayschd_id FROM dw_dayschd_sp WHERE emp_id = ? AND dt BETWEEN ? AND ?";
                            cmd.Parameters.AddWithValue("?", userId);
                            cmd.Parameters.AddWithValue("?", sqlStart);
                            cmd.Parameters.AddWithValue("?", sqlEnd);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    DateTime dt = Convert.ToDateTime(reader["dt"]);
                                    specialSchd[dt.ToString("yyyy-MM-dd")] = reader["dayschd_id"]?.ToString() ?? "";
                                }
                            }
                        }

                        // Check individual schedule (dw_tms_idvschd_dtl)
                        var idvSchd = new List<(DateTime Date, string DaySchdId)>();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT dt, dayschd_id FROM dw_tms_idvschd_dtl WHERE emp_id = ? AND dt BETWEEN ? AND ?";
                            cmd.Parameters.AddWithValue("?", userId);
                            cmd.Parameters.AddWithValue("?", sqlStart);
                            cmd.Parameters.AddWithValue("?", sqlEnd);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    idvSchd.Add((Convert.ToDateTime(reader["dt"]), reader["dayschd_id"]?.ToString() ?? ""));
                                }
                            }
                        }

                        if (idvSchd.Count > 0)
                        {
                            foreach (var item in idvSchd)
                            {
                                string dtStr = item.Date.ToString("yyyy-MM-dd");
                                string dayschdId = specialSchd.ContainsKey(dtStr) ? specialSchd[dtStr] : item.DaySchdId;

                                if (daySchdMap.ContainsKey(dayschdId))
                                {
                                    var schdInfo = daySchdMap[dayschdId];
                                    string type = MapScheduleType(schdInfo.Type);
                                    events.Add(new CalendarEventModel
                                    {
                                        Date = dtStr,
                                        Type = type,
                                        Description = $"({type}) {schdInfo.Name}"
                                    });
                                }
                            }
                        }
                        else
                        {
                            // Check group schedule (dw_tms_grpschd_dtl)
                            var grpSchd = new List<(DateTime Date, string DaySchdId)>();
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "SELECT dt, dayschd_id FROM dw_tms_grpschd_dtl WHERE group_id = ? AND dt BETWEEN ? AND ?";
                                cmd.Parameters.AddWithValue("?", wrkgrpId);
                                cmd.Parameters.AddWithValue("?", sqlStart);
                                cmd.Parameters.AddWithValue("?", sqlEnd);
                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        grpSchd.Add((Convert.ToDateTime(reader["dt"]), reader["dayschd_id"]?.ToString() ?? ""));
                                    }
                                }
                            }

                            foreach (var item in grpSchd)
                            {
                                string dtStr = item.Date.ToString("yyyy-MM-dd");
                                string dayschdId = specialSchd.ContainsKey(dtStr) ? specialSchd[dtStr] : item.DaySchdId;

                                if (daySchdMap.ContainsKey(dayschdId))
                                {
                                    var schdInfo = daySchdMap[dayschdId];
                                    string type = MapScheduleType(schdInfo.Type);
                                    events.Add(new CalendarEventModel
                                    {
                                        Date = dtStr,
                                        Type = type,
                                        Description = $"({type}) {schdInfo.Name}"
                                    });
                                }
                            }
                        }
                    }
                }
            }

            return Ok(events);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error retrieving calendar events.", Details = ex.Message });
        }
    }

    private string MapScheduleType(string dbType)
    {
        return dbType.ToUpper() switch
        {
            "W" => "WORKDAY",
            "R" => "RESTDAY",
            "O" => "OFFDAY",
            "H" => "HOLIDAY",
            _ => dbType.ToUpper()
        };
    }
}
