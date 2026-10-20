namespace HRsuiteRazor.Shared.Models;

public class StaffProfile
{
    public string EmpId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IcNew { get; set; } = string.Empty;
    public DateTime? Birthdate { get; set; }
    public string Nationality { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string HpNo { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MaritalStatus { get; set; } = string.Empty;
    public string PassportNo { get; set; } = string.Empty;
    public DateTime? PassportExpiry { get; set; }
    public string HostelNo { get; set; } = string.Empty;
    public string LockerNo { get; set; } = string.Empty;

    public string SpouseName { get; set; } = string.Empty;
    public string SpouseIc { get; set; } = string.Empty;
    public string SpouseTaxNo { get; set; } = string.Empty;
    public string SpouseEpf { get; set; } = string.Empty;
    public int ChildrenCount { get; set; }

    public string EmergencyContactName { get; set; } = string.Empty;
    public string EmergencyContactPhone { get; set; } = string.Empty;
    public string EmergencyContactRelation { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
}

public class StaffDirectoryItem
{
    public string EmpId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string HpNo { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class UpdateStaffProfileRequest
{
    public string EmpId { get; set; } = string.Empty;
    public string HpNo { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string EmergencyContactName { get; set; } = string.Empty;
    public string EmergencyContactPhone { get; set; } = string.Empty;
    public string EmergencyContactRelation { get; set; } = string.Empty;
}
