using System;

namespace Hali.Domain.Entities.Signals;

public class Locality
{
    public Guid Id { get; set; }

    public string CountryCode { get; set; } = string.Empty;

    public string? CountyName { get; set; }

    public string? CityName { get; set; }

    public string WardName { get; set; } = string.Empty;

    public string? WardCode { get; set; }

    public DateTime CreatedAt { get; set; }
}
