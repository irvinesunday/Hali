
-- Hali PostgreSQL Schema DDL (MVP Build Pack v2)
-- Extensions
create extension if not exists "uuid-ossp";
create extension if not exists pgcrypto;
create extension if not exists postgis;

-- Shared enums
do $$ begin
    create type account_type as enum ('citizen','institution_user','admin');
exception when duplicate_object then null; end $$;
do $$ begin
    create type auth_method as enum ('phone_otp','email_otp','magic_link','google','apple');
exception when duplicate_object then null; end $$;
do $$ begin
    create type signal_state as enum ('unconfirmed','active','possible_restoration','resolved','expired','suppressed');
exception when duplicate_object then null; end $$;
do $$ begin
    create type participation_type as enum ('affected','observing','no_longer_affected','restoration_yes','restoration_no','restoration_unsure');
exception when duplicate_object then null; end $$;
do $$ begin
    create type official_post_type as enum ('live_update','scheduled_disruption','advisory_public_notice');
exception when duplicate_object then null; end $$;
do $$ begin
    create type location_precision_type as enum ('area','road','junction','landmark','facility','pin','road_landmark');
exception when duplicate_object then null; end $$;
do $$ begin
    create type civic_category as enum ('roads','water','electricity','transport','safety','environment','governance','infrastructure');
exception when duplicate_object then null; end $$;

create table if not exists accounts (
    id uuid primary key default gen_random_uuid(),
    account_type account_type not null,
    display_name varchar(80),
    email varchar(255),
    phone_e164 varchar(30),
    is_email_verified boolean not null default false,
    is_phone_verified boolean not null default false,
    status varchar(30) not null default 'active',
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    unique (email),
    unique (phone_e164)
);

create table if not exists devices (
    id uuid primary key default gen_random_uuid(),
    account_id uuid references accounts(id) on delete set null,
    device_fingerprint_hash varchar(128) not null,
    device_integrity_level varchar(30) not null default 'unknown',
    platform varchar(30) not null,
    app_version varchar(30),
    expo_push_token varchar(200),
    first_seen_at timestamptz not null default now(),
    last_seen_at timestamptz not null default now(),
    is_blocked boolean not null default false,
    unique (device_fingerprint_hash)
);

create table if not exists otp_challenges (
    id uuid primary key default gen_random_uuid(),
    account_id uuid references accounts(id) on delete cascade,
    auth_method auth_method not null,
    destination varchar(255) not null,
    otp_hash varchar(255) not null,
    expires_at timestamptz not null,
    consumed_at timestamptz,
    created_at timestamptz not null default now()
);

create table if not exists refresh_tokens (
    id uuid primary key default gen_random_uuid(),
    token_hash varchar(255) not null,
    account_id uuid not null references accounts(id) on delete cascade,
    device_id uuid references devices(id) on delete cascade,
    created_at timestamptz not null default now(),
    expires_at timestamptz not null,
    revoked_at timestamptz
);
create unique index if not exists uix_refresh_tokens_token_hash on refresh_tokens(token_hash);
create index if not exists ix_refresh_tokens_account_id on refresh_tokens(account_id);
create index if not exists ix_refresh_tokens_device_id on refresh_tokens(device_id);
create index if not exists ix_refresh_tokens_expires_at on refresh_tokens(expires_at);

create table if not exists localities (
    id uuid primary key default gen_random_uuid(),
    country_code varchar(2) not null,
    county_name varchar(120),
    city_name varchar(120) not null,
    ward_name varchar(120) not null,
    ward_code varchar(50),
    geom geometry(MultiPolygon,4326) not null,
    created_at timestamptz not null default now()
);

create table if not exists institutions (
    id uuid primary key default gen_random_uuid(),
    name varchar(160) not null,
    institution_type varchar(60) not null,
    jurisdiction_label varchar(160),
    is_verified boolean not null default false,
    created_at timestamptz not null default now()
);

create table if not exists institution_jurisdictions (
    id uuid primary key default gen_random_uuid(),
    institution_id uuid not null references institutions(id) on delete cascade,
    locality_id uuid references localities(id) on delete set null,
    corridor_name varchar(160),
    geom geometry(MultiPolygon,4326),
    created_at timestamptz not null default now()
);

