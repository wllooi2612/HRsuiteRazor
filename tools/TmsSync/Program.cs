using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using MySqlConnector;

namespace TmsSync;

public class Program
{
    private const string LocalTmsConnStr = "Server=localhost;Port=3309;Database=dw__app_demo2;User ID=dba;Password=sql8;AllowUserVariables=True;CharSet=utf8mb4;";
    private const string RailwayConnStr = "Server=zephyr.proxy.rlwy.net;Port=17717;Database=myhrsuitelog;User ID=root;Password=dSuiqexWYpWNYPNNEtbyePfJsVPbIeUp;AllowUserVariables=True;CharSet=utf8mb4;";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Syncing TMS Master Tables from Localhost:3309 to Railway Cloud ===");

        using var localConn = new MySqlConnection(LocalTmsConnStr);
        await localConn.OpenAsync();
        Console.WriteLine("[OK] Connected to Local MySQL (dw__app_demo2 on port 3309).");

        using var railwayConn = new MySqlConnection(RailwayConnStr);
        await railwayConn.OpenAsync();
        Console.WriteLine("[OK] Connected to Railway Cloud MySQL (myhrsuitelog).");

        var tablesToSync = new[]
        {
            "dw_dept",
            "dw_section",
            "dw_category",
            "dw_grade",
            "dw_super",
            "dw_job",
            "dw_factory",
            "dw_tms_wrkgrp",
            "dw_leave",
            "dw_ot"
        };

        foreach (var table in tablesToSync)
        {
            try
            {
                await SyncTableAsync(localConn, railwayConn, table);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to sync table {table}: {ex.Message}");
            }
        }

        // Check and sync aspnet_users name
        string localMyhrConnStr = "Server=localhost;Port=3309;Database=myhrsuitelog;User ID=dba;Password=sql8;AllowUserVariables=True;CharSet=utf8mb4;";
        
        using (var localMyhrConn = new MySqlConnection(localMyhrConnStr))
        {
            await localMyhrConn.OpenAsync();
            using var queryCmd = localMyhrConn.CreateCommand();
            queryCmd.CommandText = "SELECT GroupID, GroupCode, ItemCode, FormID, Name, Link_path FROM aspnet_masterfile WHERE GroupID = 'LEAVE' ORDER BY GroupCode, ItemCode;";
            using (var reader = await queryCmd.ExecuteReaderAsync())
            {
                Console.WriteLine("--- ASPNET_MASTERFILE (LEAVE) ---");
                while (await reader.ReadAsync())
                {
                    Console.WriteLine($"{reader["GroupCode"]}-{reader["ItemCode"]} | {reader["FormID"]} | {reader["Name"]} | {reader["Link_path"]}");
                }
            }

            using var queryCmd2 = localMyhrConn.CreateCommand();
            queryCmd2.CommandText = "SELECT DISTINCT GroupID, GroupCode, ItemCode, FormID, Name, Link_path FROM aspnet_useraccessright WHERE GroupID = 'LEAVE' ORDER BY GroupCode, ItemCode;";
            using (var reader2 = await queryCmd2.ExecuteReaderAsync())
            {
                Console.WriteLine("--- ASPNET_USERACCESSRIGHT (LEAVE) ---");
                while (await reader2.ReadAsync())
                {
                    Console.WriteLine($"{reader2["GroupCode"]}-{reader2["ItemCode"]} | {reader2["FormID"]} | {reader2["Name"]} | {reader2["Link_path"]}");
                }
            }
        }

        using (var cmd = railwayConn.CreateCommand())
        {
            cmd.CommandText = "SELECT ID, Name FROM aspnet_users WHERE ID = '00001'";
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                Console.WriteLine($"[Railway MySQL myhrsuitelog] User 00001 Name = {reader["Name"]}");
            }
        }

        using (var updateCmd = railwayConn.CreateCommand())
        {
            updateCmd.CommandText = "UPDATE aspnet_users SET Name = 'TAN HOCK WAN' WHERE ID = '00001'";
            await updateCmd.ExecuteNonQueryAsync();
            Console.WriteLine("[Railway MySQL myhrsuitelog] Updated 00001 -> TAN HOCK WAN.");
        }

        Console.WriteLine("=== Sync Completed ===");
    }

    private static async Task SyncTableAsync(MySqlConnection srcConn, MySqlConnection destConn, string tableName)
    {
        Console.Write($"Syncing table [{tableName}]... ");

        // 1. Get CREATE TABLE statement from source
        string createTableSql = "";
        using (var cmd = srcConn.CreateCommand())
        {
            cmd.CommandText = $"SHOW CREATE TABLE `{tableName}`";
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                createTableSql = reader.GetString(1);
            }
        }

        if (string.IsNullOrEmpty(createTableSql))
        {
            Console.WriteLine("Source table not found!");
            return;
        }

        // Adjust CREATE TABLE to IF NOT EXISTS
        createTableSql = createTableSql.Replace("CREATE TABLE", "CREATE TABLE IF NOT EXISTS");

        // 2. Execute CREATE TABLE on destination
        using (var cmd = destConn.CreateCommand())
        {
            cmd.CommandText = createTableSql;
            await cmd.ExecuteNonQueryAsync();
        }

        // 3. Truncate / clear destination table
        using (var cmd = destConn.CreateCommand())
        {
            cmd.CommandText = $"TRUNCATE TABLE `{tableName}`";
            await cmd.ExecuteNonQueryAsync();
        }

        // 4. Read all data from source
        var dataTable = new DataTable();
        using (var cmd = srcConn.CreateCommand())
        {
            cmd.CommandText = $"SELECT * FROM `{tableName}`";
            using var reader = await cmd.ExecuteReaderAsync();
            dataTable.Load(reader);
        }

        if (dataTable.Rows.Count == 0)
        {
            Console.WriteLine($"0 rows (empty).");
            return;
        }

        // 5. Insert rows into destination in batches
        var cols = new List<string>();
        foreach (DataColumn c in dataTable.Columns)
        {
            cols.Add($"`{c.ColumnName}`");
        }
        string colList = string.Join(", ", cols);

        int batchSize = 100;
        int inserted = 0;

        for (int i = 0; i < dataTable.Rows.Count; i += batchSize)
        {
            var sb = new StringBuilder();
            sb.Append($"INSERT INTO `{tableName}` ({colList}) VALUES ");

            int countInBatch = Math.Min(batchSize, dataTable.Rows.Count - i);
            using var cmd = destConn.CreateCommand();

            for (int j = 0; j < countInBatch; j++)
            {
                if (j > 0) sb.Append(", ");
                sb.Append("(");
                var row = dataTable.Rows[i + j];
                for (int c = 0; c < dataTable.Columns.Count; c++)
                {
                    if (c > 0) sb.Append(", ");
                    string paramName = $"@p_{j}_{c}";
                    sb.Append(paramName);

                    object val = row[c];
                    if (val == DBNull.Value || val == null)
                    {
                        cmd.Parameters.AddWithValue(paramName, DBNull.Value);
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue(paramName, val);
                    }
                }
                sb.Append(")");
            }

            cmd.CommandText = sb.ToString();
            await cmd.ExecuteNonQueryAsync();
            inserted += countInBatch;
        }

        Console.WriteLine($"[OK] {inserted} rows synced.");
    }
}
