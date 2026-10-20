using System;

namespace Hrsuite.WebApi.Models;

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
