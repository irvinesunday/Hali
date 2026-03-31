# Hali — Database Migration Rollback Runbook

**Use this document when a production deployment fails due to a migration error.**

---

## Guiding principles

1. Migrations run BEFORE the new API image is deployed (see `deploy.yml`)
2. All migrations must be backward-compatible with the prior API version
3. Never delete columns in the same migration that stops using them — use two releases
4. Test rollback on staging before every production deployment

---

## Step 1 — Identify the failing migration

```bash
# On the production database host, check which migration failed:
PGPASSWORD=$PROD_DB_PASSWORD psql $PRODUCTION_DATABASE_URL \
  -c "SELECT migration_id, product_version, applied_at FROM __ef_migrations_history ORDER BY applied_at DESC LIMIT 10;"
```

```bash
# In the EF Core migration history:
dotnet ef migrations list \
  --project src/Hali.Infrastructure \
  --startup-project src/Hali.Api \
  --connection "$PRODUCTION_DATABASE_URL"
```

---

## Step 2 — Roll back the API image (do this first)

Roll back to the previous container image BEFORE reverting the migration,
so that the running API is compatible with whatever state the DB is in.

```bash
# If using Railway / Render / Fly.io — redeploy the previous release
# If using Kubernetes:
kubectl rollout undo deployment/hali-api
kubectl rollout undo deployment/hali-worker

# Verify the rollback:
curl https://api.hali.app/health
```

---

## Step 3 — Revert the migration

```bash
# List available migrations to find the previous stable one:
dotnet ef migrations list \
  --project src/Hali.Infrastructure \
  --startup-project src/Hali.Api

# Roll back to the last known-good migration by name:
dotnet ef database update <PreviousMigrationName> \
  --project src/Hali.Infrastructure \
  --startup-project src/Hali.Api \
  --connection "$PRODUCTION_DATABASE_URL"
```

**Important:** EF Core rollback only works if the migration's `Down()` method is implemented.
Always implement `Down()` — never leave it empty.

---

## Step 4 — Verify the database state

```bash
# Check table counts are reasonable:
PGPASSWORD=$PROD_DB_PASSWORD psql $PRODUCTION_DATABASE_URL -c "
  SELECT 'accounts' as t, COUNT(*) FROM accounts
  UNION ALL SELECT 'signal_events', COUNT(*) FROM signal_events
  UNION ALL SELECT 'signal_clusters', COUNT(*) FROM signal_clusters;
"

# Check last migration applied:
PGPASSWORD=$PROD_DB_PASSWORD psql $PRODUCTION_DATABASE_URL \
  -c "SELECT * FROM __ef_migrations_history ORDER BY applied_at DESC LIMIT 3;"
```

---

## Step 5 — Fix the migration and re-deploy

```bash
# In the failing migration file, fix the issue:
# - Ensure Down() is the exact inverse of Up()
# - Ensure the migration is backward-compatible with the prior API version
# - Test against a staging DB restore first

# Create a corrected migration if necessary:
dotnet ef migrations add FixMigrationName \
  --project src/Hali.Infrastructure \
  --startup-project src/Hali.Api

# Apply and test on staging:
dotnet ef database update \
  --project src/Hali.Infrastructure \
  --startup-project src/Hali.Api \
  --connection "$STAGING_DATABASE_URL"

# Only then merge and trigger production deployment
```

---

## Migration safety rules (for Claude Code — must be in every migration)

| Rule | Why |
|------|-----|
| Never remove a column that the current API reads | Two-release strategy: stop reading first, then remove |
| Always implement `Down()` | Rollback without `Down()` requires manual SQL |
| New NOT NULL columns must have a DEFAULT | Otherwise migration fails on existing rows |
| Enum additions use `ALTER TYPE ADD VALUE` | Cannot be rolled back without dropping the type |
| Test `Down()` on a staging DB restore before every production deploy | Untested rollbacks always fail when needed |

---

## GiST index failure recovery

If a GiST index creation fails mid-migration:

```sql
-- Check for invalid indexes:
SELECT indexname, indisvalid 
FROM pg_indexes 
JOIN pg_index ON indexrelid = (SELECT oid FROM pg_class WHERE relname = indexname)
WHERE schemaname = 'public' AND NOT indisvalid;

-- Drop the invalid index and recreate CONCURRENTLY:
DROP INDEX CONCURRENTLY IF EXISTS ix_signal_clusters_centroid;
CREATE INDEX CONCURRENTLY ix_signal_clusters_centroid 
  ON signal_clusters USING gist(centroid);
```

---

## Contact and escalation

If rollback fails or data loss is suspected:
1. Take a manual database snapshot immediately
2. Stop all write traffic to the API (set maintenance mode)
3. Review the most recent backup (see backup schedule)
4. Do not attempt further migrations until the root cause is understood
