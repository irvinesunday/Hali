
# Hali Queue and Topic Definitions

## Internal domain events
- `signal.submitted`
- `signal.previewed`
- `cluster.created`
- `cluster.activated`
- `cluster.updated`
- `cluster.possible_restoration`
- `cluster.resolved`
- `official_post.published`
- `notification.requested`
- `integrity.review_required`

## Recommended queues
1. `ingestion-normalize`
   - NLP extraction post-processing
   - location normalization
   - category/condition reconciliation

2. `cluster-linking`
   - join new signal events to an existing cluster
   - create candidate cluster when no viable match exists
   - recompute cluster headline/summary

3. `civis-scoring`
   - recompute WRAB, SDS, MACF
   - evaluate activation and suppression logic
   - emit decision reason codes

4. `restoration-evaluation`
   - process no-longer-affected votes
   - move clusters into possible restoration
   - resolve when threshold satisfied

5. `official-post-projection`
   - materialize advisory views for localities/corridors
   - expiry scheduling and state refresh

6. `notifications`
   - push notification fan-out
   - dedupe and quiet-hours policy
   - restoration prompt scheduling

7. `metrics-projection`
   - LCR, activation funnel, response funnel
   - audit-ready event projections

## Delivery rules
- Use outbox pattern from PostgreSQL to queue bridge.
- Every event payload must carry `eventId`, `occurredAt`, `aggregateId`, `aggregateType`, and `schemaVersion`.
- Jobs must be idempotent; use dedupe keys in Redis and/or durable table markers.
- Dead-letter queue per worker category with replay tooling.
