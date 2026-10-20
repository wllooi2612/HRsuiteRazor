namespace HRsuiteRazor.Shared.Models;

public class DbStatusInfo
{
    public bool IsConnected { get; set; }
    public string ServerVersion { get; set; } = "";
    public string DatabaseName { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public int TotalTables { get; set; }
    public long TotalRows { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TableStat
{
    public string TableName { get; set; } = "";
    public long RowCount { get; set; }
    public string Engine { get; set; } = "InnoDB";
}

public class ColumnDetailDto
{
    public string ColumnName { get; set; } = "";
    public string DataType { get; set; } = "";
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public string? ColumnKey { get; set; }
    public string? Extra { get; set; }
}

public class TableDetailDto
{
    public string TableName { get; set; } = "";
    public long RowCount { get; set; }
    public List<ColumnDetailDto> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> SampleRows { get; set; } = new();
}

public class ApiResult<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    public static ApiResult<T> Ok(T data, string? message = null) => new() { Success = true, Data = data, Message = message };
    public static ApiResult<T> Fail(string message) => new() { Success = false, Message = message };
}

public class GenericCrudResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int RowsAffected { get; set; }
    public object? InsertedId { get; set; }
}
