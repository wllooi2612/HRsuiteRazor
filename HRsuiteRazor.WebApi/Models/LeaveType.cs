using System.Collections.Generic;

namespace Hrsuite.WebApi.Models;

public class LeaveType
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IndirectLeaveId { get; set; } = string.Empty;
    public decimal Entitle { get; set; }
    public decimal Taken { get; set; }
    public decimal Pending { get; set; }
    public decimal Balance { get; set; }
    public decimal YearTot { get; set; }
    public decimal CarryForward { get; set; }
    public decimal Adjust { get; set; }
    public decimal ExceedBf { get; set; }
    public decimal Exceed { get; set; }

    public string? EffectiveDate { get; set; }
    public string? ExpiryDate { get; set; }
    public bool IsReplacement { get; set; }
    public List<LeaveType> SubLeaves { get; set; } = new();
}
