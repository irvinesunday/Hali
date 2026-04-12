# Hali Coding Standards
# Read this file before writing any code. Run the Pre-Commit Checklist before every git commit.

---

## Pre-Commit Checklist — run every item before committing

This checklist exists because Copilot and CI have repeatedly caught issues
that should have been caught before the PR was opened. Every item below maps
to a real past failure. Do not skip any item.

### CI Failures
- [ ] If CI fails on a push you made: fix the failure in the same branch
- [ ] Note any reusable lesson internally — do NOT write to docs/arch/LESSONS_LEARNED.md
      or docs/arch/CODING_STANDARDS.md unless an explicit documentation pass is requested
- [ ] Never push a second time to fix CI without first checking if the root cause
      is covered in docs/arch/LESSONS_LEARNED.md for context

### Self-Review (run before every commit — mandatory)

- [ ] Read every file you are about to commit in full
- [ ] Run the automated grep checks (operate on staged TS/TSX files):

```bash
# Resolve the staged file list once. The [ -n "$files" ] guards below
# replace `xargs -r` (which is not portable on BSD/macOS).
files=$(git diff --name-only --cached -- '*.ts' '*.tsx')

# Hardcoded hex colours (zero tolerance). The filter accounts for the
# `lineNumber:` prefix that grep -n adds to each match.
[ -n "$files" ] && echo "$files" | xargs grep -n "#[0-9A-Fa-f]\{3,6\}" \
  | grep -v "^[^:]*:[0-9]*:[[:space:]]*//"

# Forbidden icon libraries — grep -E for portable alternation
[ -n "$files" ] && echo "$files" | xargs grep -En \
  "Ionicons|@expo/vector-icons|MaterialIcons"

# React Native Animated API (use Reanimated instead) — portable boundary
[ -n "$files" ] && echo "$files" | xargs grep -En \
  "from 'react-native'.*(^|[^[:alnum:]_])Animated([^[:alnum:]_]|$)"

# any types — portable boundary
[ -n "$files" ] && echo "$files" | xargs grep -En \
  "(:|as)[[:space:]]+any([^[:alnum:]_]|$)"
```

- [ ] Every grep above returns zero matches
- [ ] TypeScript: `npx tsc --noEmit` returns zero errors
- [ ] Every changed file has been read and manually checked against this list
- [ ] Commit message includes self-validation result: "Self-validation: PASS"
- [ ] If ANY check fails: fix it NOW — do not commit and let CI or Copilot catch it

### Formatting
- [ ] C# files: run `dotnet format --verify-no-changes` — zero violations
- [ ] TypeScript files: run `npx tsc --noEmit` — zero errors
- [ ] C# files: use spaces, not tabs; 4-space indentation throughout
- [ ] No trailing whitespace

### Contracts and types
- [ ] No `object` or `dynamic` as a parameter or return type in any controller action
      → always use a typed DTO from Hali.Contracts
- [ ] Every controller action that accepts a body has a corresponding DTO in Hali.Contracts
- [ ] Every DTO has DataAnnotations ([Required], [MaxLength], [RegularExpression]) matching the OpenAPI schema
- [ ] For local variables in controller/contract-related code, do not use `var` when the type
      is not obvious from the right-hand side — use an explicit local type where clarity matters

### Response codes and contracts
- [ ] Every [ProducesResponseType] attribute on a controller action has corresponding
      logic that can actually return that status code
      → if the code can never return 429, remove [ProducesResponseType(429)]
      → if rate limiting isn't implemented yet, remove the attribute and the doc comment
- [ ] Every route declared in the OpenAPI spec has a corresponding controller action
- [ ] Every controller action has a route declared in the OpenAPI spec
      → these two must stay in sync — the spec is the authority
- [ ] Every DTO field that maps to a `HasMaxLength(N)` EF Core column has a matching
      `[MaxLength(N)]` DataAnnotation so the API boundary enforces length before the DB does
- [ ] Idempotency key is in the request body as `idempotencyKey` — never in an
      `Idempotency-Key` header; applies to both online and offline code paths

