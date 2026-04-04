# Hali — Canonical Schema Reference
**Source of truth for all database tables, enums, constraints, and indexes.**

This file reflects all patches from the Platform Reconciliation document. The DDL here is the reference for EF Core migration authors — do not apply it directly. Each section that is a Phase 2 or Phase 3 addition is marked.

---

## Enums

```sql
-- Account type — 3 values. Role granularity is in institution_memberships, not here.
create type account_type as enum ('citizen', 'institution_user', 'admin');

-- Auth methods
create type auth_method as enum ('phone_otp', 'email_otp', 'magic_link', 'google', 'apple');

-- Civic categories — EXACTLY 8. No health/education/other in MVP.
create type civic_category as enum (
    'roads', 'transport', 'electricity', 'water',
    'environment', 'safety', 'governance', 'infrastructure'
);

-- Signal cluster lifecycle states
create type signal_state as enum (
    'unconfirmed', 'active', 'possible_restoration', 'resolved', 'expired', 'suppressed'
);

-- Citizen participation types
create type participation_type as enum (
    'affected', 'observing', 'no_longer_affected',
    'restoration_yes', 'restoration_no', 'restoration_unsure'
);

-- Official post types
create type official_post_type as enum (
    'live_update', 'scheduled_disruption', 'advisory_public_notice'
);

-- Location precision types for location_labels
create type location_precision_type as enum (
    'area', 'road', 'junction', 'landmark', 'facility', 'pin'
);
```

---

## Core tables

### accounts
```sql
create table if not exists accounts (
    id                  uuid primary key default gen_random_uuid(),
    account_type        account_type not null,
    display_name        varchar(80),
    email               varchar(255),
    phone_e164          varchar(30),
    is_email_verified   boolean not null default false,
    is_phone_verified   boolean not null default false,
    status              varchar(30) not null default 'active',  -- active | suspended | deleted
    created_at          timestamptz not null default now(),
    updated_at          timestamptz not null default now(),
    unique (email),
    unique (phone_e164)
);
```

### devices
```sql
create table if not exists devices (
    id                      uuid primary key default gen_random_uuid(),
    account_id              uuid references accounts(id) on delete set null,
    device_fingerprint_hash varchar(128) not null,
    device_integrity_level  varchar(30) not null default 'unknown',  -- unknown | low | medium | high
    platform                varchar(30) not null,  -- ios | android | web
    app_version             varchar(30),
    push_token              varchar(512),  -- Expo push token
    push_token_updated_at   timestamptz,
    first_seen_at           timestamptz not null default now(),
    last_seen_at            timestamptz not null default now(),
    is_blocked              boolean not null default false,
    unique (device_fingerprint_hash)
);
```

**Note:** `push_token` stored on device row. Updated via `POST /v1/devices/push-token` after each OTP verify or token rotation.

### otp_challenges
```sql
create table if not exists otp_challenges (
    id          uuid primary key default gen_random_uuid(),
    account_id  uuid references accounts(id) on delete cascade,
    auth_method auth_method not null,
    destination varchar(255) not null,  -- phone or email
    otp_hash    varchar(255) not null,  -- bcrypt hash of the OTP
    expires_at  timestamptz not null,
    consumed_at timestamptz,
    created_at  timestamptz not null default now()
);
```

### refresh_tokens *(Phase 1 — MUST ADD)*
```sql
create table if not exists refresh_tokens (
    id                      uuid primary key default gen_random_uuid(),
    token_hash              varchar(128) not null,  -- SHA-256 hex of the raw token
    account_id              uuid not null references accounts(id) on delete cascade,
    device_id               uuid references devices(id) on delete set null,
    created_at              timestamptz not null default now(),
    expires_at              timestamptz not null,
    revoked_at              timestamptz,
    replaced_by_token_hash  varchar(128),  -- set on rotation; if old token re-presented → theft signal
    unique (token_hash)
);
create index if not exists ix_refresh_tokens_account_id on refresh_tokens(account_id);
create index if not exists ix_refresh_tokens_device_id on refresh_tokens(device_id);
create index if not exists ix_refresh_tokens_expires_at on refresh_tokens(expires_at);
```

---

## Geography tables

