using System;

namespace Hrsuite.WebApi.Models;

public class AttendanceRecord
{
    public string EMP_ID { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string DayType { get; set; } = string.Empty;
    public string ScheduleCode { get; set; } = string.Empty;
    public string PunchIn1 { get; set; } = string.Empty;
    public string PunchOut1 { get; set; } = string.Empty;
    public string PunchIn2 { get; set; } = string.Empty;
    public string PunchOut2 { get; set; } = string.Empty;
    public string LateIn { get; set; } = string.Empty;
    public string EarlyOut { get; set; } = string.Empty;
    public string OvertimeHours { get; set; } = string.Empty;
    public string LeaveHours { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
}

public class ClockPunchModel
{
    public string EmpId { get; set; } = string.Empty;
    public string PunchType { get; set; } = "IN"; // IN or OUT
    public string Location { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
