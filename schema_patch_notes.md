# Schema Patch Notes for Claude Code

## 1. Add infrastructure to civic_category enum
In `01_postgres_schema.sql`, update the `civic_category` enum to include:

- roads
- transport
- electricity
- water
- environment
- safety
- governance
- infrastructure

Remove: health, education, other (not in MVP NLP taxonomy).

## 2. Add refresh_tokens table
Add a `refresh_tokens` table with at minimum:
- id
- token_hash
- account_id
- device_id
- created_at
- expires_at
- revoked_at

Recommended indexes:
- unique index on token_hash
- index on account_id
- index on device_id
- index on expires_at

## 3. OpenAPI alignment changes
Ensure the OpenAPI spec includes:
- `POST /v1/auth/refresh`
- `GET /v1/home`
- `GET /v1/localities/followed`
- `PUT /v1/localities/followed`
- `POST /v1/signals/preview`

Use versioned `/v1/*` routes consistently.

## 4. Add road_landmark to location_precision_type enum
The NLP extraction layer returns `"location_precision_type": "road_landmark"` for
road + landmark combined precision. Add this value to the enum:

('area','road','junction','landmark','facility','pin','road_landmark')

Apply via EF Core migration: `ALTER TYPE location_precision_type ADD VALUE 'road_landmark';`

## 5. Add expo_push_token column to devices table
The `POST /v1/devices/push-token` endpoint stores Expo push tokens per device.
Add to the devices table:

    expo_push_token varchar(200)

Apply via EF Core migration on the Auth/Devices module.

## 6. Remove signal_clusters unique constraint on title
The original DDL had `unique(locality_id, category, spatial_cell_id, state, title)`.
This constraint is removed. Deduplication is enforced in application clustering logic
via the join-score threshold (>= 0.65). A new covering index replaces it:

    CREATE INDEX ix_signal_clusters_spatial_cell_category
    ON signal_clusters(spatial_cell_id, category);

Apply via EF Core migration on the Clusters module.

## 7. Correct civic_category enum (remove health, education, other — add infrastructure)
The MVP taxonomy only covers 8 categories. The enum must read exactly:

    ('roads','water','electricity','transport','safety','environment','governance','infrastructure')

Apply via EF Core migration. In PostgreSQL, enum values cannot be removed with
ALTER TYPE — the migration must DROP and recreate the type (safe on a fresh DB),
or use a new enum name if data already exists.

## 8. Add direct coordinates to signal_events
Per locked decision §5: "store raw lat/lng separately in addition to spatial_cell_id."
The clustering service requires direct coordinate access without a nullable FK join.
Add to signal_events:

    latitude  double precision
    longitude double precision

Apply via EF Core migration on the Signals module.

## 9. Add NLP location fields to signal_events
The NLP extraction output includes location_confidence and location_source.
These drive the mobile confirmation UI threshold logic (≥0.80 / 0.50–0.79 / <0.50).
They must be stored on the event record. Add to signal_events:

    location_confidence  numeric(4,3)   -- e.g. 0.820
    location_source      varchar(20)    -- 'nlp' | 'search' | 'pin'

Apply via EF Core migration on the Signals module.

## 10. Enforce one active participation type per device per cluster at application layer
The DB constraint unique(cluster_id, device_id, participation_type, idempotency_key)
does not prevent a device from holding both 'affected' and 'observing' rows.
The clustering service must enforce: before inserting a new participation row, soft-delete
or overwrite any existing participation row for that (cluster_id, device_id) pair.
This is an application-layer rule, not a DB constraint.

## 11. Add condition_confidence column to signal_events
The NLP extraction output includes condition_confidence (certainty about the civic
condition level, e.g. how certain the model is that the road is 'difficult' vs
'impassable'). This is distinct from location_confidence. Add to signal_events:

    condition_confidence  numeric(4,3)   -- e.g. 0.850

Apply via EF Core migration on the Signals module.

## 12. Add GiST spatial indexes on all geometry columns
PostGIS ST_DWithin proximity queries — used by the clustering service on every signal
submission — require GiST indexes. Without them every proximity call does a full table
scan. Add the following:

    CREATE INDEX ix_localities_geom ON localities USING GIST(geom);
    CREATE INDEX ix_institution_jurisdictions_geom ON institution_jurisdictions USING GIST(geom);
    CREATE INDEX ix_location_labels_geom ON location_labels USING GIST(geom);
    CREATE INDEX ix_signal_clusters_centroid ON signal_clusters USING GIST(centroid);

Apply via EF Core migration — one migration per module that owns the table.
