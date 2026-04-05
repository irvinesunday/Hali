# Self-Healing Skill — CI and Deployment Fixes

Read this file whenever you are working on a CI pipeline, deployment workflow,
database migration, Docker build, or any task where you push code and check a result.

## Core rule

Never stop and report a failure to Irvine if the failure is fixable in code.
Only stop when:
- A GitHub Actions secret needs to be set or corrected manually
- A Neon/cloud infrastructure action is required
- An external API credential needs to be provisioned
- You have iterated 5+ times and cannot determine the root cause from logs

Everything else — wrong flags, wrong order, missing tools, missing build steps,
wrong connection string format, missing migrations — you fix yourself.

## The loop you must follow for every CI/deploy task

1. Make your fix
2. Commit and push to the target branch
3. Get the latest run ID:
   ```bash
   gh run list --repo irvinesunday/Hali --branch <branch> --limit 1 \
     --json databaseId,status,conclusion,workflowName
   ```
4. Watch it complete:
   ```bash
   gh run watch <RUN_ID> --repo irvinesunday/Hali
   ```
5. If it passes — post a success comment on the PR and stop
6. If it fails — fetch the full logs:
   ```bash
   gh run view <RUN_ID> --repo irvinesunday/Hali --log-failed
   ```
7. Read the FULL log, not just the last line
8. Identify root cause, apply fix from the table below
9. Go back to step 1

Never ask Irvine for the error. Never wait for Irvine to paste logs.
You have gh CLI access — use it every time.

## Fix table

| Error | Root cause | Fix |
|---|---|---|
| `deps.json does not exist` | dotnet ef ran before build | Add explicit `dotnet build` before all `dotnet ef` calls. Remove `--no-build`. |
| `project.assets.json not found` | dotnet ef ran before restore | Add `dotnet restore` before `dotnet build`. Check project path is correct. |
| `tool not found: dotnet-ef` | PATH not persisted across steps | Use `echo "$HOME/.dotnet/tools" >> $GITHUB_PATH` — NOT `export PATH=...` |
| `ConnectionString not initialized` | Secret is empty string | STOP — secret not set in GitHub Actions. Report exact secret name to Irvine. |
| `relation already exists` | Tables exist but no migration history | Add `--idempotent` flag to all `dotnet ef database update` calls. |
| `PendingModelChangesWarning` | Model changed without migration | Run `dotnet ef migrations add <name> --context <ctx>`, commit, retry. |
| `context was not found` | Wrong DbContext class name | Run `grep -rn "class.*DbContext" src/Hali.Infrastructure/ --include="*.cs"` and use exact names. |
| `psql: invalid connection option "Host"` | psql used with Npgsql connection string | Remove all psql. Use `dotnet ef database update` directly. |
| `exit code 1` with no clear message | Silent build failure | Run `dotnet build` explicitly and read compiler errors. |
| `Coverage gate failed` | Coverage dropped | Read uncovered files, add targeted tests. Never add empty tests. |
| `Merge conflict` | Branch behind base | Run `git merge origin/develop`, resolve, push. |
| `PR body empty` | Template fields not populated | Construct full body inline. Never use `--body-file` on raw template. |
| `Docker build failed` | Missing COPY line for .csproj | Run `grep -rn "ProjectReference" src/Hali.Api/Hali.Api.csproj` and add missing COPY lines. |

## Step order for .NET CI jobs — always follow this sequence

```
actions/setup-dotnet@v4  (dotnet 9.0.x)
dotnet tool install --global dotnet-ef
echo "$HOME/.dotnet/tools" >> $GITHUB_PATH
dotnet restore <startup-project>
dotnet build <startup-project> --no-restore
dotnet ef database update --context <ctx> --connection "$CONN"
```

Never skip steps. Never reorder. Never use `--no-build` unless the build
step ran in the same job earlier.

## What counts as a blocker requiring Irvine

Stop and post a comment only if:
- A GitHub Actions secret is missing or wrong
- A Neon infrastructure action is needed (schema reset, permissions, new DB)
- An external credential needs provisioning (Africa's Talking, Anthropic, Expo)
- You have genuinely tried 5+ different targeted fixes with no progress

When you stop, post on the relevant PR:
- Exact error text
- Every fix you attempted (list of commits and what each one changed)
- Exactly what external action is needed with precise step-by-step instructions

## Rules that never change

- Never modify existing EF Core migration `Up()` or `Down()` methods
- Never hardcode secrets or connection strings in any file
- Never add trivial tests purely to raise coverage numbers
- Never suppress warnings with pragma/noqa without a documented reason
- Never merge a PR — only fix and push to the branch
- Always use `echo "$HOME/.dotnet/tools" >> $GITHUB_PATH` for dotnet tool PATH
- Always run restore → build → ef update in that exact order
