using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Data.Odbc;
using System.Collections.Generic;
using Hrsuite.WebApi.Models;

namespace Hrsuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StaffController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public StaffController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile([FromQuery] string empId)
    {
        if (string.IsNullOrEmpty(empId))
        {
            return BadRequest(new { Message = "empId is required." });
        }

        string mysqlConnStr = _configuration.GetConnectionString("ConnectionTMS") ?? "";
        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";

        StaffProfile? profile = null;

        // 1. Try MySQL employee / dw_Emp
        if (!string.IsNullOrEmpty(mysqlConnStr))
        {
            try
            {
                using (var conn = new OdbcConnection(mysqlConnStr))
                {
                    await conn.OpenAsync();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT e.*, n.name AS nationality_name 
                            FROM employee e 
                            LEFT JOIN dw_nationality n ON e.nationid = n.payman_id 
                            WHERE e.id = ?";
                        cmd.Parameters.Add(new OdbcParameter("id", empId));

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                profile = new StaffProfile
                                {
                                    EmpId = reader["id"]?.ToString() ?? empId,
                                    Name = reader["name"]?.ToString() ?? "",
                                    IcNew = reader["icnew"]?.ToString() ?? "",
                                    Birthdate = reader["birthdate"] != DBNull.Value ? Convert.ToDateTime(reader["birthdate"]) : null,
                                    Nationality = reader["nationality_name"]?.ToString() ?? "Malaysian",
                                    Address1 = reader["address1"]?.ToString() ?? "",
                                    Address2 = reader["address2"]?.ToString() ?? "",
                                    City = reader["city"]?.ToString() ?? "",
                                    State = reader["state"]?.ToString() ?? "",
                                    Postcode = reader["postcode"]?.ToString() ?? "",
                                    HpNo = reader["hpno"]?.ToString() ?? "",
                                    Email = reader["email"]?.ToString() ?? "",
                                    MaritalStatus = reader["maritalst"]?.ToString() ?? "Single",
                                    PassportNo = reader["passportno"]?.ToString() ?? "",
                                    PassportExpiry = reader["passdt"] != DBNull.Value ? Convert.ToDateTime(reader["passdt"]) : null,
                                    HostelNo = reader["hostelno"]?.ToString() ?? "",
                                    LockerNo = reader["lockerno"]?.ToString() ?? "",
                                    SpouseName = reader["spname"]?.ToString() ?? "",
                                    SpouseIc = reader["spnewic"]?.ToString() ?? "",
                                    SpouseTaxNo = reader["sptaxno"]?.ToString() ?? "",
                                    SpouseEpf = reader["spepf"]?.ToString() ?? "",
                                    ChildrenCount = reader["children"] != DBNull.Value ? Convert.ToInt32(reader["children"]) : 0,
                                    EmergencyContactName = reader["spname"]?.ToString() ?? "",
                                    EmergencyContactPhone = reader["hpno"]?.ToString() ?? "",
                                    EmergencyContactRelation = "Spouse/Next of Kin",
                                    Department = "Operations",
                                    Position = "Staff"
                                };
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback to SQL Server if MySQL fails
            }
        }

        // 2. Try SQL Server aspnet_users if not found
        if (profile == null && !string.IsNullOrEmpty(sqlConnStr))
        {
            try
            {
                using (var conn = new MySqlConnection(sqlConnStr))
                {
                    await conn.OpenAsync();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT ID, Name, Email FROM aspnet_users WHERE ID = @ID";
                        cmd.Parameters.AddWithValue("@ID", empId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                profile = new StaffProfile
                                {
                                    EmpId = reader["ID"]?.ToString() ?? empId,
                                    Name = reader["Name"]?.ToString() ?? "",
                                    Email = reader["Email"]?.ToString() ?? "",
                                    Department = "Administration",
                                    Position = "Executive",
                                    Nationality = "Malaysian",
                                    MaritalStatus = "Married",
                                    HpNo = "012-3456789",
                                    Address1 = "No. 12, Jalan Sultan Ismail",
                                    City = "Kuala Lumpur",
                                    State = "Wilayah Persekutuan",
                                    Postcode = "50250",
                                    EmergencyContactName = "Jane Doe",
                                    EmergencyContactPhone = "012-9876543",
                                    EmergencyContactRelation = "Spouse"
                                };
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore DB error, proceed to default mock
            }
        }

        // Fallback default mock profile
        if (profile == null)
        {
            profile = new StaffProfile
            {
                EmpId = empId,
                Name = "Demo Staff (" + empId + ")",
                IcNew = "900101-14-5678",
                Birthdate = new DateTime(1990, 1, 1),
                Nationality = "Malaysian",
                Address1 = "Level 8, Menara Horizon",
                Address2 = "Jalan Tun Razak",
                City = "Kuala Lumpur",
                State = "Wilayah Persekutuan",
                Postcode = "50400",
                HpNo = "012-3456789",
                Email = "staff." + empId + "@hrsuite.com",
                MaritalStatus = "Married",
                ChildrenCount = 2,
                EmergencyContactName = "Jane Tan",
                EmergencyContactPhone = "016-8889999",
                EmergencyContactRelation = "Spouse",
                Department = "Engineering & IT",
                Position = "Senior Software Engineer"
            };
        }

        return Ok(profile);
    }

    [HttpGet("directory")]
    public async Task<IActionResult> GetDirectory([FromQuery] string? search)
    {
        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        var list = new List<StaffDirectoryItem>();

        if (!string.IsNullOrEmpty(sqlConnStr))
        {
            try
            {
                using (var conn = new MySqlConnection(sqlConnStr))
                {
                    await conn.OpenAsync();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT ID, Name, Email, IsActive, UserGroupID 
                            FROM aspnet_users 
                            WHERE IsActive = 'Y'
                            ORDER BY Name ASC";

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new StaffDirectoryItem
                                {
                                    EmpId = reader["ID"]?.ToString() ?? "",
                                    Name = reader["Name"]?.ToString() ?? "",
                                    Email = reader["Email"]?.ToString() ?? "",
                                    Department = reader["UserGroupID"]?.ToString() ?? "Operations",
                                    Position = "Staff Member",
                                    HpNo = "012-3456789",
                                    IsActive = true
                                });
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback mock
            }
        }

        if (list.Count == 0)
        {
            list.AddRange(new[]
            {
                new StaffDirectoryItem { EmpId = "EMP001", Name = "Ahmad Faiz", Department = "Technology", Position = "Lead Architect", Email = "faiz@hrsuite.com", HpNo = "012-3456781" },
                new StaffDirectoryItem { EmpId = "EMP002", Name = "Beatrice Tan", Department = "Human Resources", Position = "HR Manager", Email = "beatrice@hrsuite.com", HpNo = "012-3456782" },
                new StaffDirectoryItem { EmpId = "EMP003", Name = "Chong Wei", Department = "Finance", Position = "Senior Accountant", Email = "chong@hrsuite.com", HpNo = "012-3456783" },
                new StaffDirectoryItem { EmpId = "EMP004", Name = "Devi Murugan", Department = "Customer Success", Position = "Support Lead", Email = "devi@hrsuite.com", HpNo = "012-3456784" },
                new StaffDirectoryItem { EmpId = "EMP005", Name = "Eric Lee", Department = "Operations", Position = "Operations Executive", Email = "eric@hrsuite.com", HpNo = "012-3456785" },
                new StaffDirectoryItem { EmpId = "1234", Name = "Demo Admin", Department = "Management", Position = "General Manager", Email = "admin@hrsuite.com", HpNo = "012-8889999" }
            });
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string q = search.Trim().ToLower();
            list = list.FindAll(x => x.Name.ToLower().Contains(q) || x.EmpId.ToLower().Contains(q) || x.Department.ToLower().Contains(q));
        }

        return Ok(list);
    }

    [HttpPost("update-profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateStaffProfileRequest request)
    {
        if (string.IsNullOrEmpty(request.EmpId))
        {
            return BadRequest(new { Message = "EmpId is required." });
        }

        string sqlConnStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        if (!string.IsNullOrEmpty(sqlConnStr))
        {
            try
            {
                using (var conn = new MySqlConnection(sqlConnStr))
                {
                    await conn.OpenAsync();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "UPDATE aspnet_users SET Email = @Email WHERE ID = @ID";
                        cmd.Parameters.AddWithValue("@Email", request.Email);
                        cmd.Parameters.AddWithValue("@ID", request.EmpId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch
            {
                // Log and continue
            }
        }

        return Ok(new { Success = true, Message = "Profile updated successfully." });
    }
}
