
# Hali Redis and Job Configuration

## Redis uses
- rate limiting keyed by device, account, and IP
- idempotency key cache for signal submit and participation APIs
- short-lived OTP attempt counters
- worker queue backend
- advisory cache fragments and hot locality snapshots

## Suggested key patterns
- `rl:signal-submit:{deviceHash}`
- `rl:otp:{destination}`
- `rl:auth-refresh:{deviceHash}`
- `idem:signal-submit:{idempotencyKey}`
- `idem:participation:{idempotencyKey}`
- `queue:notifications`
- `queue:civis-scoring`

## Background jobs
- `DecayActiveClustersJob` every 5 minutes
- `EvaluatePossibleRestorationJob` every 5 minutes
- `ExpireOfficialPostsJob` every 10 minutes
- `RefreshLocalitySnapshotCacheJob` every 2 minutes for hot areas
- `AggregateTemporalPatternsJob` hourly
- `ProjectMetricsJob` every 15 minutes

## Retry policy
- exponential backoff with jitter
- max attempts 8 for idempotent jobs
- max attempts 3 for non-idempotent side-effect jobs
- poison messages go to dead-letter queue and raise alert