### localities
```sql
create table if not exists localities (
    id              uuid primary key default gen_random_uuid(),
    country_code    varchar(2) not null,
    county_name     varchar(120),
    city_name       varchar(120) not null,
    ward_name       varchar(120) not null,
    ward_code       varchar(50),
    geom            geometry(MultiPolygon, 4326) not null,
    created_at      timestamptz not null default now()
);
create index if not exists ix_localities_geom on localities using gist(geom);
```

### location_labels
```sql
create table if not exists location_labels (
    id                      uuid primary key default gen_random_uuid(),
    locality_id             uuid references localities(id) on delete set null,
    area_name               varchar(160),
    road_name               varchar(160),
    junction_name           varchar(160),
    landmark_name           varchar(160),
    facility_name           varchar(160),
    location_label          varchar(255) not null,  -- human-readable composite label
    location_precision_type location_precision_type not null,
    latitude                double precision,
    longitude               double precision,
    geom                    geometry(Point, 4326),
    created_at              timestamptz not null default now()
);
```

---

## Institution tables

### institutions
```sql
create table if not exists institutions (
    id                  uuid primary key default gen_random_uuid(),
    name                varchar(160) not null,
    institution_type    varchar(60) not null,  -- utility | government | transport | ngo | other
    jurisdiction_label  varchar(160),
    is_verified         boolean not null default false,
    created_at          timestamptz not null default now()
);
```

### institution_jurisdictions
```sql
create table if not exists institution_jurisdictions (
    id              uuid primary key default gen_random_uuid(),
    institution_id  uuid not null references institutions(id) on delete cascade,
    locality_id     uuid references localities(id) on delete set null,
    corridor_name   varchar(160),
    geom            geometry(MultiPolygon, 4326),
    created_at      timestamptz not null default now()
);
create index if not exists ix_institution_jurisdictions_geom
    on institution_jurisdictions using gist(geom);
```

### institution_memberships *(Phase 2 — ADD before institution surface build)*
```sql
create table if not exists institution_memberships (
    id                      uuid primary key default gen_random_uuid(),
    institution_id          uuid not null references institutions(id) on delete cascade,
    account_id              uuid not null references accounts(id) on delete cascade,
    role                    varchar(60) not null,
    -- institution_viewer | institution_operator | institution_manager | institution_admin
    is_active               boolean not null default true,
    invited_by_account_id   uuid references accounts(id) on delete set null,
    created_at              timestamptz not null default now(),
    updated_at              timestamptz not null default now(),
    unique (institution_id, account_id)  -- one institution per user in MVP
);
create index if not exists ix_institution_memberships_account on institution_memberships(account_id);
```

**Doctrine:** One user, one institution in Phase 2 MVP. The unique constraint enforces this.

### institution_user_scopes *(Phase 2)*
```sql
create table if not exists institution_user_scopes (
    id              uuid primary key default gen_random_uuid(),
    membership_id   uuid not null references institution_memberships(id) on delete cascade,
    scope_type      varchar(30) not null,  -- 'geo' | 'category'
    locality_id     uuid references localities(id) on delete cascade,
    corridor_name   varchar(160),
    category        civic_category,
    created_at      timestamptz not null default now()
);
```

**Default:** No rows = user inherits full institution jurisdiction. Rows restrict access within that jurisdiction.

---

## Taxonomy tables

### taxonomy_categories
```sql
create table if not exists taxonomy_categories (
    id              uuid primary key default gen_random_uuid(),
    category        civic_category not null,
    subcategory_slug varchar(80) not null,
    display_name    varchar(120) not null,
    description     text,
    is_active       boolean not null default true,
    unique (category, subcategory_slug)
);
```

### taxonomy_conditions
```sql
create table if not exists taxonomy_conditions (
    id              uuid primary key default gen_random_uuid(),
    category        civic_category not null,
    condition_slug  varchar(80) not null,
    display_name    varchar(120) not null,
    ordinal         smallint,
    is_positive     boolean not null default false,
    unique (category, condition_slug)
);
```

---

## Signal tables

