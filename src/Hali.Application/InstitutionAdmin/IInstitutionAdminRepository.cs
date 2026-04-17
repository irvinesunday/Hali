using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Advisories;
using Hali.Domain.Entities.Auth;

namespace Hali.Application.InstitutionAdmin;

/// <summary>
/// Storage boundary for the Phase 2 institution-admin routes (#196).
/// Every method is scoped server-side by the acting admin's institution
/// id. Cross-institution reads and writes return no rows / no effect —
/// callers surface that to the wire as 404 to avoid cross-institution
/// existence probes.
/// </summary>
public interface IInstitutionAdminRepository
{
    /// <summary>
    /// Lists accounts registered to the given institution. Includes
    /// institution_user + institution_admin rows; excludes citizens and
    /// Hali-ops admins.
    /// </summary>
    Task<IReadOnlyList<Account>> ListUsersAsync(Guid institutionId, CancellationToken ct);

    /// <summary>
    /// Resolves a single user — returns null if the user exists but
    /// belongs to a different institution (callers surface 404).
    /// </summary>
    Task<Account?> FindUserInInstitutionAsync(Guid institutionId, Guid userId, CancellationToken ct);

    /// <summary>
    /// Counts currently-flagged institution_admin accounts in the
    /// institution. Used to enforce the last-admin-demote guard.
    /// </summary>
    Task<int> CountAdminsAsync(Guid institutionId, CancellationToken ct);

    /// <summary>
    /// Updates the <c>is_institution_admin</c> flag on an account that
    /// belongs to the caller's institution. Returns false when the
    /// account is not found in this institution.
    /// </summary>
    Task<bool> SetAccountAdminFlagAsync(
        Guid institutionId, Guid userId, bool isInstitutionAdmin, DateTime updatedAt, CancellationToken ct);

    /// <summary>
    /// Lists the institution's jurisdictions — used by GET /scope so
    /// the acting admin can see exactly what they can manage.
    /// </summary>
    Task<IReadOnlyList<InstitutionJurisdiction>> ListJurisdictionsAsync(
        Guid institutionId, CancellationToken ct);

    /// <summary>
    /// Reads a single institution row (name + type) so GET /scope can
    /// render the display context alongside the jurisdictions.
    /// </summary>
    Task<Institution?> FindInstitutionAsync(Guid institutionId, CancellationToken ct);

    /// <summary>
    /// Returns a display label for a locality id (e.g. ward name) —
    /// used to enrich the GET /scope response without forcing callers
    /// to join to the signals / localities module themselves.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetLocalityDisplayNamesAsync(
        IEnumerable<Guid> localityIds, CancellationToken ct);
}
