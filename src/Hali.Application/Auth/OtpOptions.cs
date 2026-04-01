namespace Hali.Application.Auth;

public class OtpOptions
{
    public const string Section = "Otp";

    public int Length { get; set; } = 6;
    public int TtlMinutes { get; set; } = 10;
    public int MaxRequestsPerWindow { get; set; } = 5;
    public int WindowMinutes { get; set; } = 10;
}
