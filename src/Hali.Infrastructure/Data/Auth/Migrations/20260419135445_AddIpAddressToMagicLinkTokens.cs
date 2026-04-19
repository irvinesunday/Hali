using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Auth.Migrations
{
    /// <inheritdoc />
    public partial class AddIpAddressToMagicLinkTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE magic_link_tokens
                ADD COLUMN IF NOT EXISTS ip_address character varying(45);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE magic_link_tokens
                DROP COLUMN IF EXISTS ip_address;
                """);
        }
    }
}
