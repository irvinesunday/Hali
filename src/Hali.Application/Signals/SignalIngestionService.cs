using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Observability;
using Hali.Contracts.Signals;
using Hali.Domain.Entities.Signals;
using Hali.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Hali.Application.Signals;

public class SignalIngestionService : ISignalIngestionService
{
    private readonly INlpExtractionService _nlp;

    private readonly ISignalRepository _repo;

    private readonly IClusteringService _clustering;

    private readonly IH3CellService _h3;

    private readonly ILocalityLookupRepository _localityLookup;

    private readonly ILogger<SignalIngestionService>? _logger;

    private const int H3Resolution = 9;

    private static readonly HashSet<string> AllowedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "roads", "transport", "electricity", "water", "environment", "safety", "governance", "infrastructure" };

    private static readonly HashSet<string> AllowedTemporalTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "temporary", "continuous", "recurring", "scheduled", "episodic_unknown" };

    public SignalIngestionService(INlpExtractionService nlp, ISignalRepository repo, IClusteringService clustering, IH3CellService h3, ILocalityLookupRepository localityLookup, ILogger<SignalIngestionService>? logger = null)
    {
        _nlp = nlp;
        _repo = repo;
        _clustering = clustering;
        _h3 = h3;
        _localityLookup = localityLookup;
        _logger = logger;
    }

    public async Task<SignalPreviewResponseDto> PreviewAsync(SignalPreviewRequestDto request, CancellationToken ct = default(CancellationToken))
    {
        NlpExtractionRequest nlpRequest = new NlpExtractionRequest(TaxonomyBlock: await _repo.BuildTaxonomyBlockAsync(ct), FreeText: request.FreeText, UserLatitude: request.UserLatitude, UserLongitude: request.UserLongitude, SelectedWard: request.SelectedWard, Locale: request.Locale, KnownCity: request.KnownCity, CountryCode: request.CountryCode, CurrentTimeUtc: DateTime.UtcNow.ToString("o"));
        NlpExtractionResultDto result = await _nlp.ExtractAsync(nlpRequest, ct);
        if ((object)result == null)
        {
            throw new InvalidOperationException("NLP_EXTRACTION_FAILED");
        }
        if (!AllowedCategories.Contains(result.Category))
        {
            throw new InvalidOperationException("NLP_INVALID_CATEGORY");
        }
        return new SignalPreviewResponseDto(result.Category, result.Subcategory, result.ConditionLevel, result.ConditionConfidence, new SignalLocationDto(result.Location.AreaName, result.Location.RoadName, result.Location.JunctionName, result.Location.LandmarkName, result.Location.FacilityName, result.Location.LocationLabel, result.Location.LocationPrecisionType, result.Location.LocationConfidence, result.Location.LocationSource), result.TemporalHint?.Type, result.Summary, result.ShouldSuggestJoin);
    }

    public async Task<SignalSubmitResponseDto> SubmitAsync(SignalSubmitRequestDto request, Guid? accountId, Guid? deviceId, CancellationToken ct = default(CancellationToken))
    {
        var sw = Stopwatch.StartNew();
        _logger?.LogInformation("{EventName} category={Category}",
            ObservabilityEvents.SignalSubmitStarted, request.Category);

        try
        {
            string idemKey = "idem:signal-submit:" + request.IdempotencyKey;
            if (await _repo.IdempotencyKeyExistsAsync(idemKey, ct))
            {
                throw new InvalidOperationException("SIGNAL_DUPLICATE");
            }
            if (!(await _repo.IsRateLimitAllowedAsync(request.DeviceHash, ct)))
            {
                throw new InvalidOperationException("SIGNAL_RATE_LIMITED");
            }
            if (!AllowedCategories.Contains(request.Category))
            {
                throw new InvalidOperationException("SIGNAL_INVALID_CATEGORY");
            }
            if (!Enum.TryParse<CivicCategory>(request.Category, ignoreCase: true, out var category))
            {
                throw new InvalidOperationException("SIGNAL_INVALID_CATEGORY");
            }
            if (!request.Latitude.HasValue || !request.Longitude.HasValue)
            {
                throw new InvalidOperationException("SIGNAL_MISSING_COORDINATES");
            }
            double lat = request.Latitude.Value;
            double lng = request.Longitude.Value;
            if (lat < -90 || lat > 90 || lng < -180 || lng > 180)
            {
                throw new InvalidOperationException("SIGNAL_INVALID_COORDINATES");
            }

            string spatialCellId;
            try
            {
                spatialCellId = _h3.LatLngToCell(lat, lng, H3Resolution);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning("{EventName}", ObservabilityEvents.SignalSpatialFailed);
                throw new InvalidOperationException("SIGNAL_SPATIAL_DERIVATION_FAILED", ex);
            }

            if (string.IsNullOrEmpty(spatialCellId))
            {
                _logger?.LogWarning("{EventName}", ObservabilityEvents.SignalSpatialFailed);
                throw new InvalidOperationException("SIGNAL_SPATIAL_DERIVATION_FAILED");
            }

            _logger?.LogInformation("{EventName} spatialCellId={SpatialCellId}",
                ObservabilityEvents.SignalSpatialDerived, spatialCellId);

            LocalitySummary? locality = await _localityLookup.FindByPointAsync(lat, lng, ct);
            if (locality is null)
            {
                _logger?.LogWarning("{EventName}", ObservabilityEvents.SignalLocalityFailed);
                throw new InvalidOperationException("SIGNAL_LOCALITY_UNRESOLVED");
            }

            _logger?.LogInformation("{EventName} localityId={LocalityId}",
                ObservabilityEvents.SignalLocalityResolved, locality.Id);

            string temporalType = (AllowedTemporalTypes.Contains(request.TemporalType ?? "") ? request.TemporalType : "episodic_unknown");
            SignalEvent signal = new SignalEvent
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
                Latitude = lat,
                Longitude = lng,
                LocationConfidence = ((request.LocationConfidence > 0.0) ? new decimal?((decimal)request.LocationConfidence) : ((decimal?)null)),
                LocationSource = request.LocationSource,
                ConditionConfidence = ((request.ConditionConfidence > 0.0) ? new decimal?((decimal)request.ConditionConfidence) : ((decimal?)null)),
                OccurredAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                SourceLanguage = request.SourceLanguage,
                SourceChannel = "app",
                SpatialCellId = spatialCellId,
                LocalityId = locality.Id,
                CivisPrecheck = "{}"
            };
            SignalEvent saved = await _repo.PersistSignalAsync(signal, ct);
            await _repo.SetIdempotencyKeyAsync(idemKey, TimeSpan.FromHours(24), ct);
            var routing = await _clustering.RouteSignalAsync(saved, ct);

            sw.Stop();
            _logger?.LogInformation(
                "{EventName} signalId={SignalId} clusterId={ClusterId} wasCreated={WasCreated} durationMs={DurationMs}",
                ObservabilityEvents.SignalSubmitCompleted, saved.Id, routing.ClusterId, routing.WasCreated, sw.ElapsedMilliseconds);

            return new SignalSubmitResponseDto(
                saved.Id,
                routing.ClusterId,
                routing.WasCreated,
                routing.ClusterState,
                routing.LocalityId,
                saved.CreatedAt);
        }
        catch (InvalidOperationException ex)
        {
            sw.Stop();
            _logger?.LogWarning("{EventName} reason={Reason} durationMs={DurationMs}",
                ObservabilityEvents.SignalSubmitFailed, ex.Message, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
