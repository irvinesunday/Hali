# Hali Coding Standards
# Read this file before writing any code. Run the Pre-Commit Checklist before every git commit.

---

## Pre-Commit Checklist — run every item before committing

This checklist exists because Copilot and CI have repeatedly caught issues
that should have been caught before the PR was opened. Every item below maps
to a real past failure. Do not skip any item.

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

### OpenAPI spec consistency
- [ ] After any spec change: verify the changed endpoint has a matching controller action
- [ ] After adding a controller action: verify a matching spec entry exists
- [ ] Enum values in spec match enum values in C# (case-sensitive)
- [ ] Required fields in spec match [Required] attributes in DTOs

### Cross-document consistency
- [ ] If a procedure is described in two places (e.g. CLAUDE.md and a skill file),
      both descriptions must say the same thing
      → if CLAUDE.md summarises a procedure, it must defer to the skill file
         not partially re-document it with different wording
- [ ] Never document a step in CLAUDE.md that is not implemented in the skill file it references

### React Native / TypeScript (citizen-mobile)
- [ ] No hardcoded hex colour values in new or modified code — import from the canonical theme
      module (the `src/theme` barrel; use the appropriate relative import for the file's location)
- [ ] No hardcoded font sizes, spacing numbers, or border radius values in new or modified code
      → import from the theme barrel (`src/theme`)
- [ ] No duplicate type declarations in src/types/api.ts
- [ ] No `Animated` (old API) for new animations — use `react-native-reanimated`
- [ ] All new components export from their module's barrel index file
- [ ] Every new shared component has an accessibilityLabel and accessibilityRole

### Security
- [ ] No secrets, tokens, or API keys in any committed file
- [ ] No .env files committed
- [ ] [AllowAnonymous] is explicit on every public endpoint — not implied by absence of [Authorize]

### Migrations
- [ ] Never modify an existing migration's Up() or Down() method
- [ ] Every new migration has a meaningful name (not Migration1, Migration2)
- [ ] Migration can be reversed — verify Down() is correct

### Outbox pattern
- [ ] Every cluster state mutation writes an outbox event in the same DB transaction
- [ ] No fire-and-forget state changes — every state change has a traceable event

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

### Nullable reference types
- All new C# files have nullable enabled (#nullable enable or via csproj)
- Use `??` and `?.` where appropriate; prefer `is null` / `is not null` for explicit null
  checks in new C# code (over `== null` / `!= null`)

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