create table if not exists location_labels (
    id uuid primary key default gen_random_uuid(),
    locality_id uuid references localities(id) on delete set null,
    area_name varchar(160),
    road_name varchar(160),
    junction_name varchar(160),
    landmark_name varchar(160),
    facility_name varchar(160),
    location_label varchar(255) not null,
    location_precision_type location_precision_type not null,
    latitude double precision,
    longitude double precision,
    geom geometry(Point,4326),
    created_at timestamptz not null default now()
);

create table if not exists taxonomy_categories (
    id uuid primary key default gen_random_uuid(),
    category civic_category not null,
    subcategory_slug varchar(80) not null,
    display_name varchar(120) not null,
    description text,
    is_active boolean not null default true,
    unique(category, subcategory_slug)
);

create table if not exists taxonomy_conditions (
    id uuid primary key default gen_random_uuid(),
    category civic_category not null,
    condition_slug varchar(80) not null,
    display_name varchar(120) not null,
    ordinal smallint,
    is_positive boolean not null default false,
    unique(category, condition_slug)
);

create table if not exists signal_events (
    id uuid primary key default gen_random_uuid(),
    account_id uuid references accounts(id) on delete set null,
    device_id uuid references devices(id) on delete set null,
    locality_id uuid references localities(id) on delete set null,
    location_label_id uuid references location_labels(id) on delete set null,
    category civic_category not null,
    subcategory_slug varchar(80),
    condition_slug varchar(80),
    free_text text,
    neutral_summary varchar(240),
    temporal_type varchar(40) not null default 'episodic_unknown',
    -- Direct coordinates stored per locked decision: "store raw lat/lng separately in addition to spatial_cell_id"
    latitude double precision,
    longitude double precision,
    -- NLP extraction confidence and source, used by mobile confirmation UI threshold logic
    location_confidence numeric(4,3),
    location_source varchar(20),
    -- NLP certainty about the condition level (e.g. how certain 'difficult' vs 'impassable')
    condition_confidence numeric(4,3),
    occurred_at timestamptz not null default now(),
    created_at timestamptz not null default now(),
    source_language varchar(20),
    source_channel varchar(30) not null default 'app',
    spatial_cell_id varchar(80),
    civis_precheck jsonb not null default '{}'::jsonb
);
create index if not exists ix_signal_events_locality_category_time on signal_events(locality_id, category, occurred_at desc);
create index if not exists ix_signal_events_spatial_cell_time on signal_events(spatial_cell_id, occurred_at desc);

create table if not exists signal_clusters (
    id uuid primary key default gen_random_uuid(),
    locality_id uuid references localities(id) on delete set null,
    category civic_category not null,
    subcategory_slug varchar(80),
    dominant_condition_slug varchar(80),
    state signal_state not null default 'unconfirmed',
    title varchar(240) not null,
    summary varchar(280) not null,
    location_label_id uuid references location_labels(id) on delete set null,
    centroid geometry(Point,4326),
    spatial_cell_id varchar(80),
    first_seen_at timestamptz not null,
    last_seen_at timestamptz not null,
    activated_at timestamptz,
    resolved_at timestamptz,
    possible_restoration_at timestamptz,
    civis_score numeric(8,4),
    wrab numeric(10,4),
    sds numeric(10,4),
    macf integer,
    raw_confirmation_count integer not null default 0,
    -- Temporal classification — drives home feed section routing (Active now / Recurring / Other)
    temporal_type varchar(40) not null default 'episodic_unknown',
    -- Denormalised participation counts — maintained by clustering worker, used by cluster detail API
    affected_count integer not null default 0,
    observing_count integer not null default 0
    -- NOTE: no table-level unique constraint on title.
    -- Deduplication is enforced in application clustering logic (join-score threshold).
    -- A partial unique index across (spatial_cell_id, category) is intentionally omitted
    -- because one cell can legitimately hold multiple concurrent active clusters of the
    -- same category (e.g. two distinct road closures on the same H3 cell).
);
create index if not exists ix_signal_clusters_state_locality_category on signal_clusters(state, locality_id, category);
create index if not exists ix_signal_clusters_last_seen on signal_clusters(last_seen_at desc);
create index if not exists ix_signal_clusters_spatial_cell_category on signal_clusters(spatial_cell_id, category);

