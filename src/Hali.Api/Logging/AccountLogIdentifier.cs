using System;
using System.Security.Cryptography;
using System.Text;

namespace Hali.Api.Logging;

/// <summary>
/// Produces a stable, non-reversible token for an account identifier so it
/// can appear in operational logs without exposing the raw account UUID.
/// CodeQL flags raw user identifiers in log entries as clear-text storage
/// of sensitive information (rule cs/cleartext-storage-of-sensitive-information).
/// </summary>
public static class AccountLogIdentifier
{
    public static string Hash(Guid accountId)
    {
        var bytes = SHA256.HashData(accountId.ToByteArray());
        // 12 hex chars (48 bits) is plenty for log correlation while
        // preventing recovery of the original account id.
        var sb = new StringBuilder(12);
        for (int i = 0; i < 6; i++) sb.Append(bytes[i].ToString("x2"));
        return sb.ToString();
    }

    public static string? Hash(Guid? accountId)
        => accountId.HasValue ? Hash(accountId.Value) : null;
}
