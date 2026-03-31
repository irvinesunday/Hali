# =============================================================================
# Hali MVP — Staging Environment Configuration
# Used by the deploy.yml staging job.
# DO NOT commit real values — all secrets are stored in GitHub Environments.
# This file documents which secrets are required for staging.
# =============================================================================

# These variables must be set as GitHub Environment Secrets
# in: Settings → Environments → staging → Environment secrets

# Database (separate staging DB — never share with production)
# STAGING_DATABASE_URL=Host=staging-db.hali.app;Port=5432;Database=hali_staging;...
# STAGING_DB_PASSWORD=<staging-only password>

# Redis (separate staging Redis)
# STAGING_REDIS_URL=staging-redis.hali.app:6379

# Auth
# JWT_SECRET=<staging-specific secret — different from production>

# External APIs (use sandbox/test credentials for staging)
# ANTHROPIC_API_KEY=<same API key is fine — costs are low>
# AFRICASTALKING_USERNAME=sandbox  ← always use sandbox for staging
# AFRICASTALKING_API_KEY=<Africa's Talking sandbox key>
# EXPO_ACCESS_TOKEN=<staging Expo token>

# Observability (can point to same Sentry project, different environment tag)
# SENTRY_DSN=<same DSN, staging environment tag>

# Staging-specific settings
# ASPNETCORE_ENVIRONMENT=Staging
# HALI_APP_BASE_URL=https://staging.api.hali.app

# =============================================================================
# Environment separation rules
# =============================================================================
# 1. staging DB and production DB are NEVER the same instance
# 2. staging uses Africa's Talking sandbox (no real SMS sent)
# 3. JWT_SECRET is different in every environment
# 4. ANTHROPIC_API_KEY can be shared (API calls are metered, not environment-specific)
# 5. staging deploys happen automatically on develop merge — no approval needed
# 6. production deploys require your manual approval via GitHub Environments
