---
name: triage-pipeline-failures
description: Find and classify failing tests in the Microsoft.Data.SqlClient CI/CD pipelines at or after a given commit, then fix or quarantine them.
argument-hint: <target commit SHA> [optional scope, e.g. specific pipelines/branches]
agent: agent
# No `tools:` scoping on purpose: this prompt is access-agnostic and must be able
# to call whatever Azure DevOps MCP server is connected (e.g. `ado/*`) in addition
# to the built-in terminal/read/search/edit tools. Declaring a scoped `tools:` list
# would strip out MCP/extension tools and break the preferred ADO MCP access path.
---

Triage failing tests in the Microsoft.Data.SqlClient pipelines for commit
`${input:commit}` and later. Treat only the **first whitespace-delimited token** of
`${input:commit}` as the target commit SHA — that token is what every git ancestry
check (`git merge-base --is-ancestor <target> ...`) uses. Any remaining text is
**optional scope** (e.g. a pipeline name or branch): honor it when present, otherwise
use the defaults below.

## Azure DevOps access is agnostic

Every data-retrieval step below is described as an **operation**, not a command.
Perform each operation with whatever Azure DevOps access is available, in this order
of preference:

1. An **Azure DevOps MCP server**, if one is connected (preferred — no shell needed).
2. The **`az` CLI** (`az rest --resource 499b84ac-1321-427f-aa17-267ca6975798 ...`,
   `az pipelines ...`, `az boards ...`).
3. **Direct ADO REST** calls over HTTPS with a bearer token.

Do not assume a specific mechanism. If the first choice is unavailable or errors,
fall back to the next. Keep read operations read-only; only edits to test source
files (quarantine/fix) modify state, and those happen in the repo, not in ADO.

## Environment

- **ADO organization**: `SqlClientDrivers`.
- **Projects**:
  - `public` — pipelines under `\ADO\CI` and `\ADO\PR` target the GitHub
    `dotnet/SqlClient` repo.
  - `ADO.Net` — CI/OneBranch pipelines target the ADO mirror `dotnet-sqlclient`.
- The `dotnet-sqlclient` mirror preserves the **same commit SHAs** as GitHub, so git
  ancestry against a GitHub SHA works. It **lags** GitHub because synchronization is
  gated by PRs: a commit only appears in the mirror once its sync PR completes, so a
  target commit may not be present in the mirror yet even though it is on GitHub.

## Step 1 — Scope to the right pipelines

**Operation:** list build definitions in both `public` and `ADO.Net`, with each
definition's folder path, `queueStatus`, and `repository.type` / `repository.name`.

