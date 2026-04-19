# Hali repo-level convenience targets. These do not replace the canonical
# workflows (dotnet, pnpm) — they provide one-shot aliases for the few
# multi-step flows that are easier to memorise than retype.

.PHONY: validate-loop validate-loop-dry test test-unit

# Walks the full civic loop end-to-end against a running stack. Requires
# the API at $$HALI_API_BASE (default http://localhost:8080), a
# citizen JWT in $$HALI_CITIZEN_JWT, an institution JWT in
# $$HALI_INSTITUTION_JWT, and a locality id inside the institution scope
# in $$HALI_TEST_LOCALITY. The loop is considered proven only when this
# target exits 0 — see #207 Phase 4 completion criteria.
validate-loop:
	python3 scripts/validate_civic_loop.py

# Config-resolution + health-check only. Useful for CI to verify the
# harness imports cleanly without requiring a full Postgres/Redis/API
# stack to be up.
validate-loop-dry:
	python3 scripts/validate_civic_loop.py --dry-run

# Re-runs the unit test suite from the repo root. The default
# `dotnet test` picks up the integration project too, which requires
# local Postgres/Redis; this alias is the faster default.
test-unit:
	dotnet test tests/Hali.Tests.Unit/Hali.Tests.Unit.csproj

test: test-unit
