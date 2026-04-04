using System;
using Hali.Domain.Enums;

namespace Hali.Domain.Entities.Signals;

public class SignalEvent
{
	public Guid Id { get; set; }

	public Guid? AccountId { get; set; }

	public Guid? DeviceId { get; set; }

	public Guid? LocalityId { get; set; }

	public Guid? LocationLabelId { get; set; }

	public CivicCategory Category { get; set; }

	public string? SubcategorySlug { get; set; }

	public string? ConditionSlug { get; set; }

	public string? FreeText { get; set; }

	public string? NeutralSummary { get; set; }

	public string? TemporalType { get; set; }

	public double? Latitude { get; set; }

	public double? Longitude { get; set; }

	public decimal? LocationConfidence { get; set; }

	public string? LocationSource { get; set; }

	public decimal? ConditionConfidence { get; set; }

	public DateTime OccurredAt { get; set; }

	public DateTime CreatedAt { get; set; }

	public string? SourceLanguage { get; set; }

	public string? SourceChannel { get; set; }

	public string? SpatialCellId { get; set; }

	public string? CivisPrecheck { get; set; }
}
