using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Hali.Contracts.InstitutionAdmin;

// Requests -------------------------------------------------------------

public class InviteInstitutionUserRequestDto
{
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Target role for the invited account. Only <c>institution_user</c> is
    /// accepted today; <c>institution_admin</c> elevation returns
    /// <c>institution_admin.elevation_requires_approval</c> pending the
    /// approval-flow work.
    /// </summary>
    [Required]
    [RegularExpression("^(institution_user|institution_admin)$",
        ErrorMessage = "role must be institution_user or institution_admin")]
    public string Role { get; set; } = "institution_user";
}

public class ChangeUserRoleRequestDto
{
    [Required]
    [RegularExpression("^(institution_user|institution_admin)$",
        ErrorMessage = "role must be institution_user or institution_admin")]
    public string Role { get; set; } = "institution_user";
}

// Responses ------------------------------------------------------------

public sealed record InstitutionAdminUserListItemDto(
    Guid Id,
    string? Email,
    string? DisplayName,
    string Role,
    DateTime CreatedAt);

public sealed record InstitutionAdminUserListResponseDto(
    IReadOnlyList<InstitutionAdminUserListItemDto> Items);

public sealed record InstitutionAdminUserDetailResponseDto(
    Guid Id,
    string? Email,
    string? DisplayName,
    string Role,
    string Status,
    bool IsBlocked,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record InviteInstitutionUserResponseDto(
    Guid InviteId,
    DateTime ExpiresAt);

public sealed record InstitutionAdminScopeJurisdictionDto(
    Guid Id,
    Guid? LocalityId,
    string? CorridorName,
    string? DisplayName);

public sealed record InstitutionAdminScopeResponseDto(
    Guid InstitutionId,
    string InstitutionName,
    IReadOnlyList<InstitutionAdminScopeJurisdictionDto> Jurisdictions);
