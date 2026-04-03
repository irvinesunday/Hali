using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Application.Signals;
using Xunit;
using Hali.Infrastructure.Data.Advisories;
using Hali.Infrastructure.Data.Auth;
using Hali.Infrastructure.Data.Clusters;
using Hali.Infrastructure.Data.Notifications;
using Hali.Infrastructure.Data.Participation;
using Hali.Infrastructure.Data.Signals;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Hali.Tests.Integration.Infrastructure;

/// <summary>
/// Boots the Hali API against a real PostgreSQL test database.
/// All external side-effect services (SMS, NLP, Geocoding) are replaced with
/// test doubles. Redis is the real instance on localhost:6379.
/// </summary>
public sealed class HaliWebApplicationFactory
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Auth"]          = TestConstants.ConnectionString,
                ["ConnectionStrings:Signals"]        = TestConstants.ConnectionString,
                ["ConnectionStrings:Clusters"]       = TestConstants.ConnectionString,
                ["ConnectionStrings:Participation"]  = TestConstants.ConnectionString,
                ["ConnectionStrings:Advisories"]     = TestConstants.ConnectionString,
                ["ConnectionStrings:Notifications"]  = TestConstants.ConnectionString,
                ["ConnectionStrings:Admin"]          = TestConstants.ConnectionString,
                ["Redis:Url"]                        = "localhost:6379",
                ["Auth:JwtSecret"]                   = TestConstants.JwtSecret,
                ["Auth:JwtIssuer"]                   = TestConstants.JwtIssuer,
                ["Auth:JwtAudience"]                 = TestConstants.JwtAudience,
                ["Auth:JwtExpiryMinutes"]            = "60",
                ["Auth:RefreshTokenExpiryDays"]      = "30",
                ["Otp:Length"]                       = "6",
                ["Otp:TtlMinutes"]                   = "10",
                ["Otp:MaxRequestsPerWindow"]         = "100",
                ["Otp:WindowMinutes"]                = "60",
                ["Anthropic:ApiKey"]                 = "test-key",
                ["Anthropic:Model"]                  = "claude-sonnet-4-6",
                ["Civis:JoinThreshold"]              = "0.4",
                ["Civis:TimeScoreMaxAgeHours"]       = "72",
                ["Civis:ContextEditWindowMinutes"]   = "2",
                ["Civis:RestorationRatio"]           = "0.60",
                ["Civis:MinRestorationAffectedVotes"] = "2",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace SMS provider
            ReplaceService<ISmsProvider>(services,
                ServiceLifetime.Scoped, _ => new NoOpSmsProvider());

            // Replace NLP extraction
            ReplaceService<INlpExtractionService>(services,
                ServiceLifetime.Scoped, _ => new FakeNlpExtractionService());

            // Replace geocoding
            ReplaceService<IGeocodingService>(services,
                ServiceLifetime.Scoped, _ => new FakeGeocodingService());
        });
    }

    // -------------------------------------------------------------------------
    // IAsyncLifetime — schema setup once per collection run
    // -------------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        await EnsureSchemaAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // Public helpers used by test base class
    // -------------------------------------------------------------------------

    /// <summary>
    /// Truncates all data tables, leaving the schema intact.
    /// Call from each test's InitializeAsync to get a clean slate.
    /// </summary>
    public async Task CleanTablesAsync()
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();

        // Order: most-dependent first to avoid FK violations
        string[] statements =
        {
            "TRUNCATE refresh_tokens, otp_challenges, devices, accounts CASCADE",
            "TRUNCATE participations CASCADE",
            "TRUNCATE outbox_events, civis_decisions, cluster_event_links, signal_clusters CASCADE",
            "TRUNCATE signal_events CASCADE",
            "TRUNCATE official_post_scopes, official_posts, institution_jurisdictions, institutions CASCADE",
            "TRUNCATE notifications, follows CASCADE",
        };

        foreach (var sql in statements)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // -------------------------------------------------------------------------
    // Schema bootstrap — idempotent raw SQL avoids EF migration ordering issues
    // -------------------------------------------------------------------------

    private static async Task EnsureSchemaAsync()
    {
        // Create the test DB if it doesn't exist (connect via the maintenance DB)
        await using (var maint = new NpgsqlConnection(TestConstants.MaintenanceConnectionString))
        {
            await maint.OpenAsync();
            await using var check = new NpgsqlCommand(
                "SELECT 1 FROM pg_database WHERE datname = 'hali_test'", maint);
            var exists = await check.ExecuteScalarAsync();
            if (exists == null)
            {
                await using var create = new NpgsqlCommand("CREATE DATABASE hali_test", maint);
                await create.ExecuteNonQueryAsync();
            }
        }

        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();

        // Extensions
        await ExecAsync(conn, "CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\"");
        await ExecAsync(conn, "CREATE EXTENSION IF NOT EXISTS \"pgcrypto\"");
        await ExecAsync(conn, "CREATE EXTENSION IF NOT EXISTS postgis");

        // Enums
        await ExecAsync(conn, "DO $$ BEGIN CREATE TYPE account_type AS ENUM ('citizen','institution_user','admin'); EXCEPTION WHEN duplicate_object THEN NULL; END $$");
        await ExecAsync(conn, "DO $$ BEGIN CREATE TYPE auth_method AS ENUM ('phone_otp','email_otp','magic_link','google','apple'); EXCEPTION WHEN duplicate_object THEN NULL; END $$");
        await ExecAsync(conn, "DO $$ BEGIN CREATE TYPE civic_category AS ENUM ('roads','water','electricity','transport','safety','environment','governance','infrastructure'); EXCEPTION WHEN duplicate_object THEN NULL; END $$");
        await ExecAsync(conn, "DO $$ BEGIN CREATE TYPE location_precision_type AS ENUM ('area','road','junction','landmark','facility','pin','road_landmark'); EXCEPTION WHEN duplicate_object THEN NULL; END $$");
        await ExecAsync(conn, "DO $$ BEGIN CREATE TYPE signal_state AS ENUM ('unconfirmed','active','possible_restoration','resolved','expired','suppressed'); EXCEPTION WHEN duplicate_object THEN NULL; END $$");
        await ExecAsync(conn, "DO $$ BEGIN CREATE TYPE participation_type AS ENUM ('affected','observing','no_longer_affected','restoration_yes','restoration_no','restoration_unsure'); EXCEPTION WHEN duplicate_object THEN NULL; END $$");
        await ExecAsync(conn, "DO $$ BEGIN CREATE TYPE official_post_type AS ENUM ('live_update','scheduled_disruption','advisory_public_notice'); EXCEPTION WHEN duplicate_object THEN NULL; END $$");

        // Auth tables
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS accounts (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_type account_type NOT NULL DEFAULT 'citizen',
    display_name varchar(120),
    email varchar(254),
    phone_e164 varchar(20),
    is_phone_verified boolean NOT NULL DEFAULT false,
    is_email_verified boolean NOT NULL DEFAULT false,
    status varchar(20) NOT NULL DEFAULT 'active',
    notification_settings jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_accounts_email UNIQUE (email),
    CONSTRAINT uq_accounts_phone UNIQUE (phone_e164)
)");
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS devices (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid REFERENCES accounts(id),
    device_fingerprint_hash varchar(128) NOT NULL,
    device_integrity_level varchar(30) NOT NULL DEFAULT 'unknown',
    platform varchar(30),
    app_version varchar(30),
    expo_push_token varchar(200),
    first_seen_at timestamptz NOT NULL DEFAULT now(),
    last_seen_at timestamptz NOT NULL DEFAULT now(),
    is_blocked boolean NOT NULL DEFAULT false
)");
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS otp_challenges (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid REFERENCES accounts(id),
    auth_method auth_method NOT NULL,
    destination varchar(254) NOT NULL,
    otp_hash varchar(128) NOT NULL,
    expires_at timestamptz NOT NULL,
    consumed_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now()
)");
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS refresh_tokens (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    token_hash varchar(128) NOT NULL,
    account_id uuid NOT NULL REFERENCES accounts(id),
    device_id uuid REFERENCES devices(id),
    created_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz NOT NULL,
    revoked_at timestamptz,
    CONSTRAINT uq_refresh_tokens_hash UNIQUE (token_hash)
)");
        await ExecAsync(conn, "CREATE INDEX IF NOT EXISTS ix_refresh_tokens_account ON refresh_tokens(account_id)");
        await ExecAsync(conn, "CREATE INDEX IF NOT EXISTS ix_refresh_tokens_device ON refresh_tokens(device_id)");
        await ExecAsync(conn, "CREATE INDEX IF NOT EXISTS ix_refresh_tokens_expires ON refresh_tokens(expires_at)");

        // Signals tables
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS localities (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    country_code varchar(3) NOT NULL,
    county_name varchar(100),
    city_name varchar(100),
    ward_name varchar(100) NOT NULL,
    ward_code varchar(50),
    geom geometry(MultiPolygon, 4326),
    created_at timestamptz NOT NULL DEFAULT now()
)");
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS location_labels (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    locality_id uuid REFERENCES localities(id),
    area_name varchar(200),
    road_name varchar(200),
    junction_description varchar(300),
    landmark_name varchar(200),
    facility_name varchar(200),
    location_label varchar(400) NOT NULL,
    precision_type location_precision_type NOT NULL,
    latitude double precision,
    longitude double precision,
    geom geometry(Point, 4326),
    created_at timestamptz NOT NULL DEFAULT now()
)");
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS taxonomy_categories (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    category civic_category NOT NULL,
    subcategory_slug varchar(60) NOT NULL,
    display_name varchar(120) NOT NULL,
    description text,
    is_active boolean NOT NULL DEFAULT true,
    CONSTRAINT uq_taxonomy_category_slug UNIQUE (category, subcategory_slug)
)");
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS taxonomy_conditions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    category civic_category NOT NULL,
    condition_slug varchar(60) NOT NULL,
    display_name varchar(120) NOT NULL,
    ordinal integer NOT NULL DEFAULT 0,
    is_positive boolean NOT NULL DEFAULT false,
    CONSTRAINT uq_taxonomy_condition_slug UNIQUE (category, condition_slug)
)");
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS signal_events (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid,
    device_id uuid,
    locality_id uuid REFERENCES localities(id),
    location_label_id uuid REFERENCES location_labels(id),
    category civic_category NOT NULL,
    subcategory_slug varchar(60),
    condition_slug varchar(60),
    free_text text,
    neutral_summary text,
    temporal_type varchar(30),
    latitude double precision,
    longitude double precision,
    location_confidence numeric(4,3),
    location_source varchar(20),
    condition_confidence numeric(4,3),
    occurred_at timestamptz NOT NULL DEFAULT now(),
    created_at timestamptz NOT NULL DEFAULT now(),
    source_language varchar(10),
    source_channel varchar(20),
    spatial_cell_id varchar(20),
    civis_precheck jsonb
)");
        await ExecAsync(conn, "CREATE INDEX IF NOT EXISTS ix_signal_events_locality_category_time ON signal_events(locality_id, category, occurred_at)");
        await ExecAsync(conn, "CREATE INDEX IF NOT EXISTS ix_signal_events_spatial_cell_time ON signal_events(spatial_cell_id, occurred_at)");

        // Shared outbox_events (used by both Signals and Clusters contexts)
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS outbox_events (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    aggregate_type varchar(100) NOT NULL,
    aggregate_id uuid NOT NULL,
    event_type varchar(100) NOT NULL,
    payload jsonb,
    occurred_at timestamptz NOT NULL DEFAULT now(),
    published_at timestamptz
)");
        await ExecAsync(conn, "CREATE INDEX IF NOT EXISTS ix_outbox_events_unpublished ON outbox_events(occurred_at) WHERE published_at IS NULL");

        // Clusters tables
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS signal_clusters (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    locality_id uuid,
    category civic_category NOT NULL,
    subcategory_slug varchar(80),
    dominant_condition_slug varchar(80),
    state signal_state NOT NULL DEFAULT 'unconfirmed',
    title varchar(300),
    summary text,
    location_label_id uuid,
    centroid geometry(Point, 4326),
    spatial_cell_id varchar(20),
    first_seen_at timestamptz NOT NULL DEFAULT now(),
    last_seen_at timestamptz NOT NULL DEFAULT now(),
    activated_at timestamptz,
    resolved_at timestamptz,
    possible_restoration_at timestamptz,
    civis_score numeric(8,4) DEFAULT 0,
    wrab numeric(10,4) DEFAULT 0,
    sds numeric(10,4) DEFAULT 0,
    macf numeric(10,4) DEFAULT 0,
    raw_confirmation_count integer NOT NULL DEFAULT 0,
    temporal_type varchar(40),
    affected_count integer NOT NULL DEFAULT 0,
    observing_count integer NOT NULL DEFAULT 0
)");
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS cluster_event_links (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    cluster_id uuid NOT NULL REFERENCES signal_clusters(id),
    signal_event_id uuid NOT NULL,
    link_reason varchar(50),
    linked_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_cluster_event_link UNIQUE (cluster_id, signal_event_id)
)");
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS civis_decisions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    cluster_id uuid NOT NULL REFERENCES signal_clusters(id),
    decision_type varchar(50) NOT NULL,
    reason_codes jsonb,
    metrics jsonb,
    created_at timestamptz NOT NULL DEFAULT now()
)");
        await ExecAsync(conn, "CREATE INDEX IF NOT EXISTS ix_signal_clusters_state_locality_category ON signal_clusters(state, locality_id, category)");
        await ExecAsync(conn, "CREATE INDEX IF NOT EXISTS ix_signal_clusters_last_seen ON signal_clusters(last_seen_at DESC)");
        await ExecAsync(conn, "CREATE INDEX IF NOT EXISTS ix_signal_clusters_spatial_cell_category ON signal_clusters(spatial_cell_id, category)");

        // Participation tables
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS participations (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    cluster_id uuid NOT NULL,
    account_id uuid,
    device_id uuid,
    participation_type participation_type NOT NULL,
    context_text text,
    created_at timestamptz NOT NULL DEFAULT now(),
    idempotency_key varchar(100),
    CONSTRAINT uq_participation_idempotency UNIQUE (cluster_id, device_id, participation_type, idempotency_key)
)");

        // Advisories tables
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS institutions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name varchar(200) NOT NULL,
    type varchar(50) NOT NULL,
    jurisdiction_label varchar(200),
    is_verified boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now()
)");
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS institution_jurisdictions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    institution_id uuid NOT NULL REFERENCES institutions(id),
    locality_id uuid,
    corridor_name varchar(200),
    geom geometry(MultiPolygon, 4326),
    created_at timestamptz NOT NULL DEFAULT now()
)");
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS official_posts (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    institution_id uuid NOT NULL REFERENCES institutions(id),
    author_account_id uuid,
    type official_post_type NOT NULL,
    category civic_category NOT NULL,
    title varchar(300) NOT NULL,
    body text NOT NULL,
    starts_at timestamptz,
    ends_at timestamptz,
    status varchar(20) NOT NULL DEFAULT 'draft',
    related_cluster_id uuid,
    is_restoration_claim boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
)");
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS official_post_scopes (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    official_post_id uuid NOT NULL REFERENCES official_posts(id),
    locality_id uuid,
    corridor_name varchar(200),
    geom geometry(MultiPolygon, 4326)
)");

        // Notifications tables
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS follows (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid NOT NULL,
    locality_id uuid NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_follow UNIQUE (account_id, locality_id)
)");
        await ExecAsync(conn, @"
CREATE TABLE IF NOT EXISTS notifications (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid NOT NULL,
    channel varchar(20) NOT NULL,
    notification_type varchar(50) NOT NULL,
    payload jsonb,
    send_after timestamptz NOT NULL DEFAULT now(),
    sent_at timestamptz,
    status varchar(20) NOT NULL DEFAULT 'queued',
    dedupe_key varchar(200),
    CONSTRAINT uq_notification_dedupe UNIQUE (dedupe_key)
)");
        await ExecAsync(conn, "CREATE INDEX IF NOT EXISTS ix_notifications_queued_send_after ON notifications(send_after) WHERE status = 'queued'");
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static async Task ExecAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void ReplaceService<TService>(
        IServiceCollection services,
        ServiceLifetime lifetime,
        Func<IServiceProvider, TService> factory)
        where TService : class
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(TService));
        if (descriptor != null)
            services.Remove(descriptor);

        services.Add(new ServiceDescriptor(typeof(TService), factory, lifetime));
    }
}
