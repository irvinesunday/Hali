using System;
using Hali.Domain.Enums;

namespace Hali.Domain.Entities.Signals;

public class LocationLabel
{
	public Guid Id { get; set; }

	public Guid? LocalityId { get; set; }

	public string? AreaName { get; set; }

	public string? RoadName { get; set; }

	public string? JunctionDescription { get; set; }

	public string? LandmarkName { get; set; }

	public string? FacilityName { get; set; }

	public string LocationLabelText { get; set; } = string.Empty;

	public LocationPrecisionType PrecisionType { get; set; }

	public double? Latitude { get; set; }

	public double? Longitude { get; set; }

	public DateTime CreatedAt { get; set; }
}
