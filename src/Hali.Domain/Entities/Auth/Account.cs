using Hali.Domain.Enums;

namespace Hali.Domain.Entities.Auth;

public class Account
{
    public Guid Id { get; set; }
    public AccountType AccountType { get; set; } = AccountType.Citizen;
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? PhoneE164 { get; set; }
    public bool IsPhoneVerified { get; set; }
    public bool IsEmailVerified { get; set; }
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
