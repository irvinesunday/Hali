using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Hali.Tests.Integration.Infrastructure;
using Xunit;

namespace Hali.Tests.Integration.Signals;

/// <summary>
/// Integration tests verifying that the DataAnnotations on
/// <see cref="Hali.Contracts.Signals.SignalPreviewRequestDto.FreeText"/>
/// are enforced at the API boundary (model-state validation, HTTP 400).
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class SignalPreviewValidationTests : IntegrationTestBase
{
    public SignalPreviewValidationTests(HaliWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task SignalPreview_ReturnsValidationError_WhenFreeTextMissing()
    {
        // Post a body that omits freeText entirely so the [Required] annotation
        // on the DTO fires. The model binder treats the missing field as null.
        var response = await Client.PostAsJsonAsync("/v1/signals/preview", new
        {
            userLatitude = -1.2921,
            userLongitude = 36.8219,
            locale = "en",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SignalPreview_ReturnsValidationError_WhenFreeTextExceedsMaxLength()
    {
        // 151 characters — one over the 150-character composer limit from
        // docs/arch/hali_citizen_mvp_canonical_spec.md §10.3.
        string overLimit = new string('a', 151);

        var response = await Client.PostAsJsonAsync("/v1/signals/preview", new
        {
            freeText = overLimit,
            userLatitude = -1.2921,
            userLongitude = 36.8219,
            locale = "en",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