### OpenAPI spec consistency
- [ ] After any spec change: verify the changed endpoint has a matching controller action
- [ ] After adding a controller action: verify a matching spec entry exists
- [ ] Enum values in spec match enum values in C# (case-sensitive)
- [ ] Required fields in spec match [Required] attributes in DTOs
- [ ] Example/sample values in docs and API contracts use only valid schema enum values
      (not invented synonyms — check `docs/arch/01_schema_reference.md` for the canonical set)
- [ ] Any endpoint named in a spec or arch doc that is not in `02_openapi.yaml` must be
      marked "proposed" with a note that it requires an OpenAPI + backend change
- [ ] NLP confidence values are clamped to [0.0, 0.95], not [0.0, 1.0]

### Cross-document consistency
- [ ] If a procedure is described in two places (e.g. CLAUDE.md and a skill file),
      both descriptions must say the same thing
      → if CLAUDE.md summarises a procedure, it must defer to the skill file
         not partially re-document it with different wording
- [ ] Never document a step in CLAUDE.md that is not implemented in the skill file it references
- [ ] Never reference an external or non-repo document; always link to an in-repo authority
- [ ] File references in Markdown docs must use the full repo-relative path, not just the filename
- [ ] There must be exactly one `CLAUDE.md` at the repo root; `claude.md` must not exist
- [ ] All .NET version references in docs must match the `TargetFramework` in `*.csproj` files
- [ ] Copilot/GitHub instruction files must not contradict `docs/arch/` files on security-critical topics
- [ ] Composer/input character limits must be consistent across all spec docs
      (check `04_phase1_mobile.md` against `hali_citizen_mvp_canonical_spec.md`)
- [ ] Enum values in UI specs must match the full set of values in the backend enum,
      or explicitly document which subset is exposed and how to handle the others
- [ ] Never include PR numbers as inline references in permanent documentation files
      (PR numbers belong in commit messages and changelogs, not in arch docs)
- [ ] Comments in source code that describe implementation behavior must be removed
      or updated in the same commit when the implementation changes

