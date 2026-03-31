## Version: 1.0
## Last updated: 2026-03-30

# Session 03 — Signal Intake (Phases 4 + 5 + 6)
# Prerequisite: Session 02 complete and committed.

## Context
Auth is complete. JWT middleware is wired. All auth endpoints pass tests.

## Your task this session
Build Phases 4, 5, and 6. Do not begin Phase 7 (Clustering).

### Phase 4 — Signal ingestion
- SignalEvent persistence: write to signal_events table
- Idempotency: check idem:signal-submit:{idempotencyKey} in Redis before insert
- Rate limiting: rl:signal-submit:{deviceHash} in Redis
- Outbox: write to outbox_events on every signal_event insert

### Phase 5 — CSI-NLP integration
Read nlp_extraction_prompt.md fully before implementing.

- INlpExtractionService interface
- Anthropic Claude API call with the prompt template from nlp_extraction_prompt.md
- Inject canonical taxonomy into [APPLICATION SHOULD INJECT CANONICAL TAXONOMY HERE]
- Parse structured JSON response
- Map NLP field names to API DTOs per Field Name Mapping table in nlp_extraction_prompt.md:
  subcategory → subcategorySlug, condition_level → conditionSlug, etc.
- Backend validation rules (§ Backend Validation Rules in nlp_extraction_prompt.md):
  - reject malformed JSON
  - reject unknown categories
  - apply confidence threshold logic
- POST /v1/signals/preview endpoint (calls NLP, returns SignalPreviewResponse)
- POST /v1/signals/submit endpoint (persists confirmed signal)

### Phase 6 — Geocoding integration
- IGeocodingService interface
- Nominatim/OpenStreetMap implementation
- Reverse geocoding: lat/lng → location_label fields
- Cache results in Redis (key pattern: geocode:{lat}:{lng})
- Wire into signal submission pipeline

### Tests required
- NLP extraction happy path and malformed response rejection
- Taxonomy validation (invalid category rejected, valid accepted)
- Geocoding cache hit/miss
- Signal submit idempotency (same key twice = one row)

## Done when
- POST /v1/signals/preview returns structured candidates
- POST /v1/signals/submit persists signal_event with all NLP fields
- Geocoding wraps NLP location and enriches location_label
- Output: SESSION_03_COMPLETE

---

## Universal session rules
Read session_prompts/_universal_rules.md before writing any code.
These rules apply to every session: checkpoint commits, amnesia recovery,
prompt versioning, and context window management.

---
