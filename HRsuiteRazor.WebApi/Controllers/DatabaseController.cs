using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using HRsuiteRazor.Shared.Models;
using System.Data;

namespace Hrsuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DatabaseController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private string GetConnectionString() => 
        _configuration.GetConnectionString("MyhrsuiteConnectionString")
        ?? "Server=localhost;Port=3309;Database=myhrsuitelog;User ID=dba;Password=sql8;AllowUserVariables=True;CharSet=utf8mb4;";

    public DatabaseController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var info = new DbStatusInfo
        {
            DatabaseName = "myhrsuitelog",
            Host = "localhost",
            Port = 3309
        };

        try
        {
            using var conn = new MySqlConnection(GetConnectionString());
            await conn.OpenAsync();
            info.IsConnected = true;
            info.ServerVersion = conn.ServerVersion;

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) AS table_count, 
                       IFNULL(SUM(TABLE_ROWS), 0) AS total_rows
                FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = 'myhrsuitelog';";

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                info.TotalTables = Convert.ToInt32(reader["table_count"]);
                info.TotalRows = Convert.ToInt64(reader["total_rows"]);
            }

            return Ok(ApiResult<DbStatusInfo>.Ok(info));
        }
        catch (Exception ex)
        {
            info.IsConnected = false;
            info.ErrorMessage = ex.Message;
            return Ok(ApiResult<DbStatusInfo>.Ok(info, "Database connection error: " + ex.Message));
        }
    }

    [HttpGet("tables")]
    public async Task<IActionResult> GetTables()
    {
        var list = new List<TableStat>();
        try
        {
            using var conn = new MySqlConnection(GetConnectionString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT TABLE_NAME, IFNULL(TABLE_ROWS, 0) AS row_count, IFNULL(ENGINE, 'InnoDB') AS engine_name
                FROM information_schema.TABLES 
                WHERE TABLE_SCHEMA = 'myhrsuitelog'
                ORDER BY row_count DESC, TABLE_NAME ASC;";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TableStat
                {
                    TableName = reader["TABLE_NAME"]?.ToString() ?? "",
                    RowCount = Convert.ToInt64(reader["row_count"]),
                    Engine = reader["engine_name"]?.ToString() ?? "InnoDB"
                });
            }

            return Ok(ApiResult<List<TableStat>>.Ok(list));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<List<TableStat>>.Fail(ex.Message));
        }
    }

    [HttpGet("tables/{tableName}")]
    public async Task<IActionResult> GetTableDetail(string tableName, [FromQuery] int limit = 20)
    {
        // Sanitize tableName
        if (string.IsNullOrWhiteSpace(tableName) || tableName.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
        {
            return BadRequest(ApiResult<TableDetailDto>.Fail("Invalid table name."));
        }

        var detail = new TableDetailDto { TableName = tableName };
        try
        {
            using var conn = new MySqlConnection(GetConnectionString());
            await conn.OpenAsync();

            // 1. Column info
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_KEY, EXTRA
                    FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = 'myhrsuitelog' AND TABLE_NAME = @TableName
                    ORDER BY ORDINAL_POSITION;";
                cmd.Parameters.AddWithValue("@TableName", tableName);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string colKey = reader["COLUMN_KEY"]?.ToString() ?? "";
                    detail.Columns.Add(new ColumnDetailDto
                    {
                        ColumnName = reader["COLUMN_NAME"]?.ToString() ?? "",
                        DataType = reader["DATA_TYPE"]?.ToString() ?? "",
                        IsNullable = reader["IS_NULLABLE"]?.ToString() == "YES",
                        ColumnKey = colKey,
                        IsPrimaryKey = colKey.Equals("PRI", StringComparison.OrdinalIgnoreCase),
                        Extra = reader["EXTRA"]?.ToString()
                    });
                }
            }

            // 2. Row count
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"SELECT COUNT(*) FROM `{tableName}`";
                detail.RowCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            }

            // 3. Sample rows
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"SELECT * FROM `{tableName}` LIMIT @Limit";
                cmd.Parameters.AddWithValue("@Limit", Math.Min(limit, 100));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string col = reader.GetName(i);
                        row[col] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    detail.SampleRows.Add(row);
                }
            }

            return Ok(ApiResult<TableDetailDto>.Ok(detail));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResult<TableDetailDto>.Fail(ex.Message));
        }
    }
}