create table if not exists cluster_event_links (
    id uuid primary key default gen_random_uuid(),
    cluster_id uuid not null references signal_clusters(id) on delete cascade,
    signal_event_id uuid not null references signal_events(id) on delete cascade,
    link_reason varchar(50) not null,
    linked_at timestamptz not null default now(),
    unique(cluster_id, signal_event_id)
);

create table if not exists participations (
    id uuid primary key default gen_random_uuid(),
    cluster_id uuid not null references signal_clusters(id) on delete cascade,
    account_id uuid references accounts(id) on delete set null,
    device_id uuid references devices(id) on delete set null,
    participation_type participation_type not null,
    context_text varchar(150),
    created_at timestamptz not null default now(),
    idempotency_key varchar(100),
    unique(cluster_id, device_id, participation_type, idempotency_key)
);
create index if not exists ix_participations_cluster_type_time on participations(cluster_id, participation_type, created_at desc);

create table if not exists official_posts (
    id uuid primary key default gen_random_uuid(),
    institution_id uuid not null references institutions(id) on delete cascade,
    author_account_id uuid references accounts(id) on delete set null,
    official_post_type official_post_type not null,
    category civic_category,
    title varchar(220) not null,
    body text not null,
    starts_at timestamptz,
    ends_at timestamptz,
    status varchar(30) not null default 'published',
    related_cluster_id uuid references signal_clusters(id) on delete set null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists official_post_scopes (
    id uuid primary key default gen_random_uuid(),
    official_post_id uuid not null references official_posts(id) on delete cascade,
    locality_id uuid references localities(id) on delete set null,
    corridor_name varchar(160),
    geom geometry(MultiPolygon,4326)
);

create table if not exists follows (
    id uuid primary key default gen_random_uuid(),
    account_id uuid not null references accounts(id) on delete cascade,
    locality_id uuid not null references localities(id) on delete cascade,
    created_at timestamptz not null default now(),
    unique(account_id, locality_id)
);

create table if not exists notifications (
    id uuid primary key default gen_random_uuid(),
    account_id uuid not null references accounts(id) on delete cascade,
    channel varchar(30) not null,
    notification_type varchar(50) not null,
    payload jsonb not null,
    send_after timestamptz not null default now(),
    sent_at timestamptz,
    status varchar(30) not null default 'queued',
    dedupe_key varchar(120),
    unique(dedupe_key)
);

create table if not exists admin_audit_logs (
    id uuid primary key default gen_random_uuid(),
    actor_account_id uuid references accounts(id) on delete set null,
    action varchar(100) not null,
    target_type varchar(60),
    target_id uuid,
    metadata jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now()
);

create table if not exists civis_decisions (
    id uuid primary key default gen_random_uuid(),
    cluster_id uuid references signal_clusters(id) on delete cascade,
    decision_type varchar(50) not null,
    reason_codes jsonb not null default '[]'::jsonb,
    metrics jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now()
);

create table if not exists outbox_events (
    id uuid primary key default gen_random_uuid(),
    aggregate_type varchar(60) not null,
    aggregate_id uuid not null,
    event_type varchar(80) not null,
    schema_version varchar(20) not null default '1.0',
    payload jsonb not null,
    occurred_at timestamptz not null default now(),
    published_at timestamptz
);
-- Back-compat for CI DBs that already created outbox_events before #207
-- Phase 4 added schema_version. The EF migration
-- B10_AddSchemaVersionToOutboxEvents handles the production path.
alter table outbox_events add column if not exists schema_version varchar(20) not null default '1.0';
create index if not exists ix_outbox_unpublished on outbox_events(published_at) where published_at is null;

-- PostGIS GiST spatial indexes — required for ST_DWithin proximity queries in clustering service
create index if not exists ix_localities_geom on localities using gist(geom);
create index if not exists ix_institution_jurisdictions_geom on institution_jurisdictions using gist(geom);
create index if not exists ix_location_labels_geom on location_labels using gist(geom);
create index if not exists ix_signal_clusters_centroid on signal_clusters using gist(centroid);
-- Required for institution geo-scope enforcement in official updates (Phase 10)
create index if not exists ix_official_post_scopes_geom on official_post_scopes using gist(geom);

-- Operational indexes for background workers
-- Notification worker queries: WHERE status = 'queued' AND send_after <= NOW()
create index if not exists ix_notifications_queued_send_after on notifications(send_after)
    where status = 'queued';