### signal_events
```sql
create table if not exists signal_events (
    id                  uuid primary key default gen_random_uuid(),
    account_id          uuid references accounts(id) on delete set null,
    device_id           uuid references devices(id) on delete set null,
    locality_id         uuid references localities(id) on delete set null,
    location_label_id   uuid references location_labels(id) on delete set null,
    category            civic_category not null,
    subcategory_slug    varchar(80),
    condition_slug      varchar(80),
    free_text           text,
    neutral_summary     varchar(240),
    temporal_type       varchar(40) not null default 'episodic_unknown',
    occurred_at         timestamptz not null default now(),
    created_at          timestamptz not null default now(),
    source_language     varchar(20),
    source_channel      varchar(30) not null default 'app',
    spatial_cell_id     varchar(80),  -- H3 resolution 9
    civis_precheck      jsonb not null default '{}'::jsonb
);
create index if not exists ix_signal_events_locality_category_time
    on signal_events(locality_id, category, occurred_at desc);
create index if not exists ix_signal_events_spatial_cell_time
    on signal_events(spatial_cell_id, occurred_at desc);
```

### signal_clusters
```sql
create table if not exists signal_clusters (
    id                          uuid primary key default gen_random_uuid(),
    locality_id                 uuid references localities(id) on delete set null,
    category                    civic_category not null,
    subcategory_slug            varchar(80),
    dominant_condition_slug     varchar(80),
    state                       signal_state not null default 'unconfirmed',
    title                       varchar(240) not null,
    summary                     varchar(280) not null,
    location_label_id           uuid references location_labels(id) on delete set null,
    centroid                    geometry(Point, 4326),
    spatial_cell_id             varchar(80),
    first_seen_at               timestamptz not null,
    last_seen_at                timestamptz not null,
    activated_at                timestamptz,
    resolved_at                 timestamptz,
    possible_restoration_at     timestamptz,
    civis_score                 numeric(8, 4),   -- INTERNAL — never expose in public API
    wrab                        numeric(10, 4),  -- INTERNAL
    sds                         numeric(10, 4),  -- INTERNAL
    macf                        integer,         -- INTERNAL
    raw_confirmation_count      integer not null default 0,  -- INTERNAL — use public_confirmation_count in DTOs
    public_confirmation_count   integer not null default 0,  -- safe for public surfaces
    affected_count              integer not null default 0,
    observing_count             integer not null default 0,
    temporal_type               varchar(40) not null default 'episodic_unknown',
    created_at                  timestamptz not null default now(),
    updated_at                  timestamptz not null default now()
);

-- Prevent duplicate live clusters in the same spatial cell
-- (replaces the old unique constraint that included state)
create unique index if not exists ix_clusters_active_identity
    on signal_clusters(locality_id, category, spatial_cell_id)
    where state in ('unconfirmed', 'active', 'possible_restoration');

create index if not exists ix_signal_clusters_state_locality_category
    on signal_clusters(state, locality_id, category);
create index if not exists ix_signal_clusters_last_seen
    on signal_clusters(last_seen_at desc);
create index if not exists ix_signal_clusters_centroid
    on signal_clusters using gist(centroid);
```

### cluster_event_links
```sql
create table if not exists cluster_event_links (
    id              uuid primary key default gen_random_uuid(),
    cluster_id      uuid not null references signal_clusters(id) on delete cascade,
    signal_event_id uuid not null references signal_events(id) on delete cascade,
    link_reason     varchar(50) not null,
    linked_at       timestamptz not null default now(),
    unique (cluster_id, signal_event_id)
);
```

### participations
```sql
create table if not exists participations (
    id                  uuid primary key default gen_random_uuid(),
    cluster_id          uuid not null references signal_clusters(id) on delete cascade,
    account_id          uuid references accounts(id) on delete set null,
    device_id           uuid references devices(id) on delete set null,
    participation_type  participation_type not null,
    context_text        varchar(150),
    created_at          timestamptz not null default now(),
    idempotency_key     varchar(100)
);

-- Deduplication gate: one participation type per device per cluster
-- (partial index — only applies where device_id is not null)
create unique index if not exists ix_participations_device_cluster_type
    on participations(cluster_id, device_id, participation_type)
    where device_id is not null;

-- Idempotency replay guard (separate from above)
create unique index if not exists ix_participations_idempotency
    on participations(idempotency_key)
    where idempotency_key is not null;

create index if not exists ix_participations_cluster_type_time
    on participations(cluster_id, participation_type, created_at desc);
```

**Why two indexes:** The first prevents duplicate state for the same device on the same cluster. The second prevents replay of the same API call. They are different concerns.

