using System;
using Hali.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Hali.Infrastructure.Data.Advisories.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvisoriesSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:civic_category", "electricity,environment,governance,infrastructure,roads,safety,transport,water")
                .Annotation("Npgsql:Enum:civic_category.civic_category", "roads,water,electricity,transport,safety,environment,governance,infrastructure")
                .Annotation("Npgsql:Enum:official_post_type", "advisory_public_notice,live_update,scheduled_disruption")
                .Annotation("Npgsql:Enum:official_post_type.official_post_type", "live_update,scheduled_disruption,advisory_public_notice")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                // civic_category (both schemas), official_post_type (default), and postgis
                // already created by Signals/Auth/Advisories-InitialCreate contexts
                .OldAnnotation("Npgsql:Enum:civic_category", "electricity,environment,governance,infrastructure,roads,safety,transport,water")
                .OldAnnotation("Npgsql:Enum:civic_category.civic_category", "roads,water,electricity,transport,safety,environment,governance,infrastructure")
                .OldAnnotation("Npgsql:Enum:official_post_type", "advisory_public_notice,live_update,scheduled_disruption")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            // Ensure shared types exist (InitialCreate may be skipped due to shared migration ID)
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
                                   WHERE t.typname = 'civic_category' AND n.nspname = 'public') THEN
                        CREATE TYPE civic_category AS ENUM (
                            'electricity', 'environment', 'governance', 'infrastructure',
                            'roads', 'safety', 'transport', 'water');
                    END IF;
                END $$;");
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
                                   WHERE t.typname = 'official_post_type' AND n.nspname = 'public') THEN
                        CREATE TYPE official_post_type AS ENUM (
                            'advisory_public_notice', 'live_update', 'scheduled_disruption');
                    END IF;
                END $$;");

            migrationBuilder.CreateTable(
                name: "institution_jurisdictions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locality_id = table.Column<Guid>(type: "uuid", nullable: true),
                    corridor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    geom = table.Column<Geometry>(type: "geometry(MultiPolygon, 4326)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institution_jurisdictions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "institutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    jurisdiction_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "official_post_scopes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    official_post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locality_id = table.Column<Guid>(type: "uuid", nullable: true),
                    corridor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    geom = table.Column<Geometry>(type: "geometry(MultiPolygon, 4326)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_official_post_scopes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "official_posts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<OfficialPostType>(type: "official_post_type", nullable: false),
                    category = table.Column<CivicCategory>(type: "civic_category", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    related_cluster_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_restoration_claim = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_official_posts", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "institution_jurisdictions");

            migrationBuilder.DropTable(
                name: "institutions");

            migrationBuilder.DropTable(
                name: "official_post_scopes");

            migrationBuilder.DropTable(
                name: "official_posts");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:Enum:civic_category", "electricity,environment,governance,infrastructure,roads,safety,transport,water")
                .OldAnnotation("Npgsql:Enum:civic_category.civic_category", "roads,water,electricity,transport,safety,environment,governance,infrastructure")
                .OldAnnotation("Npgsql:Enum:official_post_type", "advisory_public_notice,live_update,scheduled_disruption")
                .OldAnnotation("Npgsql:Enum:official_post_type.official_post_type", "live_update,scheduled_disruption,advisory_public_notice")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
