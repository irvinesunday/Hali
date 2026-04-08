using System;
using System.ComponentModel.DataAnnotations;

namespace Hali.Contracts.Requests;

/// <summary>
/// Request body for POST /v1/feedback.
/// Matches the FeedbackRequest schema in the OpenAPI spec.
/// All fields except Rating are optional.
/// </summary>
public class SubmitFeedbackRequest
{
    /// <summary>Simple 3-point sentiment: positive | neutral | negative</summary>
    [Required]
    [RegularExpression("^(positive|neutral|negative)$",
        ErrorMessage = "Rating must be positive, neutral, or negative.")]
    public string Rating { get; set; } = null!;

    /// <summary>Optional free text, max 300 chars.</summary>
    [MaxLength(300)]
    public string? Text { get; set; }

    /// <summary>Screen the user was on at time of submission.</summary>
    [MaxLength(50)]
    public string? Screen { get; set; }

    /// <summary>Cluster in context if on cluster detail screen.</summary>
    public Guid? ClusterId { get; set; }

    /// <summary>App version string e.g. 1.0.0</summary>
    [MaxLength(20)]
    public string? AppVersion { get; set; }

    /// <summary>ios | android</summary>
    [MaxLength(10)]
    public string? Platform { get; set; }

    /// <summary>Client-generated anonymous session UUID.</summary>
    public Guid? SessionId { get; set; }
}
