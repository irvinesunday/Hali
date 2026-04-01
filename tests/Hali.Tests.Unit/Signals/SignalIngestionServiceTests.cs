using Hali.Application.Clusters;
using Hali.Application.Signals;
using Hali.Contracts.Signals;
using Hali.Domain.Entities.Signals;
using Hali.Domain.Enums;
using NSubstitute;

namespace Hali.Tests.Unit.Signals;

public class SignalIngestionServiceTests
{
    private readonly INlpExtractionService _nlp = Substitute.For<INlpExtractionService>();
    private readonly IGeocodingService _geocoding = Substitute.For<IGeocodingService>();
    private readonly ISignalRepository _repo = Substitute.For<ISignalRepository>();
    private readonly IClusteringService _clustering = Substitute.For<IClusteringService>();

    private SignalIngestionService CreateService() => new(_nlp, _geocoding, _repo, _clustering);

    private static NlpExtractionResultDto MakeNlpResult(string category = "roads") =>
        new(
            Category: category,
            Subcategory: "potholes",
            ConditionLevel: "difficult",
            ConditionConfidence: 0.85,
            Location: new NlpLocationDto(
                AreaName: "Nairobi West",
                RoadName: "Lusaka Road",
                JunctionName: null,
                LandmarkName: null,
                FacilityName: null,
                LocationLabel: "Potholes on Lusaka Road, Nairobi West",
                LocationPrecisionType: "road",
                LocationConfidence: 0.80,
                LocationSource: "nlp"
            ),
            TemporalHint: new NlpTemporalHintDto("temporary", 0.7),
            Summary: "Potholes on Lusaka Road.",
            ShouldSuggestJoin: true,
            ReasoningNotes: null
        );

    private static SignalSubmitRequestDto MakeSubmitRequest(string idempKey = "key-abc") =>
        new(
            IdempotencyKey: idempKey,
            DeviceHash: "device-hash-1",
            FreeText: "Big potholes on Lusaka Road",
            Category: "roads",
            SubcategorySlug: "potholes",
            ConditionSlug: "difficult",
            ConditionConfidence: 0.85,
            Latitude: -1.3,
            Longitude: 36.8,
            LocationLabel: "Potholes on Lusaka Road",
            LocationPrecisionType: "road",
            LocationConfidence: 0.80,
            LocationSource: "nlp",
            TemporalType: "temporary",
            NeutralSummary: "Potholes on Lusaka Road.",
            SourceLanguage: "en",
            SpatialCellId: null
        );

    // ── PreviewAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task PreviewAsync_HappyPath_ReturnsPreviewDto()
    {
        _repo.BuildTaxonomyBlockAsync(Arg.Any<CancellationToken>()).Returns("roads: potholes");
        _nlp.ExtractAsync(Arg.Any<NlpExtractionRequest>(), Arg.Any<CancellationToken>()).Returns(MakeNlpResult());

        var svc = CreateService();
        var result = await svc.PreviewAsync(new SignalPreviewRequestDto("Big potholes on Lusaka Road", null, null, null, null, "Nairobi", "KE"));

        Assert.Equal("roads", result.Category);
        Assert.Equal("potholes", result.SubcategorySlug);
        Assert.Equal("difficult", result.ConditionSlug);
        Assert.True(result.ShouldSuggestJoin);
    }

    [Fact]
    public async Task PreviewAsync_NlpReturnsNull_ThrowsExtractionFailed()
    {
        _repo.BuildTaxonomyBlockAsync(Arg.Any<CancellationToken>()).Returns("roads: potholes");
        _nlp.ExtractAsync(Arg.Any<NlpExtractionRequest>(), Arg.Any<CancellationToken>()).Returns((NlpExtractionResultDto?)null);

        var svc = CreateService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.PreviewAsync(new SignalPreviewRequestDto("test", null, null, null, null, null, null)));