### React Native / TypeScript (citizen-mobile)
- [ ] No hardcoded hex colour values in new or modified code — import from the canonical theme
      module (the `src/theme` barrel; use the appropriate relative import for the file's location)
- [ ] No hardcoded font sizes, spacing numbers, or border radius values in new or modified code
      → import from the theme barrel (`src/theme`)
- [ ] No duplicate type declarations in src/types/api.ts
- [ ] No `Animated` (old API) for new animations — use `react-native-reanimated`
- [ ] All new components export from their module's barrel index file
- [ ] Every new shared component has an accessibilityLabel and accessibilityRole
- [ ] When replacing an inline element with a shared component, diff the old element's
      accessibility attributes (`accessibilityLabel`, `accessibilityState`, `accessibilityRole`,
      `accessibilityHint`, `accessibilityLiveRegion`) and ensure all are preserved
- [ ] Every `setTimeout`, `setInterval`, and Reanimated animation started in a `useEffect`
      is cancelled in the cleanup function — stale callbacks cause ghost navigation and leaks

### Security
- [ ] No secrets, tokens, or API keys in any committed file
- [ ] No .env files committed
- [ ] [AllowAnonymous] is explicit on every public endpoint — not implied by absence of [Authorize]
- [ ] GitHub Actions permissions are set at the job level, not the workflow level;
      grant only the minimum permission required per job
- [ ] Posting PR comments via `github.rest.issues.createComment` requires `issues: write`,
      not `pull-requests: write`
- [ ] Any client-supplied header value echoed in a response header or written to logs
      must be validated/sanitized against an allowlist before use (prevents header injection and log forging)

### Migrations
- [ ] Never modify an existing migration's Up() or Down() method
- [ ] Every new migration has a meaningful name (not Migration1, Migration2)
- [ ] Migration can be reversed — verify Down() is correct
- [ ] Any Down() that reverts an enum column to integer must use raw SQL with
      `USING` and `CASE` mapping — a bare `AlterColumn` will throw a type cast error
- [ ] Down() must never drop shared tables (e.g., `outbox_events`) used by other modules
- [ ] If a spec or arch doc references a DB table/column that does not exist in the
      current schema, mark it explicitly as a planned schema change before implementation

### Outbox pattern
- [ ] Every cluster state mutation writes an outbox event in the same DB transaction
- [ ] No fire-and-forget state changes — every state change has a traceable event

### Test coverage
- [ ] When a guard uses `IsNullOrWhiteSpace`, the test suite covers null, empty string,
      and whitespace inputs independently
- [ ] New behavior that changes service-layer method signatures or semantics has
      corresponding unit tests — not just integration tests

---

## C# conventions

### Naming
- Controllers: `{Resource}Controller` — PascalCase
- DTOs: `{Action}{Resource}Request` / `{Resource}Response` in Hali.Contracts
- Services: `I{Resource}Service` interface + `{Resource}Service` implementation
- Never use abbreviations in public API surface names

### Controller rules
- Controllers are thin — no business logic
- Prefer DataAnnotations + ModelState for request DTO/body validation; manual checks are
  acceptable for query parameters and derived/cross-field constraints
- Return types: IActionResult for flexibility, not specific types
- Always use StatusCodes constants: `StatusCodes.Status200OK` not `200`

### Service rules
- Services are pure domain logic — no HTTP concerns
- Services return domain results, not HTTP responses
- Every service method that mutates state writes an audit log entry

### Exception handling
- Middleware and service-layer catch-all blocks must handle
  `OperationCanceledException` before the general `Exception` catch.
  In middleware: `catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)`
  — return without writing a response or logging an error.
  In services: `catch (Exception ex) when (ex is not OperationCanceledException)`.
  Client disconnects are not server errors.

### Logging safety
- Never pass raw request-derived values (`Request.Path`, `Request.Method`,
  `Request.QueryString`, raw header values) into structured log templates.
  Use `context.GetEndpoint()?.DisplayName` for route identification.
  For any user-supplied value that must be logged, use
  `ObservabilityEvents.SanitizeForLog(value)`.
- The existing header-sanitization rule (Security section) covers response
  headers; this rule covers the structured-logging path explicitly.

### Nullable reference types
- All new C# files have nullable enabled (#nullable enable or via csproj)
- Use `??` and `?.` where appropriate; prefer `is null` / `is not null` for explicit null
  checks in new C# code (over `== null` / `!= null`)

### Resource disposal
- Always wrap `HttpResponseMessage` in a `using` statement to ensure the response and
  its content stream are disposed after reading
- Any `IDisposable`/`IAsyncDisposable` infrastructure object (NpgsqlDataSource, HttpClient, etc.)
  must be registered in the DI container — not captured in a closure — to ensure proper disposal

### XML documentation
- `cref` attributes must use fully qualified type names
  (`<exception cref="Hali.Application.Errors.ValidationException">`),
  not short names that may not resolve in the target namespace (CS1574).
- When a service implementation changes which exception it throws, update the
  interface's XML `<exception>` doc in the same commit.

### String formatting
- Always use `CultureInfo.InvariantCulture` when converting numeric types to strings for
  external system consumption (API payloads, NLP prompts, log messages)
- Never use `System.Web.HttpUtility` — use `Uri.EscapeDataString` or `System.Net.WebUtility`
  for URL encoding on net10.0

### Collection operations
- When deduplicating with `GroupBy`, use a predicate that preserves the most data
  (non-null fields) rather than always picking `First()`
  → prefer: `g.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Field)) ?? g.First()`

### EF Core
- `DbContext` is not thread-safe. Never wrap multiple queries in `Task.WhenAll`
  on a shared context. Use sequential queries on a single context, or resolve
  separate scoped contexts via `IServiceScopeFactory` for genuinely parallel work.
- When a `dotnet build` step precedes a `dotnet ef` step in the same CI job,
  always pass `--no-build` to the EF command to avoid redundant builds

---

## TypeScript / React Native conventions

### Component rules
- Presentation components: no data fetching, no business logic
- Never use `any` — use `unknown` and narrow, or define a proper type
- Props interfaces are defined inline in the component file
- Every component file exports exactly one component (default or named, be consistent)

### Import order (enforce consistently)
1. React
2. React Native core
3. Third-party libraries (expo-router, lucide-react-native, reanimated)
4. Internal: types
5. Internal: theme
6. Internal: other components
7. Internal: utils/hooks/api

### State management
- Server state: TanStack Query
- Auth + session: AuthContext / Zustand
- Composer draft: Zustand
- Never use AsyncStorage for sensitive data — use SecureStore

---

## What never to do (hard rules)

- Never use `object` as a controller action parameter type
- Never advertise a response code ([ProducesResponseType]) without logic to produce it
- Never add a route to the OpenAPI spec without a corresponding controller action
- Never hardcode hex colours or spacing in new or modified React Native code
  (existing legacy values may be migrated incrementally)
- Never modify existing EF Core migration Up()/Down() content
- Never commit .env files
- Never push directly to `main`
- Never merge a PR with failing CI checks
- Never use `System.Web.HttpUtility` for URL encoding — use `Uri.EscapeDataString`
- Never echo an unvalidated client-supplied header value into a response header or log
- Never grant GitHub Actions permissions at the workflow level — use job-level permissions
- Never use `--idempotent` with `dotnet ef database update` — it is not a valid flag for that command
- Never hardcode a count of CI jobs in documentation; use "all required CI jobs" instead
- Never reference external (non-repo) documents in architecture docs; link to in-repo files only
- Never include PR numbers as inline references in permanent documentation (use timeless references)
- Never hardcode a branch name (`develop`, `main`) in git remediation steps — reference
  "the PR's actual base branch" instead
- Never use a context "loaded" latch (e.g. `followsLoaded`) to gate readiness in a component that
  spans auth transitions; derive readiness from React Query `isSuccess`/`isError` instead so the
  flag resets when auth state changes

## OpenAPI rules

- In OpenAPI 3.1, always list fields the backend unconditionally serialises in `required`, even
  when they are `nullable: true`. `required` means present-on-wire, not non-null.

## TypeScript API rules

- When an API function returns different types based on a discriminant argument (e.g. section name),
  use conditional types or overloads to infer the correct return type — never rely on generic
  defaults (`T = SomeType`) that the caller might forget to override.

## Enum serialization rules

- Never use `.ToString().ToLowerInvariant()` on multi-word PascalCase enums for wire output — it
  produces `possiblerestoration` instead of `possible_restoration`. Use a PascalCase→snake_case
  helper (see `ClusteringService.ToSnakeCase`, `ClustersController.ToSnakeCase`)
- Wire-format enum values must match the OpenAPI `enum` array exactly (e.g. `possible_restoration`)

## GitHub Actions rules

- [ ] All CI workflows reference the .NET SDK version via a shared `env.DOTNET_VERSION` variable
- [ ] Any CI workflow using a .NET global tool includes an explicit `dotnet tool install`
      or `dotnet tool restore` step before invoking the tool
- [ ] PR description is updated to reflect the actual diff scope before review is requested
- [ ] Shell commands in Markdown fenced code blocks use literal (unescaped) shell syntax

---

## PR Scope Discipline

### Scope purity
- PRs must remain strictly scoped to the requested task
- No unrelated refactors, opportunistic improvements, or formatting churn
- No mixed concerns in a single PR
- If a separate issue is discovered during implementation, note it — do not fix it in the same PR

### Documentation hygiene
During implementation, maintenance passes, or PR review resolution, do NOT modify
documentation files unless the task explicitly requests documentation changes.

Protected files (modify only in a dedicated documentation pass):
- `docs/arch/CODING_STANDARDS.md`
- `docs/arch/LESSONS_LEARNED.md`
- Architecture docs (`docs/arch/*.md`)
- Process docs, workflow docs, runbooks

### Lessons and reusable patterns
If a reusable lesson or pattern is identified during feature work or review resolution:
- Note it internally for future reference
- Do NOT auto-write it to docs/arch/LESSONS_LEARNED.md or docs/arch/CODING_STANDARDS.md
- Wait for an explicit documentation pass instruction from the user
- If needed, include a brief note in the PR description instead of modifying documentation files

### Review resolution discipline
When resolving Copilot or PR review comments:
- Apply the smallest correct code fix
- Do not broaden scope beyond the comment
- Do not add documentation changes unless explicitly requested
- Decline incorrect comments with an explanation — do not force bad changes
