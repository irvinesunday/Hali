using Hali.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Participation.Migrations
{
    /// <inheritdoc />
    public partial class UseNativeEnumForParticipationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:participation_type", "affected,no_longer_affected,observing,restoration_no,restoration_unsure,restoration_yes")
                .Annotation("Npgsql:Enum:participation_type.participation_type", "affected,observing,no_longer_affected,restoration_yes,restoration_no,restoration_unsure")
                // Schema-prefixed type already exists from Session05Reconcile
                .OldAnnotation("Npgsql:Enum:participation_type.participation_type", "affected,observing,no_longer_affected,restoration_yes,restoration_no,restoration_unsure");

            // Ensure default-schema type exists (InitialCreate may be skipped due to shared migration ID)
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
                                   WHERE t.typname = 'participation_type' AND n.nspname = 'public') THEN
                        CREATE TYPE participation_type AS ENUM (
                            'affected', 'no_longer_affected', 'observing',
                            'restoration_no', 'restoration_unsure', 'restoration_yes');
                    END IF;
                END $$;");

            // Raw SQL with USING clause — PostgreSQL cannot auto-cast integer to enum
            migrationBuilder.Sql(@"
                ALTER TABLE participations
                ALTER COLUMN participation_type TYPE participation_type
                USING (CASE participation_type
                    WHEN 0 THEN 'affected'
                    WHEN 1 THEN 'no_longer_affected'
                    WHEN 2 THEN 'observing'
                    WHEN 3 THEN 'restoration_no'
                    WHEN 4 THEN 'restoration_unsure'
                    WHEN 5 THEN 'restoration_yes'
                END)::participation_type;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:participation_type.participation_type", "affected,observing,no_longer_affected,restoration_yes,restoration_no,restoration_unsure")
                .OldAnnotation("Npgsql:Enum:participation_type", "affected,no_longer_affected,observing,restoration_no,restoration_unsure,restoration_yes")
                .OldAnnotation("Npgsql:Enum:participation_type.participation_type", "affected,observing,no_longer_affected,restoration_yes,restoration_no,restoration_unsure");

            // Raw SQL with USING clause — PostgreSQL cannot auto-cast enum to integer
            migrationBuilder.Sql(@"
                ALTER TABLE participations
                ALTER COLUMN participation_type TYPE integer
                USING (CASE participation_type
                    WHEN 'affected' THEN 0
                    WHEN 'no_longer_affected' THEN 1
                    WHEN 'observing' THEN 2
                    WHEN 'restoration_no' THEN 3
                    WHEN 'restoration_unsure' THEN 4
                    WHEN 'restoration_yes' THEN 5
                END);");
        }
    }
}
