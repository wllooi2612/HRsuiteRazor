using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using MySqlConnector;

namespace DbMigrator;

public class Program
{
    private const string SqlServerConnStr = "Data Source=DEVSVR\\SQLEXPRESS;Initial Catalog=Myhrsuitelog;Integrated Security=True;TrustServerCertificate=True;";
    private const string MySqlRootConnStr = "Server=localhost;Port=3309;User ID=dba;Password=sql8;AllowUserVariables=True;CharSet=utf8mb4;";
    private const string TargetDb = "myhrsuitelog";

    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine(" Myhrsuitelog -> MySQL Database Migration Tool   ");
        Console.WriteLine("=================================================");

        try
        {
            // 1. Create MySQL Database if not exists
            using (var mySqlInitConn = new MySqlConnection(MySqlRootConnStr))
            {
                await mySqlInitConn.OpenAsync();
                using var cmd = mySqlInitConn.CreateCommand();
                cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{TargetDb}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                await cmd.ExecuteNonQueryAsync();
                Console.WriteLine($"[OK] MySQL database `{TargetDb}` checked/created.");
            }

            string mySqlConnStr = $"Server=localhost;Port=3309;Database={TargetDb};User ID=dba;Password=sql8;AllowUserVariables=True;CharSet=utf8mb4;DefaultCommandTimeout=120;";

            using var sqlConn = new SqlConnection(SqlServerConnStr);
            await sqlConn.OpenAsync();
            Console.WriteLine("[OK] Connected to SQL Server (DEVSVR\\SQLEXPRESS -> Myhrsuitelog).");

            using var myConn = new MySqlConnection(mySqlConnStr);
            await myConn.OpenAsync();
            Console.WriteLine($"[OK] Connected to MySQL (localhost:3309 -> {TargetDb}).");

            // Disable foreign key checks for clean migration
            using (var disableFk = myConn.CreateCommand())
            {
                disableFk.CommandText = "SET FOREIGN_KEY_CHECKS = 0;";
                await disableFk.ExecuteNonQueryAsync();
            }

            // 2. Get list of tables
            var tables = new List<string>();
            using (var cmd = sqlConn.CreateCommand())
            {
                cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME;";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string t = reader.GetString(0);
                    if (t.Equals("sysdiagrams", StringComparison.OrdinalIgnoreCase)) continue;
                    tables.Add(t);
                }
            }

            Console.WriteLine($"[INFO] Found {tables.Count} tables to migrate.");

            int totalSuccess = 0;
            int totalFailed = 0;

            foreach (var table in tables)
            {
                Console.Write($"Migrating [{table}] ... ");
                try
                {
                    await MigrateTableAsync(sqlConn, myConn, table);
                    totalSuccess++;
                }
                catch (Exception ex)
                {
                    totalFailed++;
                    Console.WriteLine($"\n[ERROR] Failed migrating table {table}: {ex.Message}");
                }
            }

            // Re-enable foreign key checks
            using (var enableFk = myConn.CreateCommand())
            {
                enableFk.CommandText = "SET FOREIGN_KEY_CHECKS = 1;";
                await enableFk.ExecuteNonQueryAsync();
            }

            Console.WriteLine("\n=================================================");
            Console.WriteLine($"Migration Complete! Success: {totalSuccess}, Failed: {totalFailed}");
            Console.WriteLine("=================================================");

            // Verification Summary
            Console.WriteLine("\nVerifying row counts between SQL Server and MySQL:");
            int mismatched = 0;
            foreach (var table in tables)
            {
                long sqlCount = 0;
                using (var cmd = sqlConn.CreateCommand())
                {
                    cmd.CommandText = $"SELECT COUNT(*) FROM [{table}]";
                    sqlCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                }

                long myCount = 0;
                using (var cmd = myConn.CreateCommand())
                {
                    cmd.CommandText = $"SELECT COUNT(*) FROM `{table}`";
                    myCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                }

                if (sqlCount == myCount)
                {
                    Console.WriteLine($" - {table,-35}: {sqlCount,6} rows (MATCH)");
                }
                else
                {
                    Console.WriteLine($" ! {table,-35}: SQL={sqlCount}, MySQL={myCount} (MISMATCH!)");
                    mismatched++;
                }
            }

            Console.WriteLine(mismatched == 0
                ? "\n[ALL VERIFIED] 100% data row counts match exactly!"
                : $"\n[WARNING] {mismatched} tables have mismatched row counts.");

