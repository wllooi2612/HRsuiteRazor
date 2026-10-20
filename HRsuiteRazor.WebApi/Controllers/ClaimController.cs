using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Data.Odbc;
using System.Collections.Generic;
using Hrsuite.WebApi.Models;

namespace Hrsuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClaimController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ClaimController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("types")]
    public async Task<IActionResult> GetClaimTypes([FromQuery] string? userId)
    {
        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        var list = new List<ClaimType>();

        try
        {
            using (var conn = new MySqlConnection(sqlConnStr))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT DISTINCT ClaimNo, Name FROM aspnet_ClaimID WHERE Display = 'Y' OR Display IS NULL";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string code = reader["ClaimNo"]?.ToString()?.Trim() ?? "";
                            string name = reader["Name"]?.ToString()?.Trim() ?? "";
                            if (!string.IsNullOrEmpty(code))
                            {
                                list.Add(new ClaimType { Code = code, Name = name, MaxLimit = 500m });
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetClaimTypes error: {ex.Message}");
        }

        if (list.Count == 0)
        {
            list.Add(new ClaimType { Code = "MED", Name = "Medical Outpatient Claim", MaxLimit = 500m });
            list.Add(new ClaimType { Code = "DEN", Name = "Dental Treatment", MaxLimit = 300m });
            list.Add(new ClaimType { Code = "TRAV", Name = "Travel & Mileage Claim", MaxLimit = 1000m });
            list.Add(new ClaimType { Code = "ENT", Name = "Client Entertainment", MaxLimit = 800m });
            list.Add(new ClaimType { Code = "MISC", Name = "Miscellaneous Expenses", MaxLimit = 200m });
        }

        return Ok(list);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetClaimHistory([FromQuery] string? userId, [FromQuery] int? year)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(new { Message = "userId is required." });
        }

        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        var list = new List<ClaimRecord>();

        try
        {
            using (var conn = new MySqlConnection(sqlConnStr))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    string sql = @"
                        SELECT ClaimID, ClaimNo, EMP_ID, NAME, Claim_Apply_DATE, claimTotal, 
                               ApprovedAmount, Claim_STATUS, Remarks, description, receipt_no, 
                               Clinic_ID, applevel, CreatedDate 
                        FROM aspnet_ClaimRecord 
                        WHERE EMP_ID = @EmpID";

                    if (year.HasValue)
                    {
                        sql += " AND YEAR(Claim_Apply_DATE) = @Year";
                        cmd.Parameters.AddWithValue("@Year", year.Value);
                    }

                    sql += " ORDER BY Claim_Apply_DATE DESC";

                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("@EmpID", userId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new ClaimRecord
                            {
                                ClaimID = reader["ClaimID"]?.ToString() ?? "",
                                ClaimNo = reader["ClaimNo"]?.ToString() ?? "",
                                Emp_ID = reader["EMP_ID"]?.ToString() ?? "",
                                EmpName = reader["NAME"]?.ToString() ?? "",
                                Claim_Apply_DATE = Convert.ToDateTime(reader["Claim_Apply_DATE"]),
                                ClaimTotal = reader["claimTotal"] != DBNull.Value ? Convert.ToDecimal(reader["claimTotal"]) : 0m,
                                ApprovedAmount = reader["ApprovedAmount"] != DBNull.Value ? Convert.ToDecimal(reader["ApprovedAmount"]) : 0m,
                                Claim_STATUS = reader["Claim_STATUS"]?.ToString() ?? "",
                                Remarks = reader["Remarks"]?.ToString() ?? "",
                                Description = reader["description"]?.ToString() ?? "",
                                ReceiptNo = reader["receipt_no"]?.ToString() ?? "",
                                Clinic_ID = reader["Clinic_ID"]?.ToString() ?? "",
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
    public async Task<IActionResult> GetPendingClaims([FromQuery] string? supervisorId)
    {
        if (string.IsNullOrEmpty(supervisorId))
        {
            return BadRequest(new { Message = "supervisorId is required." });
        }

        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        var list = new List<ClaimRecord>();

        try
        {
            using (var conn = new MySqlConnection(sqlConnStr))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT ClaimID, ClaimNo, EMP_ID, NAME, Claim_Apply_DATE, claimTotal, 
                               ApprovedAmount, Claim_STATUS, Remarks, description, receipt_no, 
                               Clinic_ID, applevel, CreatedDate 
                        FROM aspnet_ClaimRecord 
                        WHERE Claim_STATUS = 'PENDING' 
                          AND (
                              (applevel = '1' AND app1 = @SuperID AND (app1Pass = 'NO' OR app1Pass IS NULL))
                              OR (applevel = '2' AND app2 = @SuperID AND (app2Pass = 'NO' OR app2Pass IS NULL))
                              OR (SUPER_ID = @SuperID AND (app1Pass = 'NO' OR app1Pass IS NULL))
                          )
                        ORDER BY Claim_Apply_DATE DESC";

                    cmd.Parameters.AddWithValue("@SuperID", supervisorId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new ClaimRecord
                            {
                                ClaimID = reader["ClaimID"]?.ToString() ?? "",
                                ClaimNo = reader["ClaimNo"]?.ToString() ?? "",
                                Emp_ID = reader["EMP_ID"]?.ToString() ?? "",
                                EmpName = reader["NAME"]?.ToString() ?? "",
                                Claim_Apply_DATE = Convert.ToDateTime(reader["Claim_Apply_DATE"]),
                                ClaimTotal = reader["claimTotal"] != DBNull.Value ? Convert.ToDecimal(reader["claimTotal"]) : 0m,
                                ApprovedAmount = reader["ApprovedAmount"] != DBNull.Value ? Convert.ToDecimal(reader["ApprovedAmount"]) : 0m,
                                Claim_STATUS = reader["Claim_STATUS"]?.ToString() ?? "",
                                Remarks = reader["Remarks"]?.ToString() ?? "",
                                Description = reader["description"]?.ToString() ?? "",
                                ReceiptNo = reader["receipt_no"]?.ToString() ?? "",
                                Clinic_ID = reader["Clinic_ID"]?.ToString() ?? "",
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
    public async Task<IActionResult> ApplyClaim([FromBody] ClaimApplyModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.EmpId) || model.Amount <= 0)
        {
            return BadRequest(new { Message = "Invalid claim application payload." });
        }

        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";

        try
        {
            string empName = "";
            string app1 = model.EmpId, app2 = "", app3 = "";
            string applevel = "1", appNo = "1";

            using (var conn = new MySqlConnection(sqlConnStr))
            {
                await conn.OpenAsync();

                // 1. Employee name
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Name FROM aspnet_users WHERE ID = @ID";
                    cmd.Parameters.AddWithValue("@ID", model.EmpId);
                    empName = (await cmd.ExecuteScalarAsync())?.ToString() ?? "";
                }

                // 2. Supervisor routing
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT app1, app2, app3, appNo, applevel FROM aspnet_applevel WHERE ID = @ID AND GroupID = 'CLAIM'";
                    cmd.Parameters.AddWithValue("@ID", model.EmpId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            app1 = reader["app1"]?.ToString() ?? model.EmpId;
                            app2 = reader["app2"]?.ToString() ?? "";
                            app3 = reader["app3"]?.ToString() ?? "";
                            appNo = reader["appNo"]?.ToString() ?? "1";
                            applevel = reader["applevel"]?.ToString() ?? "1";
                        }
                    }
                }

                // 3. Generate unique ClaimID and insert
                string claimId = "CLM" + model.EmpId + DateTime.Now.ToString("yyMMddHHmmss");

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO aspnet_ClaimRecord (
                            ClaimID, ClaimNo, Claim_Apply_DATE, EMP_ID, NAME, SUPER_ID,
                            Claim_STATUS, claimTotal, description, receipt_no, Clinic_ID,
                            Remarks, app1, app2, app3, app1Pass, app2Pass, app3Pass,
                            applevel, appNo, CreatedDate, CreatedBy
                        ) VALUES (
                            @ClaimID, @ClaimNo, @ClaimDate, @EmpID, @Name, @SuperID,
                            'PENDING', @Total, @Desc, @ReceiptNo, @ClinicID,
                            @Remarks, @App1, @App2, @App3, 'NO', 'NO', 'NO',
                            @AppLevel, @AppNo, NOW(), @CreatedBy
                        )";

                    cmd.Parameters.AddWithValue("@ClaimID", claimId);
                    cmd.Parameters.AddWithValue("@ClaimNo", model.ClaimCode);
                    cmd.Parameters.AddWithValue("@ClaimDate", model.ClaimDate);
                    cmd.Parameters.AddWithValue("@EmpID", model.EmpId);
                    cmd.Parameters.AddWithValue("@Name", empName);
                    cmd.Parameters.AddWithValue("@SuperID", app1);
                    cmd.Parameters.AddWithValue("@Total", model.Amount);
                    cmd.Parameters.AddWithValue("@Desc", model.Description ?? "");
                    cmd.Parameters.AddWithValue("@ReceiptNo", model.ReceiptNo ?? "");
                    cmd.Parameters.AddWithValue("@ClinicID", model.ClinicId ?? "");
                    cmd.Parameters.AddWithValue("@Remarks", model.Remarks ?? "");
                    cmd.Parameters.AddWithValue("@App1", app1);
                    cmd.Parameters.AddWithValue("@App2", app2);
                    cmd.Parameters.AddWithValue("@App3", app3);
                    cmd.Parameters.AddWithValue("@AppLevel", applevel);
                    cmd.Parameters.AddWithValue("@AppNo", appNo);
                    cmd.Parameters.AddWithValue("@CreatedBy", model.EmpId);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return Ok(new { Message = "Claim application submitted successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error submitting claim.", Details = ex.Message });
        }
    }

    [HttpPost("approve")]
    public async Task<IActionResult> ApproveClaim([FromBody] ClaimApproveModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.ClaimID))
        {
            return BadRequest(new { Message = "Invalid request arguments." });
        }

        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";

        try
        {
            string newStatus = model.Action.ToUpper() == "APPROVE" ? "APPROVED" : "REJECT";

            using (var conn = new MySqlConnection(sqlConnStr))
            {
                await conn.OpenAsync();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        UPDATE aspnet_ClaimRecord 
                        SET Claim_STATUS = @Status, 
                            app1Pass = 'YES', 
                            dateapp1 = NOW(), 
                            ApprovedAmount = CASE WHEN @ApprovedAmount > 0 THEN @ApprovedAmount ELSE claimTotal END,
                            Remarks_1 = @Remarks 
                        WHERE ClaimID = @ClaimID";

                    cmd.Parameters.AddWithValue("@Status", newStatus);
                    cmd.Parameters.AddWithValue("@ApprovedAmount", model.ApprovedAmount);
                    cmd.Parameters.AddWithValue("@Remarks", model.Remarks ?? "");
                    cmd.Parameters.AddWithValue("@ClaimID", model.ClaimID);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return Ok(new { Message = $"Claim request successfully {(newStatus == "APPROVED" ? "approved" : "rejected")}." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error updating claim status.", Details = ex.Message });
        }
    }
}
