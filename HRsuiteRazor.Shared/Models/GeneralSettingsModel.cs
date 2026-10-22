using System;

namespace HRsuiteRazor.Shared.Models;

public class GeneralSettingsModel
{
    // Company Primary Key & Identification
    public string CoNo { get; set; } = "1";
    public string CompanyName { get; set; } = string.Empty;

    // Company Address & Contact Info
    public string Add1 { get; set; } = string.Empty;
    public string Add2 { get; set; } = string.Empty;
    public string Add3 { get; set; } = string.Empty;
    public string Tel { get; set; } = string.Empty;
    public string Fax { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string HttpMain { get; set; } = string.Empty;

    // Email Settings
    public string EmailType { get; set; } = "0"; // 0: SMTP, 1: Exchange Server
    public string EmailServer { get; set; } = string.Empty;
    public string EmailPort { get; set; } = "25";
    public string SMTPServer { get; set; } = string.Empty;
    public int? SMTPPort { get; set; } = 25;
    public string SMTPUserID { get; set; } = string.Empty;
    public string SMTPPassword { get; set; } = string.Empty;
    public string EmailDomain { get; set; } = string.Empty;
    public string EmailSSL { get; set; } = "N"; // Y/N
    public string CompanyEmail { get; set; } = string.Empty;
    public string EmailUserName { get; set; } = string.Empty;
    public string EmailPassword { get; set; } = string.Empty;

    // Log File Settings
    public string OpenMsg { get; set; } = "Y"; // Log file record Y/N
    public string LogFilePath { get; set; } = @"c:\logfile";

    // Path & Format Settings
    public string LocalhostAdd { get; set; } = string.Empty;
    public string DatetimeFormat { get; set; } = "yyyy-MM-dd";
    public string DatetimeFormatSQLServer { get; set; } = "yyyy-MM-dd";

    // Security & Flags
    public string AllowEmaildirectlink { get; set; } = "N"; // Y/N Allow Email direct login
    public string PasswordPayslip { get; set; } = "N"; // Y/N Password require payslip
    public string Offpwdemailmodify { get; set; } = "Y"; // Y/N Allow update employee password & email

    // Overtime (OT) Settings
    public DateTime? OTDateFrom { get; set; }
    public string OTInsertType { get; set; } = "H"; // H: Hour, T: Time
    public string OTBreakTimeType { get; set; } = "A"; // A: Auto, M: Manual, D: Define
    public string OTBreakTimeDefinition { get; set; } = string.Empty;
    public string OTCodeSelect { get; set; } = "Y"; // Y: Select, N: Fixed
    public int? TotalOtAllowPerDay { get; set; } = 5;
    public string OTReason { get; set; } = "Y"; // Y: Auto, N: Manual
    public string OTSubmitUnlimited { get; set; } = "Y"; // Y/N
    public decimal? OTPendingProcessInterval { get; set; } = 300;

    // Leave Settings
    public DateTime? LeaveDateFrom { get; set; }
    public string LEAVESubmitUnlimited { get; set; } = "Y"; // Y/N
    public decimal? LEAVEPendingProcessInterval { get; set; } = 300;
    public string OpenMysqlSchd { get; set; } = "Y"; // Open Timeman schedule Y/N
    public string LeaveRemindTimeGeneral { get; set; } = "13:00:00";
    public string AllowLeaveRemindTimeGeneral { get; set; } = "N"; // switch on/off Y/N

    // Claim Settings
    public DateTime? ClaimCutOffStart { get; set; }
    public DateTime? ClaimCutOffEnd { get; set; }
    public string ClaimIndividualCutOff { get; set; } = "N";
}

public class CompanyDetailModel
{
    public string CoNo { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Add1 { get; set; } = string.Empty;
    public string Add2 { get; set; } = string.Empty;
    public string Add3 { get; set; } = string.Empty;
    public string Tel { get; set; } = string.Empty;
    public string Fax { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string HttpMain { get; set; } = string.Empty;
}
