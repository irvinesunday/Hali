using System;
using System.Collections.Generic;

namespace Hali.Contracts.Signals;

/// <summary>
/// Canonical wire values for <see cref="SignalLocationDto.LocationSource"/>
/// and <see cref="SignalSubmitRequestDto.LocationSource"/>.
///
/// These values are persisted on <c>signal_events.location_source</c>
/// (varchar 20) and must stay in sync with the OpenAPI LocationSource
/// enum in <c>02_openapi.yaml</c>.
///
/// Phase 1 supports:
///   <list type="bullet">
///     <item><c>nlp</c> — derived from CSI-NLP extraction, unchanged by the user.</item>
///     <item><c>user_edit</c> — user accepted the extraction and/or edited the
///           human-readable label in the composer. Coordinates remain the device-GPS
///           or NLP-suggested coordinates.</item>
///     <item><c>place_search</c> — user picked a Nominatim-backed place search
///           result from the low-confidence fallback picker. Both the label
///           and the coordinates are authoritative from the picker.</item>
///     <item><c>map_pin</c> — user refined location by dropping / dragging a
///           marker on the map fallback surface (C11.1). Coordinates come
///           from the marker position; the label is reverse-geocoded on
///           marker release via <c>/v1/places/reverse</c>.</item>
///   </list>
/// </summary>
public static class LocationSource
{
    public const string Nlp = "nlp";
    public const string UserEdit = "user_edit";
    public const string PlaceSearch = "place_search";
    public const string MapPin = "map_pin";

    /// <summary>
    /// Case-sensitive allowlist. Submit requests whose <c>LocationSource</c>
    /// is not in this set are rejected with
    /// <c>validation.invalid_location_source</c>.
    /// </summary>
    private static readonly HashSet<string> AllSet = new HashSet<string>(StringComparer.Ordinal)
    {
        Nlp,
        UserEdit,
        PlaceSearch,
        MapPin,
    };

    public static IReadOnlyCollection<string> All => AllSet;

    public static bool IsValid(string? value)
        => value is not null && AllSet.Contains(value);
}
