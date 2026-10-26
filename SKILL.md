Configure existing Codex agents for a reliable, token-efficient development workflow within one CLI session.
Inspect the installed Codex version, existing configuration, agent definitions, MCP configuration, and applicable AGENTS.md instructions first. Use current official documentation to verify supported configuration syntax.
Apply the changes rather than only proposing them. Preserve unrelated configuration and existing useful instructions. Prefer project-scoped settings; avoid changing global settings unnecessarily. Do not create duplicate agents or MCP entries.

## AGENTS AND MODELS

Keep these three agents:
- techlead: GPT-5.6 Sol, medium reasoning.
- dev: GPT-5.6 Sol, medium reasoning.
- qa: GPT-5.6 Luna, high reasoning.

Verify the exact model IDs and supported reasoning settings using existing working configuration and available model information. Preserve working model IDs. Do not guess IDs, upgrade model families, or silently substitute another model. If something is unsupported, report the specific blocker and complete the independent configuration work.

## RESPONSIBILITIES

### Techlead
- Coordinate requirements, implementation, QA, and final delivery.
- Inspect relevant architecture and define clear acceptance criteria.
- Give bounded assignments with context, constraints, file ownership, dependencies, and expected output.
- Keep planning proportional to complexity.
- Resolve disagreements using code, documentation, and test evidence.
- Review integration and report completion with validation evidence.
- Avoid editing files currently assigned to dev or QA.

### Dev
- Implement focused changes following the project’s existing conventions.
- Avoid unrelated refactoring and unnecessary dependencies.
- Write meaningful tests appropriate to the change.
- Run relevant builds, tests, and checks before handing work to QA.
- Report changes, checks performed, failures, and remaining limitations.
- Fix confirmed QA findings.

### QA
- Independently verify the acceptance criteria and user-visible behavior.
- Run relevant tests and use browser tooling when appropriate.
- Check regressions, failure paths, permissions, validation, and edge cases.
- For ASP.NET Core work, pay attention to server-side authorization, EF Core behavior, duplicate submissions, and failed requests.
- Report actionable findings with severity, reproduction steps, expected behavior, actual behavior, and relevant file references.
- Never claim a check passed unless it was actually executed.
- Do not silently fix production code. Return production fixes to dev.
- Write or modify tests only when assigned ownership of those files.
- Retest affected behavior after fixes.

## ORCHESTRATION

Use the main CLI session as the coordinator. Avoid creating a separate coordinator agent that duplicates the techlead’s work. Use the existing techlead definition wherever the installed CLI supports it; otherwise preserve it as a delegated specialist and document how the main session coordinates it.

Choose the workflow based on task complexity:
- Small, low-risk task: dev implements and validates.
- Normal feature: brief techlead assignment → dev implementation → QA verification.
- Complex feature: techlead analysis → scoped implementation and independent parallel work → QA verification → techlead integration review.

Use parallel subagents when the work is independent and the expected benefit justifies the additional usage:
- Dev can implement while QA prepares test cases or reproduces baseline behavior.
- Techlead can investigate an independent integration concern while dev implements.
- QA’s final verification must wait for the implementation being verified.
- Do not force parallel execution for dependent tasks.
- Do not spawn additional nested agents by default.

## CONFLICT PREVENTION

- Assign explicit file ownership before concurrent edits.
- Avoid simultaneous edits to the same files.
- Do not overwrite or discard user changes or another agent’s work.
- Use separate worktrees only when independent writes or stable test environments require them.
- When running separate app instances, use distinct ports and isolated test data where needed.
- Coordinate builds, generated files, migrations, and shared browser state.
- Use development/test environments for tests that modify data.

## MCP SETUP

Inspect existing MCPs first and reuse suitable working entries.

Set up:
1. Context7 for relevant library/API documentation.
2. Microsoft Playwright MCP for browser-based verification.

Optional:
- Reuse an existing GitHub MCP if configured.
- Otherwise use existing git/gh capabilities where sufficient and report whether GitHub MCP would add value.
- Do not add Chrome DevTools or other MCPs unless a concrete capability gap requires them.

Use official server documentation to verify installation commands and configuration. Install/configure project-scoped entries where supported.

### Context7
- Request documentation matching the project’s actual package versions.
- Check library coverage rather than assuming every library is supported.
- Prefer targeted queries over retrieving large documentation dumps.

### Playwright
- Use it for relevant browser reproduction and verification.
- Prefer headless operation where supported to reduce distracting windows.
- Keep QA’s browser state separate from my personal browser session.

Do not add filesystem, shell, or git MCPs merely to duplicate capabilities already available in Codex.

If authentication or a missing prerequisite blocks an MCP, do not invent credentials. Finish the remaining setup and report the exact step I must complete.

## TOKEN EFFICIENCY

- Keep GPT-5.6 Sol and Luna as the defaults.
- Do not invoke all three agents for every task.
- Read relevant files first and broaden investigation only as evidence requires.
- Use concise assignments and structured handoffs.
- Return short test summaries; include detailed logs only for failures that need investigation.
- Avoid repeated repository scans, duplicate reviews, unnecessary test reruns, and frequent status polling.
- Recommend model/reasoning escalation only for a specific unresolved problem; do not change defaults automatically.
- Use MCP tools when they add useful information or actions, rather than calling them on every task.

## VALIDATION AND DELIVERY

- Validate the edited configuration using supported CLI checks where available.
- Verify that the intended agent definitions can be discovered.
- Check MCP connectivity and tool discovery where possible.
- Distinguish configured, connected, and functionally tested status.
- Do not claim success for checks you could not run.

Finish with:
1. Files changed and a concise summary.
2. Verified models and reasoning settings.
3. MCP status and any remaining authentication steps.
4. Whether I need to restart the CLI.
5. A short example command or prompt I can use to run the three-agent workflow on my next feature.

Do not commit, push, or modify application code as part of this setup.
