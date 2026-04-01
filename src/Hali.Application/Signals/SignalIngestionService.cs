using Hali.Application.Clusters;
using Hali.Contracts.Signals;
using Hali.Domain.Entities.Signals;
using Hali.Domain.Enums;

namespace Hali.Application.Signals;

public class SignalIngestionService : ISignalIngestionService
{
    private readonly INlpExtractionService _nlp;
    private readonly IGeocodingService _geocoding;
    private readonly ISignalRepository _repo;
    private readonly IClusteringService _clustering;

    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "roads", "transport", "electricity", "water", "environment", "safety", "governance", "infrastructure"
    };

    private static readonly HashSet<string> AllowedTemporalTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "temporary", "continuous", "recurring", "scheduled", "episodic_unknown"
    };

    public SignalIngestionService(
        INlpExtractionService nlp,
        IGeocodingService geocoding,
        ISignalRepository repo,
        IClusteringService clustering)
    {
        _nlp = nlp;
        _geocoding = geocoding;
        _repo = repo;
        _clustering = clustering;
    }

    public async Task<SignalPreviewResponseDto> PreviewAsync(SignalPreviewRequestDto request, CancellationToken ct = default)
    {
        var taxonomyBlock = await _repo.BuildTaxonomyBlockAsync(ct);

        var nlpRequest = new NlpExtractionRequest(
            FreeText: request.FreeText,
            UserLatitude: request.UserLatitude,
            UserLongitude: request.UserLongitude,
            SelectedWard: request.SelectedWard,
            Locale: request.Locale,
            KnownCity: request.KnownCity,
            CountryCode: request.CountryCode,
            CurrentTimeUtc: DateTime.UtcNow.ToString("o"),
            TaxonomyBlock: taxonomyBlock
        );

        var result = await _nlp.ExtractAsync(nlpRequest, ct);

        if (result is null)
            throw new InvalidOperationException("NLP_EXTRACTION_FAILED");

        if (!AllowedCategories.Contains(result.Category))
            throw new InvalidOperationException("NLP_INVALID_CATEGORY");

        return new SignalPreviewResponseDto(
            Category: result.Category,
            SubcategorySlug: result.Subcategory,
            ConditionSlug: result.ConditionLevel,
            ConditionConfidence: result.ConditionConfidence,
            Location: new SignalLocationDto(
                AreaName: result.Location.AreaName,
                RoadName: result.Location.RoadName,
                JunctionName: result.Location.JunctionName,
                LandmarkName: result.Location.LandmarkName,
                FacilityName: result.Location.FacilityName,
                LocationLabel: result.Location.LocationLabel,
                LocationPrecisionType: result.Location.LocationPrecisionType,
                LocationConfidence: result.Location.LocationConfidence,
                LocationSource: result.Location.LocationSource
            ),
            TemporalType: result.TemporalHint?.Type,
            NeutralSummary: result.Summary,
            ShouldSuggestJoin: result.ShouldSuggestJoin
        );
    }

    public async Task<SignalSubmitResponseDto> SubmitAsync(
        SignalSubmitRequestDto request,
        Guid? accountId,
        Guid? deviceId,
        CancellationToken ct = default)
    {
        // Idempotency check
        var idemKey = $"idem:signal-submit:{request.IdempotencyKey}";
        if (await _repo.IdempotencyKeyExistsAsync(idemKey, ct))
            throw new InvalidOperationException("SIGNAL_DUPLICATE");

        // Rate limiting
        var allowed = await _repo.IsRateLimitAllowedAsync(request.DeviceHash, ct);
        if (!allowed)
            throw new InvalidOperationException("SIGNAL_RATE_LIMITED");

        // Validate category
        if (!AllowedCategories.Contains(request.Category))
            throw new InvalidOperationException("SIGNAL_INVALID_CATEGORY");

        if (!Enum.TryParse<CivicCategory>(request.Category, ignoreCase: true, out var category))
            throw new InvalidOperationException("SIGNAL_INVALID_CATEGORY");

        // Geocoding enrichment
        string? geocodedLabel = null;
        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            var geo = await _geocoding.ReverseGeocodeAsync(request.Latitude.Value, request.Longitude.Value, ct);
            if (geo?.DisplayName != null)
                geocodedLabel = geo.DisplayName;
        }

        var temporalType = AllowedTemporalTypes.Contains(request.TemporalType ?? "")
            ? request.TemporalType
            : "episodic_unknown";

        var signal = new SignalEvent
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            DeviceId = deviceId,
            Category = category,
            SubcategorySlug = request.SubcategorySlug,
            ConditionSlug = request.ConditionSlug,
            FreeText = request.FreeText,
            NeutralSummary = request.NeutralSummary,
            TemporalType = temporalType,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            LocationConfidence = request.LocationConfidence > 0 ? (decimal)request.LocationConfidence : null,
            LocationSource = geocodedLabel is not null ? "geocode" : request.LocationSource,
            ConditionConfidence = request.ConditionConfidence > 0 ? (decimal)request.ConditionConfidence : null,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            SourceLanguage = request.SourceLanguage,
            SourceChannel = "app",
            SpatialCellId = request.SpatialCellId,
            CivisPrecheck = "{}"
        };

        var saved = await _repo.PersistSignalAsync(signal, ct);

        // Mark idempotency key (set after persist to avoid locking a key for a failed write)
        await _repo.SetIdempotencyKeyAsync(idemKey, TimeSpan.FromHours(24), ct);

        // Route to cluster (Phase 7)
        await _clustering.RouteSignalAsync(saved, ct);

        return new SignalSubmitResponseDto(saved.Id, saved.CreatedAt);
    }
}
