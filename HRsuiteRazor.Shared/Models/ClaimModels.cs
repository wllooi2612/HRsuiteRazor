namespace HRsuiteRazor.Shared.Models;

public class ClaimType
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal MaxLimit { get; set; }
}

public class ClaimRecord
{
    public string ClaimID { get; set; } = string.Empty;
    public string ClaimNo { get; set; } = string.Empty;
    public string Emp_ID { get; set; } = string.Empty;
    public string EmpName { get; set; } = string.Empty;
    public DateTime Claim_Apply_DATE { get; set; }
    public decimal ClaimTotal { get; set; }
    public decimal ApprovedAmount { get; set; }
    public string Claim_STATUS { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ReceiptNo { get; set; } = string.Empty;
    public string Clinic_ID { get; set; } = string.Empty;
    public string AppLevel { get; set; } = string.Empty;
    public DateTime? CreatedDate { get; set; }
}

public class ClaimApplyModel
{
    public string EmpId { get; set; } = string.Empty;
    public string ClaimCode { get; set; } = string.Empty;
    public DateTime ClaimDate { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }
    public string ReceiptNo { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
}

public class ClaimApproveModel
{
    public string ClaimID { get; set; } = string.Empty;
    public string EmpId { get; set; } = string.Empty;
    public string Action { get; set; } = "APPROVE";
    public decimal ApprovedAmount { get; set; }
    public string Remarks { get; set; } = string.Empty;
}
