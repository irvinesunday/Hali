using System;
using System.Text;
using Hali.Application.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Hali.Tests.Unit.Auth;

/// <summary>
/// Unit coverage for <see cref="TotpService"/>'s pure pieces — base32
/// codec, RFC 6238 code computation, recovery-code shape, and
/// current-window verify. Uses <see cref="EphemeralDataProtectionProvider"/>
/// so the key ring lives entirely in memory and does not touch disk —
/// the test runner never writes Data Protection keys to shared storage.
/// </summary>
public sealed class TotpServiceTests
{
    // RFC 6238 appendix B test vector — HMAC-SHA1, secret "12345678901234567890"
    // as raw bytes, step = 30s, digits = 6. Example: T = 59s → code 287082.
    private static readonly byte[] _rfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    public void ComputeCode_RfcVector_ProducesExpectedCode(long unixSeconds, string expected)
    {
        long step = unixSeconds / TotpService.StepSeconds;
        Assert.Equal(expected, TotpService.ComputeCode(_rfcSecret, step));
    }

    [Fact]
    public void Base32_RoundTrip_PreservesBytes()
    {
        byte[] input = Encoding.ASCII.GetBytes("HaliInstitutionSecret");
        string encoded = TotpService.EncodeBase32(input);
        byte[] decoded = TotpService.DecodeBase32(encoded);
        Assert.Equal(input, decoded);
    }

    [Fact]
    public void VerifyCode_CurrentWindowCode_IsAccepted()
    {
        // Generate an enrollment + decrypt its secret with the same
        // ephemeral provider, then compute the "right now" code using
        // the same RFC 6238 primitives the service uses internally.
        // Accepting that code end-to-end exercises the
        // Unprotect → DecodeBase32 → ComputeCode → FixedTimeEquals path.
        var provider = new EphemeralDataProtectionProvider();
        var service = new TotpService(provider, Options.Create(new InstitutionAuthOptions()));

        TotpEnrollment enrollment = service.GenerateEnrollment("user@example.com");
        string base32 = provider
            .CreateProtector("hali-institution-totp-secrets")
            .Unprotect(enrollment.EncryptedSecret);
        byte[] raw = TotpService.DecodeBase32(base32);
        long step = TotpService.GetCurrentStep(DateTimeOffset.UtcNow);
        string currentCode = TotpService.ComputeCode(raw, step);

        Assert.True(service.VerifyCode(enrollment.EncryptedSecret, currentCode));
    }

    [Theory]
    [InlineData("000000")]
    [InlineData("abcdef")]
    [InlineData("")]
    [InlineData("12345")]
    public void VerifyCode_JunkInput_IsRejected(string junk)
    {
        var provider = new EphemeralDataProtectionProvider();
        var service = new TotpService(provider, Options.Create(new InstitutionAuthOptions()));
        TotpEnrollment enrollment = service.GenerateEnrollment("user@example.com");

        Assert.False(service.VerifyCode(enrollment.EncryptedSecret, junk));
    }

    [Fact]
    public void GenerateEnrollment_ReturnsOtpAuthUriAndEncryptedSecret()
    {
        var service = CreateService();
        TotpEnrollment enrollment = service.GenerateEnrollment("alice@example.com");

        Assert.StartsWith("otpauth://totp/", enrollment.OtpAuthUri);
        Assert.Contains("secret=", enrollment.OtpAuthUri);
        Assert.Contains("issuer=", enrollment.OtpAuthUri);
        Assert.False(string.IsNullOrEmpty(enrollment.EncryptedSecret));
        Assert.DoesNotContain(enrollment.EncryptedSecret, enrollment.OtpAuthUri);
    }

    [Fact]
    public void GenerateRecoveryCodes_ProducesUniquePlaintextsAndHashes()
    {
        var service = CreateService();
        var codes = service.GenerateRecoveryCodes(10);

        Assert.Equal(10, codes.Count);
        var plaintexts = new System.Collections.Generic.HashSet<string>();
        var hashes = new System.Collections.Generic.HashSet<string>();
        foreach (var pair in codes)
        {
            Assert.False(string.IsNullOrEmpty(pair.Plaintext));
            Assert.Equal(10, pair.Plaintext.Length);
            Assert.False(string.IsNullOrEmpty(pair.Hash));
            Assert.Equal(64, pair.Hash.Length); // SHA-256 hex
            Assert.True(plaintexts.Add(pair.Plaintext), "plaintexts must be unique");
            Assert.True(hashes.Add(pair.Hash), "hashes must be unique");
        }
    }

    [Fact]
    public void HashRecoveryCode_IsDeterministic()
    {
        var service = CreateService();
        Assert.Equal(service.HashRecoveryCode("ABC12345"), service.HashRecoveryCode("ABC12345"));
        Assert.NotEqual(service.HashRecoveryCode("ABC12345"), service.HashRecoveryCode("xyz99999"));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static TotpService CreateService()
    {
        IOptions<InstitutionAuthOptions> options = Options.Create(new InstitutionAuthOptions());
        return new TotpService(new EphemeralDataProtectionProvider(), options);
    }
}
