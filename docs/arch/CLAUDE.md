## Self-Healing Requirement (MANDATORY)

For any task involving CI pipelines, deployment workflows, database migrations,
Docker builds, or test failures:

1. Read `docs/arch/SELF_HEALING_SKILL.md` before starting
2. After every fix, watch the CI run yourself using `gh run watch`
3. Read failure logs yourself using `gh run view --log-failed`
4. Apply the next fix from the fix table in the skill file
5. Iterate until the job passes or you hit a genuine external blocker

Never stop and report a code-level failure to Irvine.
Never wait for Irvine to paste error logs.
You have gh CLI access. Use it every time without being asked.

Only defer to Irvine when the fix requires action outside the codebase:
missing GitHub secret, Neon infrastructure change, or external API credentials.
