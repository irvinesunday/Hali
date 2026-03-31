# CSI-NLP Extraction Prompt (MVP)

## Purpose

This document defines the exact MVP extraction contract for Hali’s CSI-NLP layer.

The goal is not free-form AI interpretation.
The goal is bounded, structured extraction of civic signal candidates from user free text.

The backend sends the user’s text and optional context to Anthropic Claude Sonnet and expects strict JSON output.
The backend then validates, normalizes, and maps the output to Hali’s canonical taxonomy.

---

## MVP Decision

- Provider: Anthropic Claude API
- Model family: Claude Sonnet
- Output mode: Structured JSON only
- Temperature: keep low for consistency
- No prose response should be accepted by the backend
- Backend must reject malformed JSON
- Backend must reject categories outside the canonical taxonomy

---

## Canonical Taxonomy Constraint

The model must choose category and subcategory only from the allowed Hali taxonomy.
It must not invent new categories.

### Allowed top-level categories (Phase 1 MVP)
- roads
- transport
- electricity
- water
- environment
- safety
- governance
- infrastructure

### Example subcategories

#### roads
- potholes
- flooding
- obstruction
- road_damage
- impassable_section

#### transport
- matatu_obstruction
- bus_stop_congestion
- lane_blocking
- access_disruption

#### electricity
- outage
- unstable_supply
- transformer_issue

#### water
- outage
- low_pressure
- burst_pipe
- sewage_issue

#### environment
- illegal_dumping
- waste_overflow
- public_noise
- pollution

#### safety
- exposed_hazard
- broken_streetlight
- unsafe_crossing

#### governance
- public_service_disruption
- blocked_access_public_facility

#### infrastructure
- broken_drainage
- damaged_footbridge
- damaged_public_asset

---

## Extraction Inputs

The backend may provide:
- free_text
- user_latitude
- user_longitude
- selected_ward
- locale
- current_time_utc
- known_city
- country_code

---

## Required Output JSON Schema

```json
{
  "category": "roads",
  "subcategory": "potholes",
  "condition_level": "difficult",
  "condition_confidence": 0.85,
  "location": {
    "area_name": "Nairobi West",
    "road_name": "Lusaka Road",
    "junction_name": "Lusaka Road / Uhuru Highway",
    "landmark_name": "CFAO Mobility",
    "facility_name": null,
    "location_label": "Potholes near CFAO Mobility at Lusaka Road / Uhuru Highway, Nairobi West",
    "location_precision_type": "road_landmark",
    "location_confidence": 0.82,
    "location_source": "nlp"
  },
  "temporal_hint": {
    "type": "temporary",
    "confidence": 0.64
  },
  "summary": "Potholes making part of the road difficult to use near CFAO Mobility at Lusaka Road / Uhuru Highway.",
  "should_suggest_join": true,
  "reasoning_notes": [
    "Detected road condition",
    "Detected landmark and junction"
  ]
}
```

---

## Field Rules

### category
- required
- must be one of the allowed top-level categories

### subcategory
- required
- must be valid for the chosen category

### condition_level
- required when inferable
- use canonical descriptive condition levels only

### condition_confidence
- required
- float from 0.0 to 1.0

### location
- required object
- fill as much as possible
- unknown fields should be null, not invented

### temporal_hint.type
Allowed values:
- temporary
- continuous
- recurring
- scheduled
- episodic_unknown

### summary
- one neutral sentence
- no blame
- no emotion
- no political language

### should_suggest_join
- boolean

### reasoning_notes
- short machine-oriented hints
- not shown publicly

---

## Prompt Template

You are Hali CSI-NLP, a bounded civic signal extraction engine.

Your task is to convert a user’s free-text civic observation into structured JSON for a civic infrastructure product.

Rules:
1. Return JSON only.
2. Do not include markdown.
3. Do not explain yourself.
4. Do not invent categories outside the allowed taxonomy.
5. Use neutral language only.
6. If location details are uncertain, set unknown fields to null and lower the confidence.
7. Prefer specific human-readable location labels when enough evidence exists.
8. Road-visible conditions should favor road/junction/landmark labeling.
9. Utility conditions may use area-level labels if road-level detail is not present.
10. Output must match the schema exactly.