            return mismatched == 0 && totalFailed == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FATAL] {ex}");
            return 1;
        }
    }

    private class ColumnInfo
    {
        public string Name { get; set; } = "";
        public string DataType { get; set; } = "";
        public int MaxLength { get; set; }
        public int Precision { get; set; }
        public int Scale { get; set; }
        public bool IsNullable { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsPrimaryKey { get; set; }
    }

    private static async Task MigrateTableAsync(SqlConnection sqlConn, MySqlConnection myConn, string tableName)
    {
        // 1. Get primary keys
        var pks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = sqlConn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT c.name AS ColumnName
                FROM sys.tables t
                JOIN sys.indexes i ON t.object_id = i.object_id AND i.is_primary_key = 1
                JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                WHERE t.name = @TableName
                ORDER BY ic.key_ordinal;";
            cmd.Parameters.AddWithValue("@TableName", tableName);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                pks.Add(reader.GetString(0));
            }
        }

        // 2. Get columns
        var cols = new List<ColumnInfo>();
        using (var cmd = sqlConn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT 
                    c.name AS ColumnName,
                    t.name AS DataType,
                    c.max_length,
                    c.precision,
                    c.scale,
                    c.is_nullable,
                    c.is_identity
                FROM sys.tables tbl
                JOIN sys.columns c ON tbl.object_id = c.object_id
                JOIN sys.types t ON c.user_type_id = t.user_type_id
                WHERE tbl.name = @TableName
                ORDER BY c.column_id;";
            cmd.Parameters.AddWithValue("@TableName", tableName);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string colName = reader.GetString(0);
                cols.Add(new ColumnInfo
                {
                    Name = colName,
                    DataType = reader.GetString(1).ToLowerInvariant(),
                    MaxLength = reader.GetInt16(2),
                    Precision = reader.GetByte(3),
                    Scale = reader.GetByte(4),
                    IsNullable = reader.GetBoolean(5),
                    IsIdentity = reader.GetBoolean(6),
                    IsPrimaryKey = pks.Contains(colName)
                });
            }
        }

        // 3. Drop existing MySQL table and recreate
        using (var cmd = myConn.CreateCommand())
        {
            cmd.CommandText = $"DROP TABLE IF EXISTS `{tableName}`;";
            await cmd.ExecuteNonQueryAsync();
        }

        var ddl = new StringBuilder();
        ddl.Append($"CREATE TABLE `{tableName}` (\n");
        var colDefs = new List<string>();

        foreach (var col in cols)
        {
            string myType = MapToMySqlType(col);
            string nullStr = col.IsNullable ? "NULL" : "NOT NULL";
            string autoInc = (col.IsIdentity && col.IsPrimaryKey && pks.Count == 1) ? "AUTO_INCREMENT" : "";
            colDefs.Add($"  `{col.Name}` {myType} {nullStr} {autoInc}".TrimEnd());
        }

        if (pks.Count > 0)
        {
            var pkList = string.Join(", ", pks.Select(p => $"`{p}`"));
            colDefs.Add($"  PRIMARY KEY ({pkList})");
        }

        ddl.Append(string.Join(",\n", colDefs));
        ddl.Append("\n) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;");

        using (var cmd = myConn.CreateCommand())
        {
            cmd.CommandText = ddl.ToString();
            await cmd.ExecuteNonQueryAsync();
        }

        // 4. Copy rows from SQL Server to MySQL
        using var readCmd = sqlConn.CreateCommand();
        readCmd.CommandText = $"SELECT * FROM [{tableName}]";
        using var sqlReader = await readCmd.ExecuteReaderAsync();

        int rowCount = 0;
        var batch = new List<object?[]>();

        while (await sqlReader.ReadAsync())
        {
            var values = new object?[cols.Count];
            for (int i = 0; i < cols.Count; i++)
            {
                if (await sqlReader.IsDBNullAsync(i))
                {
                    values[i] = null;
                }
                else
                {
                    var val = sqlReader.GetValue(i);
                    values[i] = val;
                }
            }
            batch.Add(values);
            rowCount++;

            if (batch.Count >= 200)
            {
                await InsertBatchAsync(myConn, tableName, cols, batch);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await InsertBatchAsync(myConn, tableName, cols, batch);
            batch.Clear();
        }

        Console.WriteLine($"Done ({rowCount} rows)");
    }

    private static async Task InsertBatchAsync(MySqlConnection myConn, string tableName, List<ColumnInfo> cols, List<object?[]> batch)
    {
        if (batch.Count == 0) return;

        var sb = new StringBuilder();
        var colNames = string.Join(", ", cols.Select(c => $"`{c.Name}`"));
        sb.Append($"INSERT INTO `{tableName}` ({colNames}) VALUES ");

        using var cmd = myConn.CreateCommand();

        for (int r = 0; r < batch.Count; r++)
        {
            if (r > 0) sb.Append(", ");
            sb.Append("(");
            for (int c = 0; c < cols.Count; c++)
            {
                if (c > 0) sb.Append(", ");
                string paramName = $"@p_{r}_{c}";
                sb.Append(paramName);

                var val = batch[r][c];
                var param = cmd.CreateParameter();
                param.ParameterName = paramName;
                param.Value = val ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
            sb.Append(")");
        }

        cmd.CommandText = sb.ToString();
        await cmd.ExecuteNonQueryAsync();
    }

    private static string MapToMySqlType(ColumnInfo col)
    {
        switch (col.DataType)
        {
            case "int":
                return "INT";
            case "smallint":
                return "SMALLINT";
            case "tinyint":
                return "TINYINT";
            case "bigint":
                return "BIGINT";
            case "bit":
                return "TINYINT(1)";
            case "decimal":
            case "numeric":
                return $"DECIMAL({col.Precision},{col.Scale})";
            case "float":
            case "real":
                return "DOUBLE";
            case "money":
            case "smallmoney":
                return "DECIMAL(19,4)";
            case "datetime":
            case "datetime2":
            case "smalldatetime":
                return "DATETIME";
            case "date":
                return "DATE";
            case "time":
                return "TIME";
            case "nvarchar":
                if (col.MaxLength == -1 || col.MaxLength > 1000) return "LONGTEXT";
                // In SQL Server, nvarchar max_length in sys.columns is bytes (2 bytes per char)
                int ncharLen = col.MaxLength / 2;
                if (ncharLen <= 0) ncharLen = 255;
                if (ncharLen > 500) return "TEXT";
                return $"VARCHAR({ncharLen})";
            case "varchar":
                if (col.MaxLength == -1 || col.MaxLength > 1000) return "LONGTEXT";
                int charLen = col.MaxLength <= 0 ? 255 : col.MaxLength;
                if (charLen > 500) return "TEXT";
                return $"VARCHAR({charLen})";
            case "ntext":
            case "text":
                return "LONGTEXT";
            case "uniqueidentifier":
                return "VARCHAR(36)";
            case "image":
            case "varbinary":
            case "binary":
                return "LONGBLOB";
            default:
                return "TEXT";
        }
    }
}
