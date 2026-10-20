namespace HRsuiteRazor.Shared.Models;

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

public class PayslipSummary
{
    public string Period { get; set; } = string.Empty;
    public string Cycle { get; set; } = "1";
    public DateTime PayDate { get; set; }
    public decimal BasicPay { get; set; }
    public decimal TotalAllowance { get; set; }
    public decimal TotalDeduction { get; set; }
    public decimal EpfEmployee { get; set; }
    public decimal SocsoEmployee { get; set; }
    public decimal EisEmployee { get; set; }
    public decimal TaxPcb { get; set; }
    public decimal GrossPay { get; set; }
    public decimal NetPay { get; set; }
}

public class EAFormRecord
{
    public int Year { get; set; }
    public string EmpId { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public string IcNo { get; set; } = string.Empty;
    public decimal GrossIncome { get; set; }
    public decimal TotalEpf { get; set; }
    public decimal TotalSocso { get; set; }
    public decimal TotalTax { get; set; }
}

public class CalendarEventModel
{
    public string Date { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class DashboardSummaryModel
{
    public int PendingLeavesCount { get; set; }
    public string WelcomeMessage { get; set; } = string.Empty;
}
