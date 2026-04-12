using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Tests.Integration.Infrastructure;
using Xunit;

namespace Hali.Tests.Integration.Signals;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class SignalIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public SignalIntegrationTests(HaliWebApplicationFactory factory) : base(factory) { }

    // -----------------------------------------------------------------------
    // Preview
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Preview_ValidText_ReturnsStructuredCandidate()
    {
        var response = await Client.PostAsJsonAsync("/v1/signals/preview", new
        {
            freeText = "There is a large pothole on the main road blocking traffic",
            userLatitude = -1.2921,
            userLongitude = 36.8219,
            locale = "en",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("roads", body.GetProperty("category").GetString());
        Assert.Equal("potholes", body.GetProperty("subcategorySlug").GetString());
        Assert.True(body.TryGetProperty("location", out var loc));
        Assert.False(string.IsNullOrWhiteSpace(loc.GetProperty("locationLabel").GetString()));
    }

    // -----------------------------------------------------------------------
    // Submit
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Submit_ValidSignal_CreatesSignalEvent()
    {
        var (_, _, jwt) = await SeedVerifiedAccountAsync();
        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.PostAsJsonAsync("/v1/signals/submit", new
        {
            idempotencyKey = Guid.NewGuid().ToString(),
            deviceHash = TestConstants.TestDeviceHash,
            freeText = "Pothole on Test Road",
            category = "roads",
            subcategorySlug = "potholes",
            conditionSlug = "pothole_severe",
            conditionConfidence = 0.92,
            latitude = -1.2921,
            longitude = 36.8219,
            locationLabel = "Test Road, Test Area",
            locationPrecisionType = "road",
            locationConfidence = 0.85,
            locationSource = "nlp",
            temporalType = "temporary",
            neutralSummary = "Severe pothole on Test Road.",
            sourceLanguage = "en",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.True(body.TryGetProperty("signalEventId", out var idProp));
        Assert.NotEqual(Guid.Empty, Guid.Parse(idProp.GetString()!));
    }

    [Fact]
    public async Task Submit_SameIdempotencyKey_Returns200OnlyOnce()
    {
        var (_, _, jwt) = await SeedVerifiedAccountAsync(
            phone: "+254711000020", deviceHash: "idem-device-01");
        using var authClient = CreateAuthenticatedClient(jwt);

        var idempotencyKey = Guid.NewGuid().ToString();
        var payload = new
        {
            idempotencyKey = idempotencyKey,
            deviceHash = "idem-device-01",
            freeText = "Water outage in my area",
            category = "water",
            subcategorySlug = "outage",
            conditionConfidence = 0.80,
            latitude = -1.2921,
            longitude = 36.8219,
            locationConfidence = 0.70,
            locationSource = "user",
        };

        var first = await authClient.PostAsJsonAsync("/v1/signals/submit", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await authClient.PostAsJsonAsync("/v1/signals/submit", payload);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}
