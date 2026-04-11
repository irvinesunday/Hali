using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

	private readonly IH3CellService _h3;

	private const int H3Resolution = 9;

	private static readonly HashSet<string> AllowedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "roads", "transport", "electricity", "water", "environment", "safety", "governance", "infrastructure" };

	private static readonly HashSet<string> AllowedTemporalTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "temporary", "continuous", "recurring", "scheduled", "episodic_unknown" };

	public SignalIngestionService(INlpExtractionService nlp, IGeocodingService geocoding, ISignalRepository repo, IClusteringService clustering, IH3CellService h3)
	{
		_nlp = nlp;
		_geocoding = geocoding;
		_repo = repo;
		_clustering = clustering;
		_h3 = h3;
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
		catch (Exception)
		{
			throw new InvalidOperationException("SIGNAL_SPATIAL_DERIVATION_FAILED");
		}

		if (string.IsNullOrEmpty(spatialCellId))
		{
			throw new InvalidOperationException("SIGNAL_SPATIAL_DERIVATION_FAILED");
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
			CivisPrecheck = "{}"
		};
		SignalEvent saved = await _repo.PersistSignalAsync(signal, ct);
		await _repo.SetIdempotencyKeyAsync(idemKey, TimeSpan.FromHours(24), ct);
		await _clustering.RouteSignalAsync(saved, ct);
		return new SignalSubmitResponseDto(saved.Id, saved.CreatedAt);
	}
}
