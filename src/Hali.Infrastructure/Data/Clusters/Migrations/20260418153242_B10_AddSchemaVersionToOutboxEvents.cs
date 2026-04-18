using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Clusters.Migrations
{
    /// <inheritdoc />
    public partial class B10_AddSchemaVersionToOutboxEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "schema_version",
                table: "outbox_events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "1.0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "schema_version",
                table: "outbox_events");
        }
    }
}
