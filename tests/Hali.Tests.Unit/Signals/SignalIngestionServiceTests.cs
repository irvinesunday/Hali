using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Errors;
using Hali.Application.Signals;
using Hali.Contracts.Signals;
using Hali.Domain.Entities.Signals;
using Hali.Domain.Enums;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Hali.Tests.Unit.Signals;

public class SignalIngestionServiceTests
{
    private readonly INlpExtractionService _nlp = Substitute.For<INlpExtractionService>();

    private readonly ISignalRepository _repo = Substitute.For<ISignalRepository>();

    private readonly IClusteringService _clustering = Substitute.For<IClusteringService>();

    private readonly IH3CellService _h3 = Substitute.For<IH3CellService>();

    private readonly ILocalityLookupRepository _localityLookup = Substitute.For<ILocalityLookupRepository>();

    private SignalIngestionService CreateService()
    {
        return new SignalIngestionService(_nlp, _repo, _clustering, _h3, _localityLookup);
    }

    private static NlpExtractionResultDto MakeNlpResult(string category = "roads")
    {
        return new NlpExtractionResultDto(category, "potholes", "difficult", 0.85, new NlpLocationDto("Nairobi West", "Lusaka Road", null, null, null, "Potholes on Lusaka Road, Nairobi West", "road", 0.8, "nlp"), new NlpTemporalHintDto("temporary", 0.7), "Potholes on Lusaka Road.", ShouldSuggestJoin: true, null);
    }

    private static SignalSubmitRequestDto MakeSubmitRequest(string idempKey = "key-abc")
    {
        return new SignalSubmitRequestDto(idempKey, "device-hash-1", "Big potholes on Lusaka Road", "roads", "potholes", "difficult", 0.85, -1.3, 36.8, "Potholes on Lusaka Road", "road", 0.8, "nlp", "temporary", "Potholes on Lusaka Road.", "en");
    }

    private static readonly Guid DefaultLocalityId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private void SetupDefaultH3()
    {
        _h3.LatLngToCell(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>()).Returns("892a1008003ffff");
    }

    private void SetupDefaultLocality()
    {
        _localityLookup.FindByPointAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(new LocalitySummary(DefaultLocalityId, "Nairobi West", "Nairobi", "Nairobi"));
    }

    private static readonly Guid DefaultClusterId = Guid.Parse("cccccccc-dddd-eeee-ffff-000000000000");