        Assert.Equal("NLP_EXTRACTION_FAILED", ex.Message);
    }

    [Fact]
    public async Task PreviewAsync_NlpReturnsUnknownCategory_ThrowsInvalidCategory()
    {
        _repo.BuildTaxonomyBlockAsync(Arg.Any<CancellationToken>()).Returns("roads: potholes");
        _nlp.ExtractAsync(Arg.Any<NlpExtractionRequest>(), Arg.Any<CancellationToken>()).Returns(MakeNlpResult("aliens"));

        var svc = CreateService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.PreviewAsync(new SignalPreviewRequestDto("test", null, null, null, null, null, null)));

        Assert.Equal("NLP_INVALID_CATEGORY", ex.Message);
    }

    // ── SubmitAsync idempotency ───────────────────────────────────────────────

    [Fact]
    public async Task SubmitAsync_DuplicateIdempotencyKey_ThrowsDuplicate()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var svc = CreateService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SubmitAsync(MakeSubmitRequest(), accountId: null, deviceId: null));

        Assert.Equal("SIGNAL_DUPLICATE", ex.Message);
        await _repo.DidNotReceive().PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitAsync_SameKeyTwice_SecondCallThrowsDuplicate()
    {
        // First call: key does not exist → succeeds
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false, true);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _geocoding.ReverseGeocodeAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns((GeocodingResult?)null);

        var savedSignal = new SignalEvent { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, OccurredAt = DateTime.UtcNow, Category = CivicCategory.Roads };
        _repo.PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>()).Returns(savedSignal);

        var svc = CreateService();
        var first = await svc.SubmitAsync(MakeSubmitRequest("key-dup"), accountId: null, deviceId: null);
        Assert.NotNull(first);

        // Second call: key now exists
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SubmitAsync(MakeSubmitRequest("key-dup"), accountId: null, deviceId: null));

        Assert.Equal("SIGNAL_DUPLICATE", ex.Message);
    }

    [Fact]
    public async Task SubmitAsync_RateLimited_ThrowsRateLimited()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var svc = CreateService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SubmitAsync(MakeSubmitRequest(), accountId: null, deviceId: null));

        Assert.Equal("SIGNAL_RATE_LIMITED", ex.Message);
    }

    [Fact]
    public async Task SubmitAsync_InvalidCategory_ThrowsInvalidCategory()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var request = MakeSubmitRequest() with { Category = "unknown_category" };
        var svc = CreateService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SubmitAsync(request, accountId: null, deviceId: null));

        Assert.Equal("SIGNAL_INVALID_CATEGORY", ex.Message);
    }

    [Fact]
    public async Task SubmitAsync_ValidRequest_PersistsAndReturnsId()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _geocoding.ReverseGeocodeAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(new GeocodingResult("Lusaka Road, Nairobi", "Lusaka Road", "Nairobi West", "Nairobi", "Kenya"));

        var expectedId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        _repo.PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>())
            .Returns(new SignalEvent { Id = expectedId, CreatedAt = now, OccurredAt = now, Category = CivicCategory.Roads });

        var svc = CreateService();
        var result = await svc.SubmitAsync(MakeSubmitRequest(), accountId: null, deviceId: null);

        Assert.Equal(expectedId, result.SignalEventId);
        await _repo.Received(1).PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>());
        await _repo.Received(1).SetIdempotencyKeyAsync(
            Arg.Is<string>(k => k.StartsWith("idem:signal-submit:")),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    // ── Taxonomy validation ───────────────────────────────────────────────────

    [Theory]
    [InlineData("roads", true)]
    [InlineData("water", true)]
    [InlineData("electricity", true)]
    [InlineData("environment", true)]
    [InlineData("ROADS", true)]          // case-insensitive
    [InlineData("garbage", false)]
    [InlineData("", false)]
    [InlineData("road", false)]          // partial match must fail
    public async Task SubmitAsync_CategoryValidation(string category, bool shouldSucceed)
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _geocoding.ReverseGeocodeAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns((GeocodingResult?)null);

        var expectedId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        _repo.PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>())
            .Returns(new SignalEvent { Id = expectedId, CreatedAt = now, OccurredAt = now, Category = CivicCategory.Roads });

        var request = MakeSubmitRequest() with { Category = category };
        var svc = CreateService();

        if (shouldSucceed)
        {
            var result = await svc.SubmitAsync(request, null, null);
            Assert.NotNull(result);
        }
        else
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.SubmitAsync(request, null, null));
            Assert.Equal("SIGNAL_INVALID_CATEGORY", ex.Message);
        }
    }
}