Ignore any definition that is **not currently enabled** (`queueStatus != enabled`,
i.e. disabled or paused) — also skip names flagged `[Disabled]`, `[Retired]`, or under
`\Retired\` folders. Then keep only definitions whose repo is `dotnet/SqlClient`
(GitHub) or `dotnet-sqlclient` (TfsGit). Exclude the legacy `Microsoft.Data.SqlClient`
and `*.sni` repos unless the user asks for SNI.

Because this triage targets **non-PR commit runs** (see Step 2), prefer CI/branch
definitions over PR-validation ones. Typical in-scope pipelines: CI-SqlClient,
CI-SqlClient-Package, sqlclient-ci-stress, sqlclient-ci-package (public); MDS Main CI,
MDS Main CI-Package, sqlclient-kerberos, Test-SqlClient-Managed-Instance, OneBranch
official/non-official (ADO.Net). PR-triggered definitions (PR-SqlClient-Project,
PR-SqlClient-Package, sqlclient-pr) are in scope only for the CI/branch runs they may
also host — their PR-ref runs are excluded in Step 2 unless the user asks to include
PR runs.

## Step 2 — Find runs at/after the target commit

**Operation:** for each in-scope definition, list recent runs (filter to
`failed`, `partiallySucceeded`, `canceled`) with their `sourceBranch`,
`sourceVersion`, `result`, and `finishTime`.

**Limit to non-PR commit runs.** Only consider runs triggered by real commits on
tracked branches (e.g. `refs/heads/main`, `refs/heads/release/*`); **exclude PR
validation runs**. A run is a PR run — and therefore out of scope — when any of these
hold:

- Its `sourceBranch` is an ephemeral merge ref such as `refs/pull/N/merge` or
  `refs/pull/N/head`.
- Its build `reason` is `pullRequest`.
- It is a PR-triggered definition (e.g. `PR-SqlClient-Project`, `PR-SqlClient-Package`,
  `sqlclient-pr`) running against a PR ref.

Keep only runs whose `sourceVersion` is a committed SHA on a tracked branch. If the
user explicitly asks to include PR runs, honor that override.

Resolve **"at or after `${input:commit}`" by commit graph, not timestamp**:

- `git merge-base --is-ancestor <target> <sourceVersion>` → true means the run's
  commit is the target or a descendant (in scope).
- `sourceVersion == <target>` → the target itself (in scope).
- Divergent `release/*` or `dev/*` commits do **not** descend from a `main` target —
  exclude them.

Mirror (`dotnet-sqlclient`) runs use the **same SHAs** as GitHub, so apply the same
ancestry checks. Because mirror sync is PR-gated, the target commit may not have
reached the mirror yet — in that window there simply are no in-scope mirror runs, so
do not infer a run is out of scope from a SHA mismatch (there is none); it is only a
timing lag.

## Step 3 — Enumerate failing test runs per build

**Operation:** for each in-scope build, list its test runs.

Compute real failures as `totalTests - passedTests - notApplicableTests`. Do **not**
treat `unanalyzedTests`/`notApplicableTests` as failures. Keep runs with failures > 0.

## Step 4 — Get failing test names and errors

**Operation:** for each failing test run, fetch the `Failed` results with their
`automatedTestName`, `errorMessage`, and `stackTrace`.

**You must capture the actual xUnit output and full stack trace for every failed
test — do not classify a failure without it.** The one-line `errorMessage` is not
enough; get the complete assertion text (e.g. `Assert.Equal() Failure: Values differ /
Expected / Actual`) and the full stack frames (the test method and the failing product
frames). If any source truncates it, cross-check another until you have the whole thing:

- The result's `errorMessage` + `stackTrace` fields (expand sub-results — see below).
- The test run's **attachments** (TRX / `*.trx`, console logs) when the API truncates
  long stacks.
- The **job log** for the test step (Step 5) — the raw `dotnet test` / xUnit output
  always contains the assertion and stack, even when the results API does not.

**CRITICAL — data-driven (Theory) results hide the error on a child:** xUnit
`[Theory]`/`[ClassData]`/`[InlineData]` tests publish as a parent result with
`resultGroupType == "dataDriven"` whose own `errorMessage`/`stackTrace` are **null**.
The real assertion lives on the failing **sub-result**. When a `Failed` result has a
null error, re-fetch that result **including sub-results** and read the child. A null
parent error means "look at the children", not "the test aborted". Only treat it as an
abort when the build log also shows no assertion and the process was terminated
(e.g. a `--blame-hang` dump).

## Step 5 — Locate each failure's job (for logs/links)

**Operation:** for a failing run, read its `pipelineReference` (stage/phase/job), then
read the build's timeline and walk Stage → Phase → Job by `parentId` to get the job
record id. Build deep links:

- Tests tab: `.../_build/results?buildId=<id>&view=ms.vss-test-web.build-test-results-tab`
- A specific result: append `&runId=<runId>&resultId=<resultId>&paneView=debug`
- Job logs: `.../_build/results?buildId=<id>&view=logs&j=<jobRecordId>`

## Step 6 — Classify every failure

| Class | Signals | Action |
|-------|---------|--------|
| **True positive** (broken driver) | Deterministic; fails on every leg for the commit; assertion tied to changed code; absent on the parent commit | Fix the bug; keep/add a failing test |
| **Test-isolation / concurrency** | Off-by-a-small-count on a process-global resource (e.g. pool `ConnectionCount` Expected 2 Actual 3); some data rows pass, others fail; depends on parallel tests | Isolate the resource (unique connection string, `[Collection]`); else quarantine |
| **Flaky (timing/GC/load)** | Intermittent; only under CI load; GC-finalizer or retry/failover timing; "connection is broken" under contention | Deterministic fix (poll not sleep, set retry interval/timeouts); else quarantine |
| **Environmental / infra** | Empty error AND no assertion in the log; host/agent crash; blame-hang dump; network/DTC outage; many unrelated tests fail at once | Re-run to confirm; report infra; don't quarantine on a single infra hit |

Determine **regression vs pre-existing** by repeating Steps 3–4 on the
immediately-preceding in-scope build (the parent commit). A failure present before
`${input:commit}` was not introduced by it.

## Step 7 — Check quarantine status before acting

A test is already quarantined if it carries `[Trait("Category", "flaky")]`; those run
in a separate, non-blocking quarantine step (`TestFilters="category=flaky"`) while the
regular step excludes `category!=failing&category!=flaky&category!=interactive`.

- Already-quarantined failure = expected quarantine noise, not a blocker. Only escalate
  with a real fix.
- Non-quarantined failure in a regular step = a real blocker.

## Step 8 — Present findings and STOP (checkpoint)

Steps 1–7 are **read-only investigation**. Before changing anything, present your
findings and wait for the user's explicit go-ahead. Do **not** edit any files or take
any action until the user approves.

Present a per-failure table: test name, in-scope build(s), full xUnit assertion +
key stack frames, classification, whether it is a regression, current quarantine
status, and the **proposed** action (fix / quarantine / already quarantined /
re-run to confirm). Link each failure to its build/result and job. Then explicitly ask
the user which items to act on.

## Step 9 — Act (only after approval)

For each item the user approves:

1. Prefer a **deterministic fix** that removes the race/isolation/timing dependency.
2. Otherwise **quarantine**: add `[Trait("Category", "flaky")]` plus a comment holding
   the observed failure signature (test name, assertion, key stack frames) and the
   root-cause reasoning. Mirror existing quarantine comments in `ConnectionFailoverTests.cs`.
3. Cover both sync and async variants when the API has both.
4. Un-quarantine once fixed and consistently green.

Make only the source edits needed to fix or quarantine; do not modify pipeline YAML or
ADO state. After editing, report what changed.
