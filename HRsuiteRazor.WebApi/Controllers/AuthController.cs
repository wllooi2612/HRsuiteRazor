using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Hrsuite.WebApi.Models;
using Hrsuite.WebApi.Helpers;

namespace Hrsuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new { Message = "Username and password are required." });
        }

        string connStr = _configuration.GetConnectionString("MyhrsuiteConnectionString") ?? "";
        string encryptionKey = _configuration["encryptionKey"] ?? "hrsuite";
        
        // Encrypt the password input to match the database hash
        string encryptedInput = CryptographyHelper.Encrypt(request.Password, encryptionKey);

        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT ID, Name, Password, Email, IsActive, UserGroupID, LeaveUserGroupID, superAdminLeave FROM aspnet_users WHERE ID = @ID AND IsActive = 'Y'";
                    cmd.Parameters.AddWithValue("@ID", request.Username);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string dbPassword = reader["Password"]?.ToString() ?? "";
                            if (dbPassword == encryptedInput)
                            {
                                var user = new User
                                {
                                    ID = reader["ID"]?.ToString() ?? "",
                                    Name = reader["Name"]?.ToString() ?? "",
                                    Email = reader["Email"]?.ToString() ?? "",
                                    IsActive = reader["IsActive"]?.ToString() ?? "",
                                    UserGroupID = reader["UserGroupID"]?.ToString() ?? "",
                                    LeaveUserGroupID = reader["LeaveUserGroupID"]?.ToString() ?? "",
                                    SuperAdminLeave = reader["superAdminLeave"]?.ToString() ?? ""
                                };

                                return Ok(user);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Database connection error.", Details = ex.Message });
        }

        return Unauthorized(new { Message = "Invalid username or password." });
    }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
