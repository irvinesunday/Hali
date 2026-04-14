using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Errors;
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

    private const int MaxLocationLabelLength = 400;

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
            throw new DependencyException("dependency.nlp_unavailable", "NLP extraction service is currently unavailable.");
        }
        if (!AllowedCategories.Contains(result.Category))
        {
            throw new ValidationException("NLP returned an unrecognised category.", code: "validation.invalid_category");
        }
        bool requiresLocationFallback = LocationFallbackPolicy.RequiresFallback(result.Location);
        // C11: normalize the NLP-emitted locationSource through the
        // LocationSource allowlist before exposing it on the wire. The
        // OpenAPI `LocationSource` enum is tight, so an unexpected NLP
        // value would break the contract for any client reading the
        // preview response. Unknown values default to "nlp" — the NLP
        // extraction is the only valid source for a preview response.
        string previewLocationSource = LocationSource.IsValid(result.Location.LocationSource)
            ? result.Location.LocationSource
            : LocationSource.Nlp;
        return new SignalPreviewResponseDto(
            result.Category,
            result.Subcategory,
            result.ConditionLevel,
            result.ConditionConfidence,
            new SignalLocationDto(
                result.Location.AreaName,
                result.Location.RoadName,
                result.Location.JunctionName,
                result.Location.LandmarkName,
                result.Location.FacilityName,
                result.Location.LocationLabel,
                result.Location.LocationPrecisionType,
                result.Location.LocationConfidence,
                previewLocationSource),
            result.TemporalHint?.Type,
            result.Summary,
            result.ShouldSuggestJoin,
            requiresLocationFallback);
    }

    public async Task<SignalSubmitResponseDto> SubmitAsync(SignalSubmitRequestDto request, Guid? accountId, Guid? deviceId, CancellationToken ct = default(CancellationToken))
    {
        var sw = Stopwatch.StartNew();
        _logger?.LogInformation("{EventName} category={Category}",
            ObservabilityEvents.SignalSubmitStarted, ObservabilityEvents.SanitizeForLog(request.Category));

        try
        {
            string idemKey = "idem:signal-submit:" + request.IdempotencyKey;
            if (await _repo.IdempotencyKeyExistsAsync(idemKey, ct))
            {
                throw new ConflictException("signal.duplicate", "Signal already submitted with this idempotency key.");
            }
            if (!(await _repo.IsRateLimitAllowedAsync(request.DeviceHash, ct)))
            {
                throw new RateLimitException("integrity.rate_limited", "Too many signals submitted. Please try again later.");
            }
            if (!AllowedCategories.Contains(request.Category))
            {
                throw new ValidationException("Invalid signal category.", code: "validation.invalid_category");
            }
            if (!Enum.TryParse<CivicCategory>(request.Category, ignoreCase: true, out var category))
            {
                throw new ValidationException("Invalid signal category.", code: "validation.invalid_category");
            }
            if (!request.Latitude.HasValue || !request.Longitude.HasValue)
            {
                throw new ValidationException("latitude and longitude are required.", code: "validation.missing_coordinates");
            }
            double lat = request.Latitude.Value;
            double lng = request.Longitude.Value;
            if (lat < -90 || lat > 90 || lng < -180 || lng > 180)
            {
                throw new ValidationException("Latitude must be between -90 and 90, longitude between -180 and 180.", code: "validation.invalid_coordinates");
            }

            string spatialCellId;
            try
            {
                spatialCellId = _h3.LatLngToCell(lat, lng, H3Resolution);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning("{EventName}", ObservabilityEvents.SignalSpatialFailed);
                throw new DependencyException("dependency.spatial_derivation_failed", "Unable to derive spatial cell from provided coordinates.", innerException: ex);
            }

            if (string.IsNullOrEmpty(spatialCellId))
            {
                _logger?.LogWarning("{EventName}", ObservabilityEvents.SignalSpatialFailed);
                throw new DependencyException("dependency.spatial_derivation_failed", "Unable to derive spatial cell from provided coordinates.");
            }

            _logger?.LogInformation("{EventName} spatialCellId={SpatialCellId}",
                ObservabilityEvents.SignalSpatialDerived, spatialCellId);

            LocalitySummary? locality = await _localityLookup.FindByPointAsync(lat, lng, ct);
            if (locality is null)
            {
                _logger?.LogWarning("{EventName}", ObservabilityEvents.SignalLocalityFailed);
                throw new ValidationException("The provided coordinates do not fall within a known locality.", code: "validation.locality_unresolved");
            }

            _logger?.LogInformation("{EventName} localityId={LocalityId}",
                ObservabilityEvents.SignalLocalityResolved, locality.Id);

            if (request.LocationLabel is not null && request.LocationLabel.Length > MaxLocationLabelLength)
            {
                throw new ValidationException(
                    $"Location label must not exceed {MaxLocationLabelLength} characters.",
                    code: "validation.location_label_too_long");
            }

            // C11: canonical LocationSource allowlist. Unknown values would
            // otherwise land in the DB's varchar(20) column unvalidated and
            // quietly degrade downstream analytics/source-attribution.
            if (!LocationSource.IsValid(request.LocationSource))
            {
                throw new ValidationException(
                    "locationSource must be one of: nlp, user_edit, place_search, map_pin.",
                    code: "validation.invalid_location_source");
            }

            // C11 + C11.1: when the user selected a place from the fallback
            // picker OR dropped a pin on the map, the label produced by the
            // picker / reverse-geocode is authoritative and must be present.
            // We deliberately do not require a label for the NLP / user_edit
            // paths (the existing flow tolerates a null label; the cluster
            // falls back to structural fields for its display).
            bool authoritativeLabelSource =
                string.Equals(request.LocationSource, LocationSource.PlaceSearch, StringComparison.Ordinal)
                || string.Equals(request.LocationSource, LocationSource.MapPin, StringComparison.Ordinal);
            if (authoritativeLabelSource && string.IsNullOrWhiteSpace(request.LocationLabel))
            {
                throw new ValidationException(
                    $"locationLabel is required when locationSource is {request.LocationSource}.",
                    code: "validation.location_label_required");
            }

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
                CivisPrecheck = "{}",
                // Transient — not persisted to signal_events but carried
                // in-memory to the clustering service so the cluster can
                // store a denormalized copy of the display label.
                LocationLabelText = request.LocationLabel
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            _logger?.LogWarning("{EventName} reason={Reason} durationMs={DurationMs}",
                ObservabilityEvents.SignalSubmitFailed,
                ObservabilityEvents.SanitizeForLog(ex.GetType().Name),
                sw.ElapsedMilliseconds);
            throw;
        }
    }
}