    private void SetupDefaultRepo()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _repo.PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var s = ci.ArgAt<SignalEvent>(0);
            return new SignalEvent
            {
                Id = s.Id,
                CreatedAt = s.CreatedAt,
                OccurredAt = s.OccurredAt,
                Category = s.Category,
                SpatialCellId = s.SpatialCellId,
                LocalityId = s.LocalityId
            };
        });
        _clustering.RouteSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ClusterRoutingResult(DefaultClusterId, WasCreated: true, WasJoined: false, "unconfirmed", DefaultLocalityId));
    }

    [Fact]
    public async Task PreviewAsync_HappyPath_ReturnsPreviewDto()
    {
        _repo.BuildTaxonomyBlockAsync(Arg.Any<CancellationToken>()).Returns("roads: potholes");
        _nlp.ExtractAsync(Arg.Any<NlpExtractionRequest>(), Arg.Any<CancellationToken>()).Returns(MakeNlpResult());
        SignalIngestionService svc = CreateService();
        SignalPreviewResponseDto result = await svc.PreviewAsync(new SignalPreviewRequestDto("Big potholes on Lusaka Road", null, null, null, null, "Nairobi", "KE"));
        Assert.Equal("roads", result.Category);
        Assert.Equal("potholes", result.SubcategorySlug);
        Assert.Equal("difficult", result.ConditionSlug);
        Assert.True(result.ShouldSuggestJoin);
    }

    // --- C11: requiresLocationFallback server-authoritative flag ---

    [Fact]
    public async Task PreviewAsync_HighConfidenceWithLabel_DoesNotRequireFallback()
    {
        _repo.BuildTaxonomyBlockAsync(Arg.Any<CancellationToken>()).Returns("roads: potholes");
        _nlp.ExtractAsync(Arg.Any<NlpExtractionRequest>(), Arg.Any<CancellationToken>()).Returns(MakeNlpResult());
        SignalIngestionService svc = CreateService();

        SignalPreviewResponseDto result = await svc.PreviewAsync(
            new SignalPreviewRequestDto("Big potholes on Lusaka Road", null, null, null, null, "Nairobi", "KE"));

        Assert.False(result.RequiresLocationFallback);
    }

    [Fact]
    public async Task PreviewAsync_LowConfidence_RequiresFallback()
    {
        _repo.BuildTaxonomyBlockAsync(Arg.Any<CancellationToken>()).Returns("roads: potholes");
        NlpExtractionResultDto lowConf = new NlpExtractionResultDto(
            "roads", "potholes", "difficult", 0.85,
            new NlpLocationDto("Nairobi West", null, null, null, null, "somewhere in Nairobi West", "area", 0.3, "nlp"),
            new NlpTemporalHintDto("temporary", 0.7), "Potholes.", ShouldSuggestJoin: false, null);
        _nlp.ExtractAsync(Arg.Any<NlpExtractionRequest>(), Arg.Any<CancellationToken>()).Returns(lowConf);
        SignalIngestionService svc = CreateService();

        SignalPreviewResponseDto result = await svc.PreviewAsync(
            new SignalPreviewRequestDto("Potholes somewhere", null, null, null, null, "Nairobi", "KE"));

        Assert.True(result.RequiresLocationFallback);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PreviewAsync_HighConfidenceButBlankLabel_RequiresFallback(string? label)
    {
        _repo.BuildTaxonomyBlockAsync(Arg.Any<CancellationToken>()).Returns("roads: potholes");
        NlpExtractionResultDto blankLabel = new NlpExtractionResultDto(
            "roads", "potholes", "difficult", 0.85,
            new NlpLocationDto(null, null, null, null, null, label, null, 0.9, "nlp"),
            new NlpTemporalHintDto("temporary", 0.7), "Potholes.", ShouldSuggestJoin: false, null);
        _nlp.ExtractAsync(Arg.Any<NlpExtractionRequest>(), Arg.Any<CancellationToken>()).Returns(blankLabel);
        SignalIngestionService svc = CreateService();

        SignalPreviewResponseDto result = await svc.PreviewAsync(
            new SignalPreviewRequestDto("Potholes", null, null, null, null, "Nairobi", "KE"));

        Assert.True(result.RequiresLocationFallback);
    }

    [Fact]
    public async Task PreviewAsync_NlpReturnsNull_ThrowsExtractionFailed()
    {
        _repo.BuildTaxonomyBlockAsync(Arg.Any<CancellationToken>()).Returns("roads: potholes");
        _nlp.ExtractAsync(Arg.Any<NlpExtractionRequest>(), Arg.Any<CancellationToken>()).Returns((NlpExtractionResultDto)null!);
        SignalIngestionService svc = CreateService();
        var ex = await Assert.ThrowsAsync<DependencyException>(() => svc.PreviewAsync(new SignalPreviewRequestDto("test", null, null, null, null, null, null)));
        Assert.Equal("dependency.nlp_unavailable", ex.Code);
    }

    [Fact]
    public async Task PreviewAsync_NlpReturnsUnknownCategory_ThrowsInvalidCategory()
    {
        _repo.BuildTaxonomyBlockAsync(Arg.Any<CancellationToken>()).Returns("roads: potholes");
        _nlp.ExtractAsync(Arg.Any<NlpExtractionRequest>(), Arg.Any<CancellationToken>()).Returns(MakeNlpResult("aliens"));
        SignalIngestionService svc = CreateService();
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.PreviewAsync(new SignalPreviewRequestDto("test", null, null, null, null, null, null)));
        Assert.Equal("validation.invalid_category", ex.Code);
    }

    [Fact]
    public async Task SubmitAsync_DuplicateIdempotencyKey_ThrowsDuplicate()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        SignalIngestionService svc = CreateService();
        var ex = await Assert.ThrowsAsync<ConflictException>(() => svc.SubmitAsync(MakeSubmitRequest(), null, null));
        Assert.Equal("signal.duplicate", ex.Code);
        await _repo.DidNotReceive().PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitAsync_SameKeyTwice_SecondCallThrowsDuplicate()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false, true);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        SetupDefaultH3();
        SetupDefaultLocality();
        _repo.PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>()).Returns(new SignalEvent
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            OccurredAt = DateTime.UtcNow,
            Category = CivicCategory.Roads,
            SpatialCellId = "892a1008003ffff",
            LocalityId = DefaultLocalityId
        });
        _clustering.RouteSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ClusterRoutingResult(DefaultClusterId, WasCreated: true, WasJoined: false, "unconfirmed", DefaultLocalityId));
        SignalIngestionService svc = CreateService();
        Assert.NotNull(await svc.SubmitAsync(MakeSubmitRequest("key-dup"), null, null));
        var ex = await Assert.ThrowsAsync<ConflictException>(() => svc.SubmitAsync(MakeSubmitRequest("key-dup"), null, null));
        Assert.Equal("signal.duplicate", ex.Code);
    }

    [Fact]
    public async Task SubmitAsync_RateLimited_ThrowsRateLimited()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        SignalIngestionService svc = CreateService();
        var ex = await Assert.ThrowsAsync<RateLimitException>(() => svc.SubmitAsync(MakeSubmitRequest(), null, null));
        Assert.Equal("integrity.rate_limited", ex.Code);
    }

    [Fact]
    public async Task SubmitAsync_InvalidCategory_ThrowsInvalidCategory()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        SignalSubmitRequestDto request = MakeSubmitRequest() with { Category = "unknown_category" };
        SignalIngestionService svc = CreateService();
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.SubmitAsync(request, null, null));
        Assert.Equal("validation.invalid_category", ex.Code);
    }

    [Fact]
    public async Task SubmitAsync_ValidRequest_PersistsAndReturnsId()
    {
        SetupDefaultRepo();
        SetupDefaultH3();
        SetupDefaultLocality();
        SignalIngestionService svc = CreateService();
        var result = await svc.SubmitAsync(MakeSubmitRequest(), null, null);
        Assert.NotEqual(Guid.Empty, result.SignalEventId);
        await _repo.Received(1).PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>());
        await _repo.Received(1).SetIdempotencyKeyAsync(Arg.Is((string k) => k.StartsWith("idem:signal-submit:")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("roads", true)]
    [InlineData("water", true)]
    [InlineData("electricity", true)]
    [InlineData("environment", true)]
    [InlineData("ROADS", true)]
    [InlineData("garbage", false)]
    [InlineData("", false)]
    [InlineData("road", false)]
    public async Task SubmitAsync_CategoryValidation(string category, bool shouldSucceed)
    {
        SetupDefaultRepo();
        SetupDefaultH3();
        SetupDefaultLocality();
        SignalSubmitRequestDto request = MakeSubmitRequest() with { Category = category };
        SignalIngestionService svc = CreateService();
        if (shouldSucceed)
        {
            Assert.NotNull(await svc.SubmitAsync(request, null, null));
            return;
        }
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.SubmitAsync(request, null, null));
        Assert.Equal("validation.invalid_category", ex.Code);
    }

    // --- Phase A1: Spatial cell derivation tests ---

    [Fact]
    public async Task SubmitAsync_MissingCoordinates_ThrowsMissingCoordinates()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        SignalSubmitRequestDto request = MakeSubmitRequest() with { Latitude = null, Longitude = null };
        SignalIngestionService svc = CreateService();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.SubmitAsync(request, null, null));
        Assert.Equal("validation.missing_coordinates", ex.Code);
        await _repo.DidNotReceive().PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task SubmitAsync_ValidCoordinates_DerivesSpatialCellId()
    {
        SetupDefaultRepo();
        _h3.LatLngToCell(-1.3, 36.8, 9).Returns("892a1008003ffff");
        SetupDefaultLocality();
        SignalIngestionService svc = CreateService();
        await svc.SubmitAsync(MakeSubmitRequest(), null, null);

        await _repo.Received(1).PersistSignalAsync(
            Arg.Is<SignalEvent>(s => s.SpatialCellId == "892a1008003ffff"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitAsync_ValidSubmission_ReachesClusteringWithNonNullSpatialCellId()
    {
        SetupDefaultRepo();
        SetupDefaultH3();
        SetupDefaultLocality();
        SignalIngestionService svc = CreateService();
        await svc.SubmitAsync(MakeSubmitRequest(), null, null);

        await _clustering.Received(1).RouteSignalAsync(
            Arg.Is<SignalEvent>(s => s.SpatialCellId != null),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-91.0, 36.8)]
    [InlineData(91.0, 36.8)]
    [InlineData(-1.3, -181.0)]
    [InlineData(-1.3, 181.0)]
    public async Task SubmitAsync_InvalidCoordinates_ThrowsInvalidCoordinates(double lat, double lng)
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        SignalSubmitRequestDto request = MakeSubmitRequest() with { Latitude = lat, Longitude = lng };
        SignalIngestionService svc = CreateService();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.SubmitAsync(request, null, null));
        Assert.Equal("validation.invalid_coordinates", ex.Code);
        await _repo.DidNotReceive().PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitAsync_SpatialDerivationFails_ThrowsAndDoesNotPersist()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _h3.LatLngToCell(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>()).Throws(new ArgumentException("Invalid coordinates"));
        SignalIngestionService svc = CreateService();

        var ex = await Assert.ThrowsAsync<DependencyException>(() => svc.SubmitAsync(MakeSubmitRequest(), null, null));
        Assert.Equal("dependency.spatial_derivation_failed", ex.Code);
        await _repo.DidNotReceive().PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitAsync_SpatialDerivationReturnsEmpty_ThrowsAndDoesNotPersist()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _h3.LatLngToCell(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>()).Returns("");
        SignalIngestionService svc = CreateService();

        var ex = await Assert.ThrowsAsync<DependencyException>(() => svc.SubmitAsync(MakeSubmitRequest(), null, null));
        Assert.Equal("dependency.spatial_derivation_failed", ex.Code);
        await _repo.DidNotReceive().PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>());
    }

    // --- Phase A2: Locality resolution tests ---

    [Fact]
    public async Task SubmitAsync_ValidCoordinates_ResolvesLocalityAndPersistsLocalityId()
    {
        SetupDefaultRepo();
        SetupDefaultH3();
        SetupDefaultLocality();
        SignalIngestionService svc = CreateService();

        await svc.SubmitAsync(MakeSubmitRequest(), null, null);

        await _repo.Received(1).PersistSignalAsync(
            Arg.Is<SignalEvent>(s => s.LocalityId == DefaultLocalityId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitAsync_LocalityUnresolved_ThrowsAndDoesNotPersist()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        SetupDefaultH3();
        _localityLookup.FindByPointAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns((LocalitySummary?)null);
        SignalIngestionService svc = CreateService();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.SubmitAsync(MakeSubmitRequest(), null, null));
        Assert.Equal("validation.locality_unresolved", ex.Code);
        await _repo.DidNotReceive().PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitAsync_ValidSubmissionWithLocality_ReachesClusteringWithLocalityId()
    {
        SetupDefaultRepo();
        SetupDefaultH3();
        SetupDefaultLocality();
        SignalIngestionService svc = CreateService();

        await svc.SubmitAsync(MakeSubmitRequest(), null, null);

        await _clustering.Received(1).RouteSignalAsync(
            Arg.Is<SignalEvent>(s => s.LocalityId == DefaultLocalityId),
            Arg.Any<CancellationToken>());
    }

    // --- Phase A3: Cluster routing result in submit response ---

    [Fact]
    public async Task SubmitAsync_NewClusterCreated_ResponseIncludesClusterIdAndIsNewCluster()
    {
        SetupDefaultRepo();
        SetupDefaultH3();
        SetupDefaultLocality();
        var clusterId = Guid.NewGuid();
        _clustering.RouteSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ClusterRoutingResult(clusterId, WasCreated: true, WasJoined: false, "unconfirmed", DefaultLocalityId));
        SignalIngestionService svc = CreateService();

        var result = await svc.SubmitAsync(MakeSubmitRequest(), null, null);

        Assert.Equal(clusterId, result.ClusterId);
        Assert.True(result.IsNewCluster);
        Assert.Equal("unconfirmed", result.ClusterState);
        Assert.Equal(DefaultLocalityId, result.LocalityId);
    }

    [Fact]
    public async Task SubmitAsync_JoinedExistingCluster_ResponseIncludesClusterIdAndIsNotNew()
    {
        SetupDefaultRepo();
        SetupDefaultH3();
        SetupDefaultLocality();
        var clusterId = Guid.NewGuid();
        _clustering.RouteSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ClusterRoutingResult(clusterId, WasCreated: false, WasJoined: true, "active", DefaultLocalityId));
        SignalIngestionService svc = CreateService();

        var result = await svc.SubmitAsync(MakeSubmitRequest(), null, null);

        Assert.Equal(clusterId, result.ClusterId);
        Assert.False(result.IsNewCluster);
        Assert.Equal("active", result.ClusterState);
    }

    [Fact]
    public async Task SubmitAsync_ResponseAlwaysIncludesSignalEventIdAndCreatedAt()
    {
        SetupDefaultRepo();
        SetupDefaultH3();
        SetupDefaultLocality();
        SignalIngestionService svc = CreateService();

        var result = await svc.SubmitAsync(MakeSubmitRequest(), null, null);

        Assert.NotEqual(Guid.Empty, result.SignalEventId);
        Assert.NotEqual(Guid.Empty, result.ClusterId);
        Assert.NotEqual(default, result.CreatedAt);
    }

    // --- B9: Location label length validation ---

    [Fact]
    public async Task SubmitAsync_LocationLabelExceedsMaxLength_ThrowsValidation()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        SetupDefaultH3();
        SetupDefaultLocality();
        string oversized = new string('x', 401);
        SignalSubmitRequestDto request = MakeSubmitRequest() with { LocationLabel = oversized };
        SignalIngestionService svc = CreateService();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.SubmitAsync(request, null, null));
        Assert.Equal("validation.location_label_too_long", ex.Code);
        await _repo.DidNotReceive().PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitAsync_LocationLabelAtMaxLength_Succeeds()
    {
        SetupDefaultRepo();
        SetupDefaultH3();
        SetupDefaultLocality();
        string atLimit = new string('x', 400);
        SignalSubmitRequestDto request = MakeSubmitRequest() with { LocationLabel = atLimit };
        SignalIngestionService svc = CreateService();

        var result = await svc.SubmitAsync(request, null, null);

        Assert.NotEqual(Guid.Empty, result.SignalEventId);
    }

    [Fact]
    public async Task SubmitAsync_NullLocationLabel_Succeeds()
    {
        SetupDefaultRepo();
        SetupDefaultH3();
        SetupDefaultLocality();
        SignalSubmitRequestDto request = MakeSubmitRequest() with { LocationLabel = null };
        SignalIngestionService svc = CreateService();

        var result = await svc.SubmitAsync(request, null, null);

        Assert.NotEqual(Guid.Empty, result.SignalEventId);
    }

    // --- C11: location source allowlist + place_search label requirement ---

    [Theory]
    [InlineData("nlp")]
    [InlineData("user_edit")]
    [InlineData("place_search")]
    public async Task SubmitAsync_ValidLocationSource_Succeeds(string source)
    {
        SetupDefaultRepo();
        SetupDefaultH3();
        SetupDefaultLocality();
        // place_search requires a label; the default request already has one.
        SignalSubmitRequestDto request = MakeSubmitRequest() with { LocationSource = source };
        SignalIngestionService svc = CreateService();

        var result = await svc.SubmitAsync(request, null, null);

        Assert.NotEqual(Guid.Empty, result.SignalEventId);
    }

    [Theory]
    [InlineData("map_pin")]        // deferred to C11.1 — not yet in the allowlist
    [InlineData("NLP")]            // wire allowlist is case-sensitive
    [InlineData("place-search")]   // dash vs underscore
    [InlineData("")]
    [InlineData("   ")]
    public async Task SubmitAsync_UnknownLocationSource_ThrowsInvalidLocationSource(string source)
    {
        SetupDefaultRepo();
        SetupDefaultH3();
        SetupDefaultLocality();
        SignalSubmitRequestDto request = MakeSubmitRequest() with { LocationSource = source };
        SignalIngestionService svc = CreateService();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.SubmitAsync(request, null, null));
        Assert.Equal("validation.invalid_location_source", ex.Code);
        await _repo.DidNotReceive().PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SubmitAsync_PlaceSearchWithoutLabel_ThrowsLocationLabelRequired(string? label)
    {
        SetupDefaultRepo();
        SetupDefaultH3();
        SetupDefaultLocality();
        SignalSubmitRequestDto request = MakeSubmitRequest() with
        {
            LocationSource = "place_search",
            LocationLabel = label,
        };
        SignalIngestionService svc = CreateService();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.SubmitAsync(request, null, null));
        Assert.Equal("validation.location_label_required", ex.Code);
        await _repo.DidNotReceive().PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>());
    }
}
