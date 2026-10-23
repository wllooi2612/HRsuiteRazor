using System.Net.Http.Json;
using System.Text.Json;
using HRsuiteRazor.Shared.Models;

namespace HRsuiteRazor.Services;

public class HrApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<HrApiClient> _logger;

    public HrApiClient(IHttpClientFactory httpClientFactory, ILogger<HrApiClient> logger)
    {
        _http = httpClientFactory.CreateClient("WebApi");
        _logger = logger;
    }

    // -------------------------------------------------------------
    // Database Diagnostics & Schema Inspection
    // -------------------------------------------------------------

    public async Task<DbStatusInfo> GetDatabaseStatusAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResult<DbStatusInfo>>("api/Database/status");
            if (res?.Data != null) return res.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call api/Database/status");
        }

        return new DbStatusInfo
        {
            IsConnected = false,
            ErrorMessage = "Could not connect to Web API at " + _http.BaseAddress
        };
    }

    public async Task<List<TableStat>> GetTableStatsAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResult<List<TableStat>>>("api/Database/tables");
            return res?.Data ?? new List<TableStat>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call api/Database/tables");
            return new List<TableStat>();
        }
    }

    public async Task<TableDetailDto?> GetTableDetailAsync(string tableName, int limit = 20)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResult<TableDetailDto>>($"api/Database/tables/{tableName}?limit={limit}");
            return res?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call api/Database/tables/{TableName}", tableName);
            return null;
        }
    }

    // -------------------------------------------------------------
    // Staff & Users
    // -------------------------------------------------------------

    public async Task<List<StaffDirectoryItem>> GetUsersAsync(string? search = null)
    {
        try
        {
            string url = string.IsNullOrWhiteSpace(search) 
                ? "api/Staff/directory" 
                : $"api/Staff/directory?search={Uri.EscapeDataString(search)}";

            var items = await _http.GetFromJsonAsync<List<StaffDirectoryItem>>(url);
            return items ?? new List<StaffDirectoryItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call api/Staff/directory");
            return new List<StaffDirectoryItem>();
        }
    }

    public async Task<StaffProfile?> GetStaffProfileAsync(string empId)
    {
        try
        {
            return await _http.GetFromJsonAsync<StaffProfile>($"api/Staff/profile?empId={Uri.EscapeDataString(empId)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call api/Staff/profile");
            return null;
        }
    }

    public async Task<bool> UpdateStaffProfileAsync(UpdateStaffProfileRequest req)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/Staff/profile", req);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update staff profile");
            return false;
        }
    }

    public async Task<bool> CreateUserAsync(AdminUserModel user)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/Admin/users", user);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user via Web API");
            return false;
        }
    }

    public async Task<bool> UpdateUserAsync(string id, AdminUserModel user)
    {
        try
        {
            var res = await _http.PutAsJsonAsync($"api/Admin/users/{Uri.EscapeDataString(id)}", user);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user via Web API");
            return false;
        }
    }

    // -------------------------------------------------------------
    // Leaves
    // -------------------------------------------------------------

    public async Task<List<LeaveRequest>> GetLeavesAsync(string? userId = null)
    {
        try
        {
            string url = string.IsNullOrWhiteSpace(userId) 
                ? "api/Leave/history" 
                : $"api/Leave/history?userId={Uri.EscapeDataString(userId)}";

            var items = await _http.GetFromJsonAsync<List<LeaveRequest>>(url);
            return items ?? new List<LeaveRequest>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch leaves from Web API");
            return new List<LeaveRequest>();
        }
    }

    public async Task<List<LeaveType>> GetLeaveTypesAsync(string? userId = null)
    {
        try
        {
            string url = string.IsNullOrWhiteSpace(userId) 
                ? "api/Leave/types" 
                : $"api/Leave/types?userId={Uri.EscapeDataString(userId)}";

            var items = await _http.GetFromJsonAsync<List<LeaveType>>(url);
            return items ?? new List<LeaveType>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch leave types from Web API");
            return new List<LeaveType>();
        }
    }

    public async Task<bool> ApplyLeaveAsync(LeaveApplyModel model)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/Leave/apply", model);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply leave via Web API");
            return false;
        }
    }

    public async Task<bool> ApproveLeaveAsync(ApproveActionModel model)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/Leave/approve", model);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve leave via Web API");
            return false;
        }
    }

    // -------------------------------------------------------------
    // Overtime
    // -------------------------------------------------------------

    public async Task<List<OTRecord>> GetOtHistoryAsync(string? userId = null, int? year = null)
    {
        try
        {
            string url = $"api/OT/history?userId={Uri.EscapeDataString(userId ?? "")}&year={year ?? DateTime.Now.Year}";
            var items = await _http.GetFromJsonAsync<List<OTRecord>>(url);
            return items ?? new List<OTRecord>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch OT history from Web API");
            return new List<OTRecord>();
        }
    }

    public async Task<bool> ApplyOtAsync(OTApplyModel model)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/OT/apply", model);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply OT via Web API");
            return false;
        }
    }

    public async Task<bool> ApproveOtAsync(OTApproveModel model)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/OT/approve", model);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve OT via Web API");
            return false;
        }
    }

    // -------------------------------------------------------------
    // Claims
    // -------------------------------------------------------------

    public async Task<List<ClaimRecord>> GetClaimsAsync(string? userId = null, int? year = null)
    {
        try
        {
            string url = $"api/Claim/history?userId={Uri.EscapeDataString(userId ?? "")}&year={year ?? DateTime.Now.Year}";
            var items = await _http.GetFromJsonAsync<List<ClaimRecord>>(url);
            return items ?? new List<ClaimRecord>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch claims from Web API");
            return new List<ClaimRecord>();
        }
    }

    public async Task<List<ClaimType>> GetClaimTypesAsync(string? userId = null)
    {
        try
        {
            string url = $"api/Claim/types?userId={Uri.EscapeDataString(userId ?? "")}";
            var items = await _http.GetFromJsonAsync<List<ClaimType>>(url);
            return items ?? new List<ClaimType>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch claim types from Web API");
            return new List<ClaimType>();
        }
    }

    public async Task<bool> ApplyClaimAsync(ClaimApplyModel model)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/Claim/apply", model);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply claim via Web API");
            return false;
        }
    }

    // -------------------------------------------------------------
    // Generic Database CRUD (Select, Insert, Update, Delete)
    // -------------------------------------------------------------

    public async Task<List<Dictionary<string, object?>>> SelectTableAsync(string tableName, int limit = 50, int offset = 0)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResult<List<Dictionary<string, object?>>>>(
                $"api/DbCrud/{tableName}?limit={limit}&offset={offset}");
            return res?.Data ?? new List<Dictionary<string, object?>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select records from {TableName}", tableName);
            return new List<Dictionary<string, object?>>();
        }
    }

    public async Task<GenericCrudResponse> InsertRecordAsync(string tableName, Dictionary<string, object?> data)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"api/DbCrud/{tableName}", data);
            return await res.Content.ReadFromJsonAsync<GenericCrudResponse>() 
                ?? new GenericCrudResponse { Success = false, Message = "Empty response" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert into {TableName}", tableName);
            return new GenericCrudResponse { Success = false, Message = ex.Message };
        }
    }

    public async Task<GenericCrudResponse> UpdateRecordAsync(string tableName, string idCol, string idVal, Dictionary<string, object?> data)
    {
        try
        {
            var res = await _http.PutAsJsonAsync($"api/DbCrud/{tableName}/{idCol}/{idVal}", data);
            return await res.Content.ReadFromJsonAsync<GenericCrudResponse>() 
                ?? new GenericCrudResponse { Success = false, Message = "Empty response" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update {TableName}", tableName);
            return new GenericCrudResponse { Success = false, Message = ex.Message };
        }
    }

    public async Task<GenericCrudResponse> DeleteRecordAsync(string tableName, string idCol, string idVal)
    {
        try
        {
            var res = await _http.DeleteAsync($"api/DbCrud/{tableName}/{idCol}/{idVal}");
            return await res.Content.ReadFromJsonAsync<GenericCrudResponse>() 
                ?? new GenericCrudResponse { Success = false, Message = "Empty response" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete from {TableName}", tableName);
            return new GenericCrudResponse { Success = false, Message = ex.Message };
        }
    }

    // -------------------------------------------------------------
    // Admin Module (Users, Groups, Permissions, Logs)
    // -------------------------------------------------------------

    public async Task<List<AdminUserModel>> GetAdminUsersAsync()
    {
        try
        {
            var items = await _http.GetFromJsonAsync<List<AdminUserModel>>("api/Admin/users");
            return items ?? new List<AdminUserModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch admin users from Web API");
            return new List<AdminUserModel>();
        }
    }

    public async Task<bool> CreateAdminUserAsync(AdminUserModel user)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/Admin/users", user);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user via Web API");
            return false;
        }
    }

    public async Task<List<UserGroupModel>> GetAdminGroupsAsync()
    {
        try
        {
            var items = await _http.GetFromJsonAsync<List<UserGroupModel>>("api/Admin/groups");
            return items ?? new List<UserGroupModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch admin groups from Web API");
            return new List<UserGroupModel>();
        }
    }

    public async Task<List<CompanyModel>> GetCompaniesAsync()
    {
        try
        {
            var items = await _http.GetFromJsonAsync<List<CompanyModel>>("api/Admin/companies");
            return items ?? new List<CompanyModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch companies from Web API");
            return new List<CompanyModel>();
        }
    }

    public async Task<List<EmployeeLookupModel>> GetEmployeesAsync()
    {
        try
        {
            var items = await _http.GetFromJsonAsync<List<EmployeeLookupModel>>("api/Admin/employees");
            return items ?? new List<EmployeeLookupModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch employees from Web API");
            return new List<EmployeeLookupModel>();
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateAdminUserWithDetailsAsync(string id, AdminUserModel user)
    {
        try
        {
            var res = await _http.PutAsJsonAsync($"api/Admin/users/{Uri.EscapeDataString(id)}", user);
            if (!res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadAsStringAsync();
                return (false, string.IsNullOrWhiteSpace(content) ? res.ReasonPhrase : content);
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user via Web API");
            return (false, ex.Message);
        }
    }

    public async Task<bool> UpdateAdminUserAsync(string id, AdminUserModel user)
    {
        var (success, _) = await UpdateAdminUserWithDetailsAsync(id, user);
        return success;
    }

    public async Task<bool> DeleteAdminUserAsync(string id)
    {
        try
        {
            var res = await _http.DeleteAsync($"api/Admin/users/{Uri.EscapeDataString(id)}");
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user via Web API");
            return false;
        }
    }

    public async Task<string?> ResetUserPasswordAsync(string id)
    {
        try
        {
            var res = await _http.PostAsync($"api/Admin/users/{Uri.EscapeDataString(id)}/reset-password", null);
            if (res.IsSuccessStatusCode)
            {
                var result = await res.Content.ReadFromJsonAsync<JsonElement>();
                if (result.TryGetProperty("temporaryPassword", out var pwd))
                {
                    return pwd.GetString();
                }
                return "123456";
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset password via Web API");
            return null;
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> CreateAdminGroupWithDetailsAsync(UserGroupModel group)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/Admin/groups", group);
            if (!res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadAsStringAsync();
                return (false, string.IsNullOrWhiteSpace(content) ? res.ReasonPhrase : content);
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create group via Web API");
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateAdminGroupWithDetailsAsync(string userGroupId, string groupId, UserGroupModel group)
    {
        try
        {
            var res = await _http.PutAsJsonAsync($"api/Admin/groups/{Uri.EscapeDataString(userGroupId)}/{Uri.EscapeDataString(groupId)}", group);
            if (!res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadAsStringAsync();
                return (false, string.IsNullOrWhiteSpace(content) ? res.ReasonPhrase : content);
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update group via Web API");
            return (false, ex.Message);
        }
    }

    public async Task<bool> DeleteAdminGroupAsync(string userGroupId, string groupId)
    {
        try
        {
            var res = await _http.DeleteAsync($"api/Admin/groups/{Uri.EscapeDataString(userGroupId)}/{Uri.EscapeDataString(groupId)}");
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete group via Web API");
            return false;
        }
    }

    public async Task<List<PermissionPageModel>> GetPermissionsAsync(string userGroupId, string groupId)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<List<PermissionPageModel>>($"api/Admin/permissions?userGroupId={Uri.EscapeDataString(userGroupId)}&groupId={Uri.EscapeDataString(groupId)}");
            return res ?? new List<PermissionPageModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get permissions via Web API");
            return new List<PermissionPageModel>();
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> SavePermissionsAsync(SavePermissionsRequest request)
    {
        try
        {
            var res = await _http.PutAsJsonAsync("api/Admin/permissions", request);
            if (!res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadAsStringAsync();
                return (false, string.IsNullOrWhiteSpace(content) ? res.ReasonPhrase : content);
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save permissions via Web API");
            return (false, ex.Message);
        }
    }

    public async Task<List<QdeEmployeeModel>> GetQdeEmployeesAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<List<QdeEmployeeModel>>("api/Admin/qde-employees");
            return res ?? new List<QdeEmployeeModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch QDE employees via Web API");
            return new List<QdeEmployeeModel>();
        }
    }

    public async Task<(bool Success, BatchCreateUsersResult? Result, string? ErrorMessage)> BatchCreateUsersAsync(BatchCreateUsersRequest request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/Admin/batch-create-users", request);
            if (res.IsSuccessStatusCode)
            {
                var result = await res.Content.ReadFromJsonAsync<BatchCreateUsersResult>();
                return (true, result, null);
            }
            var err = await res.Content.ReadAsStringAsync();
            return (false, null, string.IsNullOrWhiteSpace(err) ? res.ReasonPhrase : err);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to batch create users via Web API");
            return (false, null, ex.Message);
        }
    }

    // -------------------------------------------------------------
    // General Settings & Company Admin
    // -------------------------------------------------------------

    public async Task<GeneralSettingsModel?> GetGeneralSettingsAsync(string coNo = "1")
    {
        try
        {
            return await _http.GetFromJsonAsync<GeneralSettingsModel>($"api/Admin/general/{Uri.EscapeDataString(coNo)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get general settings for CoNo={CoNo}", coNo);
            return null;
        }
    }

    public async Task<List<CompanyDetailModel>> GetCompanyDetailsAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<List<CompanyDetailModel>>("api/Admin/general/companies-detail");
            return res ?? new List<CompanyDetailModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get company details");
            return new List<CompanyDetailModel>();
        }
    }

    public async Task<(bool Success, string? Message)> SaveGeneralSettingsAsync(GeneralSettingsModel model)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/Admin/general", model);
            if (res.IsSuccessStatusCode)
            {
                return (true, "Settings saved successfully.");
            }
            var err = await res.Content.ReadAsStringAsync();
            return (false, string.IsNullOrWhiteSpace(err) ? res.ReasonPhrase : err);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save general settings");
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Message)> SaveCompanyAsync(CompanyDetailModel model)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/Admin/general/company", model);
            if (res.IsSuccessStatusCode)
            {
                return (true, "Company saved successfully.");
            }
            var err = await res.Content.ReadAsStringAsync();
            return (false, string.IsNullOrWhiteSpace(err) ? res.ReasonPhrase : err);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save company");
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Message)> DeleteCompanyAsync(string coNo)
    {
        try
        {
            var res = await _http.DeleteAsync($"api/Admin/general/company/{Uri.EscapeDataString(coNo)}");
            if (res.IsSuccessStatusCode)
            {
                return (true, "Company deleted successfully.");
            }
            var err = await res.Content.ReadAsStringAsync();
            return (false, string.IsNullOrWhiteSpace(err) ? res.ReasonPhrase : err);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete company CoNo={CoNo}", coNo);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Message)> TestEmailConnectionAsync(GeneralSettingsModel model)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/Admin/general/test-email", model);
            if (res.IsSuccessStatusCode)
            {
                var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
                bool success = doc.TryGetProperty("success", out var s) && s.GetBoolean();
                string msg = doc.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "Connection tested.";
                return (success, msg);
            }
            var err = await res.Content.ReadAsStringAsync();
            return (false, string.IsNullOrWhiteSpace(err) ? "Test request failed" : err);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test email connection");
            return (false, ex.Message);
        }
    }

    // -------------------------------------------------------------
    // LOG VIEWER & AUDIT LOGS
    // -------------------------------------------------------------

    public async Task<LogQueryResult> QueryLogsAsync(LogQueryRequest request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/Admin/logs/query", request);
            if (res.IsSuccessStatusCode)
            {
                var data = await res.Content.ReadFromJsonAsync<LogQueryResult>();
                return data ?? new LogQueryResult();
            }
            _logger.LogWarning("Failed to query logs: {StatusCode}", res.StatusCode);
            return new LogQueryResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling api/Admin/logs/query");
            return new LogQueryResult();
        }
    }

    public async Task<(bool Success, string? Message)> SeedSampleLogsAsync(int count = 60)
    {
        try
        {
            var res = await _http.PostAsync($"api/Admin/logs/seed-sample?count={count}", null);
            if (res.IsSuccessStatusCode)
            {
                return (true, "Sample logs generated successfully.");
            }
            var err = await res.Content.ReadAsStringAsync();
            return (false, err);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling api/Admin/logs/seed-sample");
            return (false, ex.Message);
        }
    }
}