---

## Official update tables

### official_posts
```sql
create table if not exists official_posts (
    id                  uuid primary key default gen_random_uuid(),
    institution_id      uuid not null references institutions(id) on delete cascade,
    author_account_id   uuid references accounts(id) on delete set null,
    official_post_type  official_post_type not null,
    category            civic_category,
    title               varchar(220) not null,
    body                text not null,
    starts_at           timestamptz,
    ends_at             timestamptz,
    status              varchar(30) not null default 'published',  -- draft | published | withdrawn | expired
    related_cluster_id  uuid references signal_clusters(id) on delete set null,
    is_restoration_claim boolean not null default false,  -- MUST BE SET: triggers possible_restoration lifecycle
    created_at          timestamptz not null default now(),
    updated_at          timestamptz not null default now()
);
create index if not exists ix_official_posts_institution_status
    on official_posts(institution_id, status, created_at desc);
create index if not exists ix_official_posts_cluster
    on official_posts(related_cluster_id)
    where related_cluster_id is not null;
```

### official_post_scopes
```sql
create table if not exists official_post_scopes (
    id              uuid primary key default gen_random_uuid(),
    official_post_id uuid not null references official_posts(id) on delete cascade,
    locality_id     uuid references localities(id) on delete set null,
    corridor_name   varchar(160),
    geom            geometry(MultiPolygon, 4326)
);
```

### official_update_templates *(Phase 2)*
```sql
create table if not exists official_update_templates (
    id                      uuid primary key default gen_random_uuid(),
    institution_id          uuid not null references institutions(id) on delete cascade,
    official_post_type      official_post_type not null,
    category                civic_category,
    name                    varchar(120) not null,
    title_template          varchar(220),
    body_template           text,
    is_active               boolean not null default true,
    created_by_account_id   uuid references accounts(id) on delete set null,
    created_at              timestamptz not null default now(),
    updated_at              timestamptz not null default now()
);
```

---

## Participation and follow tables

### follows
```sql
create table if not exists follows (
    id          uuid primary key default gen_random_uuid(),
    account_id  uuid not null references accounts(id) on delete cascade,
    locality_id uuid not null references localities(id) on delete cascade,
    created_at  timestamptz not null default now(),
    unique (account_id, locality_id)
    -- Max 5 per account — enforced at application layer with 422 policy_blocked
);
```

---

## Notification tables

### notifications
```sql
create table if not exists notifications (
    id                  uuid primary key default gen_random_uuid(),
    account_id          uuid not null references accounts(id) on delete cascade,
    channel             varchar(30) not null,  -- push | email | sms
    notification_type   varchar(50) not null,
    payload             jsonb not null,
    send_after          timestamptz not null default now(),
    sent_at             timestamptz,
    status              varchar(30) not null default 'queued',  -- queued | sent | failed | skipped
    dedupe_key          varchar(120),
    unique (dedupe_key)
);
create index if not exists ix_notifications_account_status
    on notifications(account_id, status, send_after);
```

### institution_notification_recipients *(Phase 2)*
```sql
create table if not exists institution_notification_recipients (
    id                  uuid primary key default gen_random_uuid(),
    institution_id      uuid not null references institutions(id) on delete cascade,
    account_id          uuid references accounts(id) on delete set null,
    email               varchar(255),
    notification_type   varchar(80) not null,
    -- cluster_activated_in_scope | cluster_possible_restoration | cluster_resolved
    is_active           boolean not null default true,
    created_at          timestamptz not null default now(),
    unique (institution_id, account_id, notification_type)
);
```

---

## CIVIS and analytics tables

### civis_decisions
```sql
create table if not exists civis_decisions (
    id              uuid primary key default gen_random_uuid(),
    cluster_id      uuid references signal_clusters(id) on delete cascade,
    decision_type   varchar(50) not null,  -- activation_evaluation | decay_check | restoration_check
    reason_codes    jsonb not null default '[]'::jsonb,
    metrics         jsonb not null default '{}'::jsonb,
    -- metrics: { wrab, sds, macf, active_mass, unique_device_count, burst_penalty }
    created_at      timestamptz not null default now()
);
create index if not exists ix_civis_decisions_cluster_time
    on civis_decisions(cluster_id, created_at desc);
```