Allowed categories and subcategories:
[APPLICATION SHOULD INJECT CANONICAL TAXONOMY HERE]

Input context:
- free_text: {FREE_TEXT}
- user_latitude: {USER_LATITUDE}
- user_longitude: {USER_LONGITUDE}
- selected_ward: {SELECTED_WARD}
- locale: {LOCALE}
- current_time_utc: {CURRENT_TIME_UTC}
- known_city: {KNOWN_CITY}
- country_code: {COUNTRY_CODE}

Return JSON only.

---

## Backend Validation Rules

The backend must:
1. parse JSON safely
2. validate required fields
3. validate category against enums
4. validate subcategory/category compatibility
5. clamp confidence values to valid range if needed
6. reject malformed outputs
7. reject outputs with invented categories
8. fallback to manual confirmation UI if confidence is low

---

## Confidence Handling

### Location confidence thresholds
- >= 0.80 → may prefill confidently
- 0.50 to 0.79 → require visible confirmation UI
- < 0.50 → require user correction before submit

### Condition confidence thresholds
- >= 0.75 → accept as suggested
- 0.50 to 0.74 → show editable confirmation
- < 0.50 → treat as weak suggestion

---

## Example Inputs and Outputs

### Example 1
Input:
"Big potholes near National Oil in Nairobi West. The road is terrible especially with the rain."

Expected extraction characteristics:
- category = roads
- subcategory = potholes
- condition_level likely = difficult
- area_name = Nairobi West
- landmark_name = National Oil
- temporal_hint likely = temporary or episodic_unknown

### Example 2
Input:
"Matatus have blocked one lane turning left near CFAO Mobility on Lusaka road."

Expected extraction characteristics:
- category = transport or roads depending taxonomy design
- likely subcategory = lane_blocking or matatu_obstruction
- road_name = Lusaka Road
- landmark_name = CFAO Mobility
- location_precision_type = road_landmark

### Example 3
Input:
"No water in South B since morning."

Expected extraction characteristics:
- category = water
- subcategory = outage
- area_name = South B
- area-level label acceptable
- temporal_hint = temporary or continuous

---

## Engineering Notes

- This service is an extraction component, not a chatbot
- Keep prompts deterministic
- Put provider implementation behind INlpExtractionService
- Keep response DTO separate from domain entity mapping

---

## Field Name Mapping — NLP → API → Database

The same concept uses different names across the three layers. The backend DTO pipeline
must translate explicitly. Do not pass NLP JSON directly to the database or API response.

| NLP JSON output key       | OpenAPI / API DTO field  | Database column         |
|---------------------------|--------------------------|-------------------------|
| `subcategory`             | `subcategorySlug`        | `subcategory_slug`      |
| `condition_level`         | `conditionSlug`          | `condition_slug`        |
| `condition_confidence`    | `conditionConfidence`    | `condition_confidence`  |
| `location.location_label` | `locationLabel`          | `location_label`        |
| `location.location_confidence` | `locationConfidence` | `location_confidence`  |
| `location.location_source` | `locationSource`        | `location_source`       |
| `location.location_precision_type` | `locationPrecisionType` | `location_precision_type` |
| `temporal_hint.type`      | `temporalType`           | `temporal_type`         |
| `summary`                 | `neutralSummary`         | `neutral_summary`       |

Note: condition_confidence (certainty about the civic condition level) and location_confidence
(certainty about the geographic location) are distinct fields stored in separate columns.
Do not conflate them.

The NlpExtractionResultDto (C# record) must map FROM the NLP JSON keys.
The SignalCandidate API schema (OpenAPI) uses camelCase slugs as shown.
The SignalEvent EF Core entity uses snake_case column names as shown.
Each transformation layer (NLP → DTO → Domain → Persistence) must be explicit.
