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
using Xunit;

namespace Hali.Tests.Unit.Signals;

public class SignalIngestionServiceTests
{
	private readonly INlpExtractionService _nlp = Substitute.For<INlpExtractionService>(Array.Empty<object>());

	private readonly IGeocodingService _geocoding = Substitute.For<IGeocodingService>(Array.Empty<object>());

	private readonly ISignalRepository _repo = Substitute.For<ISignalRepository>(Array.Empty<object>());

	private readonly IClusteringService _clustering = Substitute.For<IClusteringService>(Array.Empty<object>());

	private SignalIngestionService CreateService()
	{
		return new SignalIngestionService(_nlp, _geocoding, _repo, _clustering);
	}

	private static NlpExtractionResultDto MakeNlpResult(string category = "roads")
	{
		return new NlpExtractionResultDto(category, "potholes", "difficult", 0.85, new NlpLocationDto("Nairobi West", "Lusaka Road", null, null, null, "Potholes on Lusaka Road, Nairobi West", "road", 0.8, "nlp"), new NlpTemporalHintDto("temporary", 0.7), "Potholes on Lusaka Road.", ShouldSuggestJoin: true, null);
	}

	private static SignalSubmitRequestDto MakeSubmitRequest(string idempKey = "key-abc")
	{
		return new SignalSubmitRequestDto(idempKey, "device-hash-1", "Big potholes on Lusaka Road", "roads", "potholes", "difficult", 0.85, -1.3, 36.8, "Potholes on Lusaka Road", "road", 0.8, "nlp", "temporary", "Potholes on Lusaka Road.", "en", null);
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

	[Fact]
	public async Task PreviewAsync_NlpReturnsNull_ThrowsExtractionFailed()
	{
		_repo.BuildTaxonomyBlockAsync(Arg.Any<CancellationToken>()).Returns("roads: potholes");
		_nlp.ExtractAsync(Arg.Any<NlpExtractionRequest>(), Arg.Any<CancellationToken>()).Returns(null, Array.Empty<NlpExtractionResultDto>());
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
		_geocoding.ReverseGeocodeAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>()).Returns(null, Array.Empty<GeocodingResult>());
		SubstituteExtensions.Returns(returnThis: new SignalEvent
		{
			Id = Guid.NewGuid(),
			CreatedAt = DateTime.UtcNow,
			OccurredAt = DateTime.UtcNow,
			Category = CivicCategory.Roads
		}, value: _repo.PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>()), returnThese: Array.Empty<SignalEvent>());
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
		SignalSubmitRequestDto request = MakeSubmitRequest()with
		{
			Category = "unknown_category"
		};
		SignalIngestionService svc = CreateService();
		var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.SubmitAsync(request, null, null));
		Assert.Equal("validation.invalid_category", ex.Code);
	}

	[Fact]
	public async Task SubmitAsync_ValidRequest_PersistsAndReturnsId()
	{
		_repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
		_repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
		_geocoding.ReverseGeocodeAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>()).Returns(new GeocodingResult("Lusaka Road, Nairobi", "Lusaka Road", "Nairobi West", "Nairobi", "Kenya"));
		Guid expectedId = Guid.NewGuid();
		DateTime now = DateTime.UtcNow;
		_repo.PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>()).Returns(new SignalEvent
		{
			Id = expectedId,
			CreatedAt = now,
			OccurredAt = now,
			Category = CivicCategory.Roads
		});
		SignalIngestionService svc = CreateService();
		Assert.Equal(expectedId, (await svc.SubmitAsync(MakeSubmitRequest(), null, null)).SignalEventId);
		await _repo.Received(1).PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>());
		await _repo.Received(1).SetIdempotencyKeyAsync(Arg.Is((string k) => k.StartsWith("idem:signal-submit:")), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
	}

	[Theory]
	[InlineData(new object[] { "roads", true })]
	[InlineData(new object[] { "water", true })]
	[InlineData(new object[] { "electricity", true })]
	[InlineData(new object[] { "environment", true })]
	[InlineData(new object[] { "ROADS", true })]
	[InlineData(new object[] { "garbage", false })]
	[InlineData(new object[] { "", false })]
	[InlineData(new object[] { "road", false })]
	public async Task SubmitAsync_CategoryValidation(string category, bool shouldSucceed)
	{
		_repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
		_repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
		_geocoding.ReverseGeocodeAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>()).Returns(null, Array.Empty<GeocodingResult>());
		Guid expectedId = Guid.NewGuid();
		DateTime now = DateTime.UtcNow;
		_repo.PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>()).Returns(new SignalEvent
		{
			Id = expectedId,
			CreatedAt = now,
			OccurredAt = now,
			Category = CivicCategory.Roads
		});
		SignalSubmitRequestDto request = MakeSubmitRequest()with
		{
			Category = category
		};
		SignalIngestionService svc = CreateService();
		if (shouldSucceed)
		{
			Assert.NotNull(await svc.SubmitAsync(request, null, null));
			return;
		}
		var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.SubmitAsync(request, null, null));
		Assert.Equal("validation.invalid_category", ex.Code);
	}
}
