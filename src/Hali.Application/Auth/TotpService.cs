using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Hali.Application.Auth;

/// <summary>
/// RFC 6238 TOTP implementation using HMAC-SHA1 + a 30-second step.
/// Secrets are encrypted at rest via ASP.NET Data Protection with the
/// purpose string <c>"hali-institution-totp-secrets"</c> so the key ring
/// rotates independently of other Data Protection consumers.
/// </summary>
public sealed class TotpService : ITotpService
{
    public const int StepSeconds = 30;
    public const int Digits = 6;
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const string DataProtectionPurpose = "hali-institution-totp-secrets";

    private readonly IDataProtector _protector;
    private readonly InstitutionAuthOptions _opts;

    public TotpService(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<InstitutionAuthOptions> options)
    {
        _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
        _opts = options.Value;
    }

    public TotpEnrollment GenerateEnrollment(string accountEmail)
    {
        // 20 bytes = 160 bits — the RFC 4226 recommended minimum strength
        // for HMAC-SHA1 based TOTP.
        byte[] rawSecret = RandomNumberGenerator.GetBytes(20);
        string base32Secret = EncodeBase32(rawSecret);
        string encryptedSecret = _protector.Protect(base32Secret);

        // otpauth://totp/{issuer}:{account}?secret={b32}&issuer={issuer}
        // Authenticator apps parse this URI directly (commonly rendered as
        // a QR code); issuer + account are URL-encoded so reserved chars
        // round-trip cleanly.
        string label = $"{Uri.EscapeDataString(_opts.TotpIssuer)}:{Uri.EscapeDataString(accountEmail)}";
        string otpAuthUri =
            $"otpauth://totp/{label}?secret={base32Secret}&issuer={Uri.EscapeDataString(_opts.TotpIssuer)}&digits={Digits}&period={StepSeconds}";

        IReadOnlyList<RecoveryCodePair> codes = GenerateRecoveryCodes(_opts.RecoveryCodeCount);

        return new TotpEnrollment(encryptedSecret, otpAuthUri, codes);
    }

    public bool VerifyCode(string encryptedSecret, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != Digits)
        {
            return false;
        }
        if (!int.TryParse(code, out _))
        {
            return false;
        }

        string base32Secret;
        try
        {
            base32Secret = _protector.Unprotect(encryptedSecret);
        }
        catch (CryptographicException)
        {
            // Key ring has rotated past the encryption timestamp or the
            // ciphertext is corrupt — treat as "cannot verify" rather than
            // throwing a 500 at the caller.
            return false;
        }

        byte[] secret = DecodeBase32(base32Secret);
        long currentStep = GetCurrentStep(DateTimeOffset.UtcNow);

        // ±1 step tolerance for clock drift between the server and the
        // authenticator app. Matches RFC 6238 §5.2 guidance.
        for (int offset = -1; offset <= 1; offset++)
        {
            string candidate = ComputeCode(secret, currentStep + offset);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(candidate),
                    Encoding.ASCII.GetBytes(code)))
            {
                return true;
            }
        }
        return false;
    }

    public IReadOnlyList<RecoveryCodePair> GenerateRecoveryCodes(int count)
    {
        var codes = new List<RecoveryCodePair>(count);
        for (int i = 0; i < count; i++)
        {
            // 10 alphanumeric chars = ~52 bits of entropy — comparable to a
            // strong passphrase and easy for a user to transcribe.
            string plaintext = GenerateAlphanumeric(10);
            codes.Add(new RecoveryCodePair(plaintext, HashRecoveryCode(plaintext)));
        }
        return codes;
    }

    public string HashRecoveryCode(string plaintext)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // ------------------------------------------------------------------
    // Internals
    // ------------------------------------------------------------------

    public static long GetCurrentStep(DateTimeOffset now)
        => now.ToUnixTimeSeconds() / StepSeconds;

    public static string ComputeCode(byte[] secret, long step)
    {
        // RFC 6238 §4.2: counter is the 8-byte big-endian step number.
        byte[] counter = new byte[8];
        for (int i = 7; i >= 0; i--)
        {
            counter[i] = (byte)(step & 0xFF);
            step >>= 8;
        }

        byte[] mac = HMACSHA1.HashData(secret, counter);

        // Dynamic truncation per RFC 4226 §5.3.
        int offset = mac[mac.Length - 1] & 0x0F;
        int binCode = ((mac[offset] & 0x7F) << 24)
                    | ((mac[offset + 1] & 0xFF) << 16)
                    | ((mac[offset + 2] & 0xFF) << 8)
                    | (mac[offset + 3] & 0xFF);

        int otp = binCode % (int)Math.Pow(10, Digits);
        return otp.ToString("D" + Digits, System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string EncodeBase32(byte[] data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bitsLeft = 0;
        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                int index = (buffer >> (bitsLeft - 5)) & 0x1F;
                sb.Append(Base32Alphabet[index]);
                bitsLeft -= 5;
            }
        }
        if (bitsLeft > 0)
        {
            int index = (buffer << (5 - bitsLeft)) & 0x1F;
            sb.Append(Base32Alphabet[index]);
        }
        return sb.ToString();
    }

    public static byte[] DecodeBase32(string text)
    {
        int byteCount = text.Length * 5 / 8;
        byte[] output = new byte[byteCount];
        int buffer = 0, bitsLeft = 0, writeIndex = 0;
        foreach (char c in text)
        {
            int value = Base32Alphabet.IndexOf(char.ToUpperInvariant(c));
            if (value < 0) continue; // skip padding / separator characters
            buffer = (buffer << 5) | value;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output[writeIndex++] = (byte)((buffer >> (bitsLeft - 8)) & 0xFF);
                bitsLeft -= 8;
            }
        }
        return output;
    }

    private static string GenerateAlphanumeric(int length)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // omit ambiguous chars
        var sb = new StringBuilder(length);
        byte[] buf = RandomNumberGenerator.GetBytes(length);
        for (int i = 0; i < length; i++)
        {
            sb.Append(alphabet[buf[i] % alphabet.Length]);
        }
        return sb.ToString();
    }
}
