# Verify before you claim

Finding classes that look right from the diff but often are not in this repo.
Check the named source before raising one. Each entry is a dated observation
(seeded 2026-10 from repository policy and build files); if the repo disagrees,
trust the repo and report [skill drift](skill-drift.md).

New entries come from drift reports with evidence, not from guesses.

## API and compatibility

- **"This is a breaking change."** Check whether the member is in
  `src/Microsoft.Data.SqlClient/ref/` and whether it shipped in a released
  package. An API added and changed between releases breaks nobody. A `public`
  member on an `internal` type is not public API. When shell is authorized,
  compare against the last release:
  `dotnet build build.proj /t:CompareMdsRefAssemblies /p:BaselinePackageVersion=<last>`.
  Behavioral breaks still count; signature parity alone does not clear them.
- **"Add an AppContext switch."** `policy/review-process.md` asks for an opt-out
  only for non-security breaking changes that affect customers. Confirm the
  change is breaking first. Check `LocalAppContextSwitches.cs` and
  `.github/instructions/features.instructions.md` for an existing switch.
- **"Ref was not updated."** The ref project uses conditional compilation per
  target. Check every affected `#if` branch in `ref/` before calling one missing.

## Repository process

- **"Add a CHANGELOG entry."** PRs must not edit `CHANGELOG.md`; release notes
  are produced at release time under `release-notes/`. Not a finding. What is
  real: a behavior change hidden in a refactor PR should be named in the PR
  description.
- **"Code belongs in netfx/ or netcore/."** Those folders are gone. All code is
  in `src/Microsoft.Data.SqlClient/src/`. New files under legacy paths are a
  real finding; asking to mirror a change into them is not.
- **"No CI ran" or "no coverage report."** PR pipelines are path filtered. See
  [posting.md](posting.md#ci-and-coverage). Read the filter before reporting.
- **Milestone, labels, assignees.** Workflows handle these. Not review findings.

## Platform and framework

- **"This only compiles on Windows/.NET Framework."** Do not infer from the file
  suffix alone. Read the compile conditions in the project and imported build
  files, and `#if` guards around the code.
- **"Native SNI path is missed."** Managed and native SNI differ. Confirm the
  changed path is actually shared, or that the native path has the same bug,
  before asking for parity.
- **"Sync and async must match."** Compare observable results and cleanup, not
  implementation shape. Some exception timing differences are documented
  compatibility behavior.

## Tests

- **Check that assertions guard the promised behavior.** Inspect current-head
  CI and the test's setup and assertions first. If a coverage question remains
  unresolved, an optional mutation experiment in an authorized disposable
  environment can show whether the test detects the relevant broken behavior.
  Do not mutate user work or rerun evidence already settled by CI.
  A newly introduced regression test should fail on the base for the bug it
  reproduces; strengthened tests and tests of already-working behavior can
  legitimately pass on the base.
- **Common vacuous patterns here:** pool reuse asserted by opening another
  connection with the same key (it may get a different physical connection);
  expected values computed with the same conversion under test; a test that
  passes on an unrelated `SqlException` or timeout; setup that already did the
  cleanup the fix is supposed to do.
- **"Coverage gap."** Prove it: name the input that the current tests miss, or
  show the suite still passes with the bug reintroduced. Read the Codecov
  comment first when present.
- **"Needs a ManualTest."** ManualTests need a configured SQL Server. Prefer the
  simulated TDS server in unit/functional tests when the behavior is wire-level
  and the simulator supports it.

## Performance

- **Timing claims need timing.** Allocation counts or object size do not predict
  latency. Use `PerformanceTests` (BenchmarkDotNet) or a stated workload.
- **"This allocates."** Read the API first. Slicing a `Span<T>`, `AsSpan`, and
  many `TryFormat` paths do not allocate; a redundant scan or copy may be the
  real cost and is a different finding.

## Scope and other reviewers

- **"This PR introduces X."** Check whether the same shape exists on the base
  branch. A pre-existing, repo-wide gap is a valid deferral when fixing one call
  site would leave the rest inconsistent.
- **Bot findings.** Check each cited location and type against the file.
  "Also at lines X, Y, Z" lists are often wrong. Verify the proposed fix too.
- **Answered threads.** A restatement at higher severity is not new. The reply
  usually explains why the obvious fix was declined.
