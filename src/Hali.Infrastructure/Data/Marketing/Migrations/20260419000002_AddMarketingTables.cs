using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Marketing.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS early_access_signups (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    email varchar(254) NOT NULL,
    submitted_at timestamptz NOT NULL
)");
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_early_access_signups_email
    ON early_access_signups(email)");
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_early_access_signups_submitted_at
    ON early_access_signups(submitted_at)");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS institution_inquiries (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name varchar(120) NOT NULL,
    organisation varchar(200) NOT NULL,
    role varchar(120) NOT NULL,
    email varchar(254) NOT NULL,
    area varchar(200) NOT NULL,
    category varchar(50) NOT NULL,
    message varchar(500),
    submitted_at timestamptz NOT NULL
)");
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_institution_inquiries_email
    ON institution_inquiries(email)");
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_institution_inquiries_submitted_at
    ON institution_inquiries(submitted_at)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS institution_inquiries");
            migrationBuilder.Sql("DROP TABLE IF EXISTS early_access_signups");
        }
    }
}
