using System;
using System.Collections.Generic;

namespace Hali.Application.Auth;

/// <summary>
/// RFC 6238 TOTP generator/validator plus recovery-code utilities. Secret
/// plaintext never crosses this boundary — enrollers store the
/// <see cref="TotpEnrollment.EncryptedSecret"/> verbatim, and verifiers
/// pass the encrypted blob back in on every call so decryption stays
/// inside the service.
/// </summary>
public interface ITotpService
{
    /// <summary>
    /// Generates a fresh base-32 TOTP secret and encrypts it with the
    /// configured Data Protection purpose. Also emits the <c>otpauth://</c>
    /// URI and the recovery-code plaintexts — both of which are shown to
    /// the user exactly once at enrollment.
    /// </summary>
    TotpEnrollment GenerateEnrollment(string accountEmail);

    /// <summary>
    /// Returns true when <paramref name="code"/> matches the 6-digit TOTP
    /// for the current 30-second window (or the immediately adjacent
    /// window, to tolerate ≤30s of clock skew).
    /// </summary>
    bool VerifyCode(string encryptedSecret, string code);

    /// <summary>
    /// Produces <paramref name="count"/> 10-character recovery codes along
    /// with their SHA-256 hashes. The plaintexts are returned for one-time
    /// display; only the hashes should be stored.
    /// </summary>
    IReadOnlyList<RecoveryCodePair> GenerateRecoveryCodes(int count);

    /// <summary>Hashes a recovery code for lookup against stored rows.</summary>
    string HashRecoveryCode(string plaintext);
}

/// <summary>Output of <see cref="ITotpService.GenerateEnrollment"/>.</summary>
public sealed record TotpEnrollment(
    string EncryptedSecret,
    string OtpAuthUri,
    IReadOnlyList<RecoveryCodePair> RecoveryCodes);

/// <summary>Recovery code pair — plaintext (shown to user) + stored hash.</summary>
public sealed record RecoveryCodePair(string Plaintext, string Hash);