### tda_snapshots
```sql
create table if not exists tda_snapshots (
    id                      uuid primary key default gen_random_uuid(),
    cluster_lineage_id      uuid not null,  -- originating cluster id
    category                civic_category not null,
    locality_id             uuid references localities(id) on delete set null,
    spatial_cell_id         varchar(80),
    temporal_class          varchar(40) not null,
    -- temporary | continuous | recurring | scheduled | episodic_unknown
    peak_hour_of_day        smallint,
    peak_day_of_week        smallint,
    recurrence_confidence   numeric(5, 4),
    pattern_data            jsonb not null default '{}'::jsonb,
    snapshot_at             timestamptz not null default now(),
    created_at              timestamptz not null default now()
);
create index if not exists ix_tda_snapshots_locality_category
    on tda_snapshots(locality_id, category);
create index if not exists ix_tda_snapshots_cell_category
    on tda_snapshots(spatial_cell_id, category);
```

---

## Audit and event tables

### admin_audit_logs
```sql
create table if not exists admin_audit_logs (
    id                  uuid primary key default gen_random_uuid(),
    actor_account_id    uuid references accounts(id) on delete set null,
    actor_role          varchar(60),
    action              varchar(100) not null,
    target_type         varchar(60),
    target_id           uuid,
    before_snapshot     jsonb,
    after_snapshot      jsonb,
    reason_code         varchar(80),
    rule_version        varchar(30),
    metadata            jsonb not null default '{}'::jsonb,
    created_at          timestamptz not null default now()
);
create index if not exists ix_audit_logs_actor_time
    on admin_audit_logs(actor_account_id, created_at desc);
create index if not exists ix_audit_logs_target
    on admin_audit_logs(target_type, target_id);
```

**CIVIS parameter changes must always include** `before_snapshot`, `after_snapshot`, and `rule_version`.

### outbox_events
```sql
create table if not exists outbox_events (
    id              uuid primary key default gen_random_uuid(),
    aggregate_type  varchar(60) not null,
    aggregate_id    uuid not null,
    event_type      varchar(80) not null,
    payload         jsonb not null,
    occurred_at     timestamptz not null default now(),
    published_at    timestamptz
);
create index if not exists ix_outbox_unpublished
    on outbox_events(published_at)
    where published_at is null;
```

---

## Trigger: transactional outbox on cluster state change

This trigger ensures outbox emission is atomic with every state change. Add via migration.

```sql
create or replace function fn_emit_cluster_state_changed()
returns trigger as $$
begin
    if old.state <> new.state then
        insert into outbox_events(aggregate_type, aggregate_id, event_type, payload, occurred_at)
        values (
            'signal_cluster',
            new.id,
            'cluster.state_changed',
            jsonb_build_object(
                'old_state', old.state,
                'new_state', new.state,
                'locality_id', new.locality_id,
                'category', new.category,
                'occurred_at', now()
            ),
            now()
        );
    end if;
    return new;
end;
$$ language plpgsql;

create trigger trg_cluster_state_changed
    after update on signal_clusters
    for each row
    when (old.state is distinct from new.state)
    execute function fn_emit_cluster_state_changed();
```

---

## Migration conventions

1. Each module owns its own migration set. Auth module owns `refresh_tokens`. Institutions module owns `institution_memberships` etc.
2. Migration naming: `{timestamp}_{Module}_{Description}` e.g. `20260401120000_Auth_AddRefreshTokens`
3. Never edit the reference SQL files as a migration mechanism.
4. Phase 2 tables are created in Phase 2 migrations — do not pre-create them in Phase 1.
5. Enum additions require `ALTER TYPE ... ADD VALUE` — they cannot be rolled back. Plan carefully.
6. All migrations must be idempotent where possible (`CREATE TABLE IF NOT EXISTS`, `IF NOT EXISTS` on indexes).

---

## What must never appear in a public API response

The following column names are internal. Any DTO for a public or institution-facing endpoint must explicitly omit them:

- `civis_score`
- `wrab`
- `sds`
- `macf`
- `raw_confirmation_count`
- `civis_precheck`
- `device_fingerprint_hash`
- `device_integrity_level`
- `account_id` (in any aggregate or cluster context)
- `device_id` (in any public context)
- `token_hash`
- `otp_hash`
