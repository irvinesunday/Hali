## Version: 1.0
## Last updated: 2026-03-30

# Session 02 — Auth (Phase 3)
# Prerequisite: Session 01 complete and committed.

## Context
Phases 1 and 2 are complete. The solution builds. EF Core migrations cover all 21 tables.

## Your task this session
Build Phase 3 (Auth) only. Do not begin Phase 4.

### What to build
- ISmsProvider interface + Africa's Talking implementation
- OTP generation, hashing, storage in otp_challenges table
- OTP verification and consumption (mark consumed_at)
- JWT access token issuance (60-minute expiry, HS256)
- Refresh token issuance: hashed, stored in refresh_tokens table, 30-day expiry
- POST /v1/auth/otp endpoint
- POST /v1/auth/verify endpoint (returns access_token + refresh_token)
- POST /v1/auth/refresh endpoint (validates hash, issues new token pair)
- POST /v1/auth/logout endpoint (revokes refresh token by setting revoked_at)
- Device registration: upsert into devices table on every auth verify
- Rate limiting on OTP requests: rl:otp:{destination} in Redis

### Key rules from CLAUDE.md Auth Rules
- Refresh tokens stored as SHA-256 hashes, never plaintext
- Refresh token rotation preferred (issue new token on every refresh)
- Single active token per device if rotation is complex
- Read REFRESH_TOKEN_EXPIRY_DAYS and JWT_EXPIRY_MINUTES from configuration

### Interfaces to implement
- ISmsProvider (Africa's Talking behind abstraction)

### Tests required
- OTP generation and expiry
- OTP verify happy path and rejection cases
- Refresh token issuance, rotation, and revocation
- Rate limit enforcement

## Done when
- All auth endpoints return correct responses
- Refresh token round-trip works (verify → refresh → refresh again)
- Unit tests pass
- Output: SESSION_02_COMPLETE

---

## Universal session rules
Read session_prompts/_universal_rules.md before writing any code.
These rules apply to every session: checkpoint commits, amnesia recovery,
prompt versioning, and context window management.

---
