using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Migrations.Advisories
{
    /// <inheritdoc />
    [Migration("20260401000005_AdvisoriesInitial")]
    public partial class AdvisoriesInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "institutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "varchar(160)", nullable: false),
                    institution_type = table.Column<string>(type: "varchar(60)", nullable: false),
                    jurisdiction_label = table.Column<string>(type: "varchar(160)", nullable: true),
                    is_verified = table.Column<bool>(nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "institution_jurisdictions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locality_id = table.Column<Guid>(type: "uuid", nullable: true),
                    corridor_name = table.Column<string>(type: "varchar(160)", nullable: true),
                    // geometry(MultiPolygon,4326) — handled via Npgsql.NetTopologySuite
                    geom = table.Column<object>(type: "geometry(MultiPolygon,4326)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institution_jurisdictions", x => x.id);
                    table.ForeignKey(
                        name: "FK_institution_jurisdictions_institutions",
                        column: x => x.institution_id,
                        principalTable: "institutions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_institution_jurisdictions_localities",
                        column: x => x.locality_id,
                        principalTable: "localities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            // GiST index on institution_jurisdictions.geom (schema_patch_notes §12)
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_institution_jurisdictions_geom ON institution_jurisdictions USING GIST(geom);");

            migrationBuilder.CreateTable(
                name: "official_posts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    official_post_type = table.Column<string>(type: "official_post_type", nullable: false),
                    category = table.Column<string>(type: "civic_category", nullable: true),
                    title = table.Column<string>(type: "varchar(220)", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    status = table.Column<string>(type: "varchar(30)", nullable: false, defaultValue: "published"),
                    related_cluster_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_official_posts", x => x.id);
                    table.ForeignKey(
                        name: "FK_official_posts_institutions",
                        column: x => x.institution_id,
                        principalTable: "institutions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_official_posts_accounts",
                        column: x => x.author_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_official_posts_clusters",
                        column: x => x.related_cluster_id,
                        principalTable: "signal_clusters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "official_post_scopes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    official_post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locality_id = table.Column<Guid>(type: "uuid", nullable: true),
                    corridor_name = table.Column<string>(type: "varchar(160)", nullable: true),
                    // geometry(MultiPolygon,4326) — handled via Npgsql.NetTopologySuite
                    geom = table.Column<object>(type: "geometry(MultiPolygon,4326)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_official_post_scopes", x => x.id);
                    table.ForeignKey(
                        name: "FK_official_post_scopes_posts",
                        column: x => x.official_post_id,
                        principalTable: "official_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_official_post_scopes_localities",
                        column: x => x.locality_id,
                        principalTable: "localities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            // GiST index on official_post_scopes.geom — for geo-scope enforcement (schema_patch_notes §12)
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_official_post_scopes_geom ON official_post_scopes USING GIST(geom);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "official_post_scopes");
            migrationBuilder.DropTable(name: "official_posts");
            migrationBuilder.DropTable(name: "institution_jurisdictions");
            migrationBuilder.DropTable(name: "institutions");
        }
    }
}
