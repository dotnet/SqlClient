# Plan: Migrate to Microsoft.Testing.Platform (MTP)

**TL;DR**: You're currently on **xUnit 2.9.3** running under **VSTest** (via `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio`). The recommended migration path is to upgrade to **xUnit v3 with native MTP support** via `xunit.v3.runner.mtp`. Your `xunit.runner.json` already contains `_v3_` prefixed settings, so this move was clearly anticipated. The biggest risk is verifying that `Microsoft.DotNet.XUnitExtensions` (from dotnet/arcade) has a v3-compatible release — it's used heavily for `ConditionalFact`/`ConditionalTheory` across ManualTests and FunctionalTests.

---

## Current State

| Component | Current | 
|-----------|---------|
| Test framework | xUnit 2.9.3 |
| Test platform | VSTest (`Microsoft.NET.Test.Sdk` 17.14.1) |
| VSTest adapter | `xunit.runner.visualstudio` 2.8.2 |
| Console runner | `xunit.runner.console` 2.9.3 |
| Conditional test infra | `Microsoft.DotNet.XUnitExtensions` 11.0.0-beta |
| Target frameworks | net462, net8.0, net9.0, net10.0 |

5 test projects affected: UnitTests, FunctionalTests, ManualTests, Abstractions.Test, Azure.Test. PerformanceTests/StressTests are out of scope (BenchmarkDotNet/custom harness).

---

## Steps

### Phase 1: Dependency & Compatibility Research *(blocks everything)*

1. **Verify `Microsoft.DotNet.XUnitExtensions` v3 compatibility** — check dotnet/arcade for an xUnit v3-compatible version. This is the single biggest blocker. If none exists, fall back to a bridge approach (VSTestBridge shim with xUnit v2 under MTP).
2. **Audit xUnit v2→v3 breaking changes** — key areas: `IAsyncLifetime` gains `CancellationToken`, `Assert` API changes, `TheoryData` generics, `[Collection]` behavior.
3. **Verify net462 support** — xUnit v3 supports .NET Standard 2.0+ (includes net462), but confirm `xunit.v3.runner.mtp` works on that TFM.

### Phase 2: Package Updates *(parallel with step 5)*

4. **Update `tests/Directory.Packages.props`**: replace `xunit` → `xunit.v3`, remove `xunit.runner.visualstudio` + `xunit.runner.console` + `Microsoft.NET.Test.Sdk`, add `xunit.v3.runner.mtp`, update `Microsoft.DotNet.XUnitExtensions` to v3-compatible version.
5. **Add MTP properties** — set `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>` in test projects or a shared props file if needed.

### Phase 3: Update Test Project Files *(depends on Phase 2)*

6. **Update all 5 test csproj files** — each has package refs duplicated in netfx and netcore `ItemGroup`s. Replace `xunit` → `xunit.v3`, remove `xunit.runner.visualstudio`, `xunit.runner.console`, `Microsoft.NET.Test.Sdk`, add `xunit.v3.runner.mtp`.
7. **Update `xunit.runner.json`** — activate v3 settings (rename `_v3_` keys to actual names), switch `$schema` to v3, remove v2-only `shadowCopy`.

### Phase 4: Fix Test Code *(depends on Phase 3)*

8. **Fix xUnit v3 breaking changes** — `IAsyncLifetime` signature changes, assertion API changes, namespace changes.
9. **Fix `Microsoft.DotNet.XUnitExtensions` API changes** — update all `ConditionalFact`, `ConditionalTheory`, `PlatformSpecific`, `ActiveIssue`, `SkipOnPlatform` usages if needed.

### Phase 5: Build Infrastructure *(depends on Phase 3)*

10. **Update `build2.proj` test targets** — verify `dotnet test` CLI args work under MTP: `--blame-hang`, `--collect "Code coverage"`, `--filter`, `--logger:"trx"`. MTP optionally replaces these with extension packages (`Microsoft.Testing.Extensions.HangDump`, `.TrxReport`, `.CodeCoverage`).
11. **Update `CodeCoverage.runsettings`** — verify MTP compatibility.
12. **Update CI pipelines in `eng/pipelines/`** — current pipelines use `MSBuild@1`/`DotNetCoreCLI@2` which invoke `dotnet test`, so changes should be minimal.

### Phase 6: Validation *(depends on all above)*

13. Build + run UnitTests (simplest, fastest feedback) on all TFMs and OSes.
14. Build + run FunctionalTests.
15. Build + run ManualTests (CI or with SQL Server).
16. Verify code coverage, TRX output, blame-hang, and test filtering all work.
17. Full CI pipeline pass.

---

## Relevant Files

- `src/Microsoft.Data.SqlClient/tests/Directory.Packages.props` — central test package versions (primary)
- `src/Microsoft.Data.SqlClient/tests/UnitTests/Microsoft.Data.SqlClient.UnitTests.csproj` — package refs in netfx + netcore ItemGroups
- `src/Microsoft.Data.SqlClient/tests/FunctionalTests/Microsoft.Data.SqlClient.FunctionalTests.csproj` — same
- `src/Microsoft.Data.SqlClient/tests/ManualTests/Microsoft.Data.SqlClient.ManualTests.csproj` — same
- `src/Microsoft.Data.SqlClient.Extensions/Abstractions/test/Abstractions.Test.csproj` — extension tests
- `src/Microsoft.Data.SqlClient.Extensions/Azure/test/Azure.Test.csproj` — extension tests
- `src/Microsoft.Data.SqlClient/tests/tools/Microsoft.Data.SqlClient.TestUtilities/xunit.runner.json` — runner config (already v3-prepped)
- `src/Microsoft.Data.SqlClient/tests/tools/Microsoft.Data.SqlClient.TestUtilities/CodeCoverage.runsettings` — coverage config
- `build2.proj` — `TestMdsUnit`, `TestMdsFunctional`, `TestMdsManual` targets (lines 495-590)

## Verification

1. `dotnet build` succeeds for all test csproj files across all TFMs
2. `dotnet test` discovers and runs tests under MTP (not VSTest)
3. `--filter "category!=failing&category!=flaky"` filtering works
4. `--collect "Code coverage"` + runsettings produces coverage
5. `--logger:"trx"` produces TRX output
6. `dotnet msbuild build2.proj -t:TestMdsUnit` works end-to-end
7. CI pipeline passes all stages

## Decisions

- **Recommended**: Direct xUnit v3 + native MTP (Option B) — `xunit.runner.json` already prepped for v3
- **Fallback**: If `Microsoft.DotNet.XUnitExtensions` lacks v3 support → use VSTestBridge shim with xUnit v2 under MTP first, upgrade to v3 later
- **Out of scope**: PerformanceTests, StressTests, legacy `build.proj`

## Further Considerations

1. **`Microsoft.DotNet.XUnitExtensions` v3 compatibility** — The single biggest risk. Check [dotnet/arcade](https://github.com/dotnet/arcade) for xUnit v3 tracking. If blocked, the bridge approach (`Microsoft.Testing.Extensions.VSTestBridge`) lets you adopt MTP immediately without touching xUnit version.
2. **Incremental vs. big-bang** — Since package versions are centralized in `Directory.Packages.props`, all 5 projects move together. You could migrate UnitTests first by decentralizing its packages temporarily, but this adds complexity. Recommend moving all at once since the csproj changes are mechanical.
3. **MTP native extensions vs. CLI compat** — MTP offers packages like `Microsoft.Testing.Extensions.HangDump` and `.TrxReport` as replacements for `--blame-hang` and `--logger:trx`. For initial migration, stick with `dotnet test` CLI compatibility to minimize changes, then optionally adopt native extensions later.
