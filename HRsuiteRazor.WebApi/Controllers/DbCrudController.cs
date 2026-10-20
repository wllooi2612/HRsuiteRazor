using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using HRsuiteRazor.Shared.Models;
using System.Text;
using System.Text.Json;

namespace Hrsuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DbCrudController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private string GetConnectionString() => 
        _configuration.GetConnectionString("MyhrsuiteConnectionString")
        ?? "Server=localhost;Port=3309;Database=myhrsuitelog;User ID=dba;Password=sql8;AllowUserVariables=True;CharSet=utf8mb4;";

    public DbCrudController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private bool IsValidIdentifier(string name) => 
        !string.IsNullOrWhiteSpace(name) && name.All(c => char.IsLetterOrDigit(c) || c == '_');

    // SELECT
    [HttpGet("{tableName}")]
    public async Task<IActionResult> Select(string tableName, [FromQuery] int limit = 50, [FromQuery] int offset = 0, [FromQuery] string? search = null)
    {
        if (!IsValidIdentifier(tableName))
            return BadRequest(ApiResult<List<Dictionary<string, object?>>>.Fail("Invalid table name."));

        var rows = new List<Dictionary<string, object?>>();
        try
        {
            using var conn = new MySqlConnection(GetConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT * FROM `{tableName}` LIMIT @Limit OFFSET @Offset";
            cmd.Parameters.AddWithValue("@Limit", Math.Min(limit, 500));
            cmd.Parameters.AddWithValue("@Offset", Math.Max(0, offset));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
            }

            return Ok(ApiResult<List<Dictionary<string, object?>>>.Ok(rows));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<List<Dictionary<string, object?>>>.Fail(ex.Message));
        }
    }

    // INSERT
    [HttpPost("{tableName}")]
    public async Task<IActionResult> Insert(string tableName, [FromBody] Dictionary<string, JsonElement> data)
    {
        if (!IsValidIdentifier(tableName))
            return BadRequest(new GenericCrudResponse { Success = false, Message = "Invalid table name." });

        if (data == null || data.Count == 0)
            return BadRequest(new GenericCrudResponse { Success = false, Message = "No data provided for insert." });

        foreach (var col in data.Keys)
        {
            if (!IsValidIdentifier(col))
                return BadRequest(new GenericCrudResponse { Success = false, Message = $"Invalid column name: {col}" });
        }

        try
        {
            using var conn = new MySqlConnection(GetConnectionString());
            await conn.OpenAsync();

            var cols = data.Keys.ToList();
            var colNames = string.Join(", ", cols.Select(c => $"`{c}`"));
            var paramNames = string.Join(", ", cols.Select(c => $"@{c}"));

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"INSERT INTO `{tableName}` ({colNames}) VALUES ({paramNames}); SELECT LAST_INSERT_ID();";

            foreach (var kvp in data)
            {
                object? val = ConvertJsonElement(kvp.Value);
                cmd.Parameters.AddWithValue($"@{kvp.Key}", val ?? DBNull.Value);
            }

            var insertId = await cmd.ExecuteScalarAsync();
            return Ok(new GenericCrudResponse
            {
                Success = true,
                Message = $"Record inserted successfully into `{tableName}`.",
                RowsAffected = 1,
                InsertedId = insertId
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new GenericCrudResponse { Success = false, Message = ex.Message });
        }
    }

    // UPDATE
    [HttpPut("{tableName}/{idColumn}/{idValue}")]
    public async Task<IActionResult> Update(string tableName, string idColumn, string idValue, [FromBody] Dictionary<string, JsonElement> data)
    {
        if (!IsValidIdentifier(tableName) || !IsValidIdentifier(idColumn))
            return BadRequest(new GenericCrudResponse { Success = false, Message = "Invalid table or column name." });

        if (data == null || data.Count == 0)
            return BadRequest(new GenericCrudResponse { Success = false, Message = "No data provided for update." });

        foreach (var col in data.Keys)
        {
            if (!IsValidIdentifier(col))
                return BadRequest(new GenericCrudResponse { Success = false, Message = $"Invalid column name: {col}" });
        }

        try
        {
            using var conn = new MySqlConnection(GetConnectionString());
            await conn.OpenAsync();

            var setClauses = data.Keys.Select(c => $"`{c}` = @set_{c}").ToList();
            string setSql = string.Join(", ", setClauses);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"UPDATE `{tableName}` SET {setSql} WHERE `{idColumn}` = @pk_val";
            cmd.Parameters.AddWithValue("@pk_val", idValue);

            foreach (var kvp in data)
            {
                object? val = ConvertJsonElement(kvp.Value);
                cmd.Parameters.AddWithValue($"@set_{kvp.Key}", val ?? DBNull.Value);
            }

            int affected = await cmd.ExecuteNonQueryAsync();
            return Ok(new GenericCrudResponse
            {
                Success = true,
                Message = $"Updated {affected} row(s) in `{tableName}`.",
                RowsAffected = affected
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new GenericCrudResponse { Success = false, Message = ex.Message });
        }
    }

    // DELETE
    [HttpDelete("{tableName}/{idColumn}/{idValue}")]
    public async Task<IActionResult> Delete(string tableName, string idColumn, string idValue)
    {
        if (!IsValidIdentifier(tableName) || !IsValidIdentifier(idColumn))
            return BadRequest(new GenericCrudResponse { Success = false, Message = "Invalid table or column name." });

        try
        {
            using var conn = new MySqlConnection(GetConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM `{tableName}` WHERE `{idColumn}` = @pk_val";
            cmd.Parameters.AddWithValue("@pk_val", idValue);

            int affected = await cmd.ExecuteNonQueryAsync();
            return Ok(new GenericCrudResponse
            {
                Success = true,
                Message = $"Deleted {affected} row(s) from `{tableName}`.",
                RowsAffected = affected
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new GenericCrudResponse { Success = false, Message = ex.Message });
        }
    }

    private static object? ConvertJsonElement(JsonElement elem)
    {
        switch (elem.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.String:
                return elem.GetString();
            case JsonValueKind.Number:
                if (elem.TryGetInt64(out long l)) return l;
                if (elem.TryGetDecimal(out decimal d)) return d;
                return elem.GetDouble();
            case JsonValueKind.True:
                return 1;
            case JsonValueKind.False:
                return 0;
            default:
                return elem.ToString();
        }
    }
}
