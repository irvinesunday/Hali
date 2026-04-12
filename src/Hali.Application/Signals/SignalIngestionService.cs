using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Errors;
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

    private static readonly HashSet<string> AllowedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "roads", "transport", "electricity", "water", "environment", "safety", "governance", "infrastructure" };

    private static readonly HashSet<string> AllowedTemporalTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "temporary", "continuous", "recurring", "scheduled", "episodic_unknown" };

    public SignalIngestionService(INlpExtractionService nlp, IGeocodingService geocoding, ISignalRepository repo, IClusteringService clustering)
    {
        _nlp = nlp;
        _geocoding = geocoding;
        _repo = repo;
        _clustering = clustering;
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
        return new SignalPreviewResponseDto(result.Category, result.Subcategory, result.ConditionLevel, result.ConditionConfidence, new SignalLocationDto(result.Location.AreaName, result.Location.RoadName, result.Location.JunctionName, result.Location.LandmarkName, result.Location.FacilityName, result.Location.LocationLabel, result.Location.LocationPrecisionType, result.Location.LocationConfidence, result.Location.LocationSource), result.TemporalHint?.Type, result.Summary, result.ShouldSuggestJoin);
    }

    public async Task<SignalSubmitResponseDto> SubmitAsync(SignalSubmitRequestDto request, Guid? accountId, Guid? deviceId, CancellationToken ct = default(CancellationToken))
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
        string geocodedLabel = null;
        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            GeocodingResult geo = await _geocoding.ReverseGeocodeAsync(request.Latitude.Value, request.Longitude.Value, ct);
            if (geo?.DisplayName != null)
            {
                geocodedLabel = geo.DisplayName;
            }
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
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            LocationConfidence = ((request.LocationConfidence > 0.0) ? new decimal?((decimal)request.LocationConfidence) : ((decimal?)null)),
            LocationSource = ((geocodedLabel != null) ? "geocode" : request.LocationSource),
            ConditionConfidence = ((request.ConditionConfidence > 0.0) ? new decimal?((decimal)request.ConditionConfidence) : ((decimal?)null)),
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            SourceLanguage = request.SourceLanguage,
            SourceChannel = "app",
            SpatialCellId = request.SpatialCellId,
            CivisPrecheck = "{}"
        };
        SignalEvent saved = await _repo.PersistSignalAsync(signal, ct);
        await _repo.SetIdempotencyKeyAsync(idemKey, TimeSpan.FromHours(24), ct);
        await _clustering.RouteSignalAsync(saved, ct);
        return new SignalSubmitResponseDto(saved.Id, saved.CreatedAt);
    }
}
