using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Hali.Contracts.Signals;
using Xunit;

namespace Hali.Tests.Unit.Security;

/// <summary>
/// Verifies that <see cref="SignalPreviewRequestDto"/> DataAnnotations
/// are present and effective. The DTO annotations are the API boundary
/// authority — the controller-level guard is idempotent but secondary.
/// </summary>
public class SignalPreviewDtoValidationTests
{
    private static IList<ValidationResult> Validate(SignalPreviewRequestDto dto)
    {
        List<ValidationResult> results = new List<ValidationResult>();
        ValidationContext context = new ValidationContext(dto);
        Validator.TryValidateObject(dto, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void SignalPreviewRequestDto_FreeText_Required_FailsModelStateWhenNull()
    {
        // null is not representable as a record positional parameter directly,
        // so we use the workaround of a null-forgiving cast.
        SignalPreviewRequestDto dto = new SignalPreviewRequestDto(
            FreeText: null!,
            UserLatitude: null,
            UserLongitude: null,
            SelectedWard: null,
            Locale: null,
            KnownCity: null,
            CountryCode: null);

        IList<ValidationResult> errors = Validate(dto);

        Assert.Contains(errors, e =>
            e.MemberNames != null &&
            System.Linq.Enumerable.Contains(e.MemberNames, nameof(SignalPreviewRequestDto.FreeText)));
    }

    [Fact]
    public void SignalPreviewRequestDto_FreeText_MaxLength_FailsModelStateAboveLimit()
    {
        // 151 characters — one over the 150-character composer limit
        // from docs/arch/hali_citizen_mvp_canonical_spec.md §10.3.
        string overLimit = new string('a', 151);
        SignalPreviewRequestDto dto = new SignalPreviewRequestDto(
            FreeText: overLimit,
            UserLatitude: null,
            UserLongitude: null,
            SelectedWard: null,
            Locale: null,
            KnownCity: null,
            CountryCode: null);

        IList<ValidationResult> errors = Validate(dto);

        Assert.Contains(errors, e =>
            e.MemberNames != null &&
            System.Linq.Enumerable.Contains(e.MemberNames, nameof(SignalPreviewRequestDto.FreeText)));
    }
}
