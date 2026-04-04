using System;

namespace Hali.Contracts.Signals;

public record SignalSubmitResponseDto(Guid SignalEventId, DateTime CreatedAt);
