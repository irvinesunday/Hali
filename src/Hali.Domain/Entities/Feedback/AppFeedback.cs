using System;

namespace Hali.Domain.Entities.Feedback;

/// <summary>
/// Anonymous in-app feedback. AccountId is nullable — accepted without authentication.
/// No FK constraints on ClusterId or AccountId — feedback survives deletions.
/// </summary>
public class AppFeedback
{
	public Guid Id { get; set; }

	/// <summary>positive | neutral | negative</summary>
	public string Rating { get; set; } = null!;

	public string? Text { get; set; }

	/// <summary>Screen name at time of submission.</summary>
	public string? Screen { get; set; }

	public Guid? ClusterId { get; set; }

	public Guid? AccountId { get; set; }

	public string? AppVersion { get; set; }

	public string? Platform { get; set; }

	public Guid? SessionId { get; set; }

	public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
}
