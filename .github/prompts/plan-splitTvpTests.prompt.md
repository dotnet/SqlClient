## Plan: Split TvpTest.cs Into Independent Test Classes

The current `TestMain()` runs 500+ test cases sequentially via baseline comparison, making it slow, hard to debug, and retry-unfriendly. The tests are already logically grouped into helper classes (`StreamInputParam`, `SqlVariantParam`, `DateTimeVariantTest`, `OutputParameter`) plus inline methods, with no shared state between groups—making them ideal candidates for splitting.

### Steps (Each Step = One Git Commit)

1. ✅ **Add per-group baseline files and update `TestMain()` to validate using concatenated split files** — Extract sections from [SqlParameterTest_DebugMode.bsl](src/Microsoft.Data.SqlClient/tests/ManualTests/SQL/ParameterTest/SqlParameterTest_DebugMode.bsl), [SqlParameterTest_ReleaseMode.bsl](src/Microsoft.Data.SqlClient/tests/ManualTests/SQL/ParameterTest/SqlParameterTest_ReleaseMode.bsl), and Azure variants into new files (`StreamInputParameter_DebugMode.bsl`, `StreamInputParameter_ReleaseMode.bsl`, `TvpColumnBoundaries_DebugMode.bsl`, `TvpColumnBoundaries_ReleaseMode.bsl`, etc.). Modify `FindDiffFromBaseline()` to concatenate the split baseline files in order (selecting Debug or Release variants based on build configuration) and compare against test output instead of using the original combined file. **If the split is incorrect, `TestMain()` fails—proving equivalence when CI passes.**

2. ✅ **Add new test classes that run alongside `TestMain()`** — Create [StreamInputParameterTests.cs](src/Microsoft.Data.SqlClient/tests/ManualTests/SQL/ParameterTest/), [TvpColumnBoundariesTests.cs](src/Microsoft.Data.SqlClient/tests/ManualTests/SQL/ParameterTest/), [TvpQueryHintsTests.cs](src/Microsoft.Data.SqlClient/tests/ManualTests/SQL/ParameterTest/), [SqlVariantParameterTests.cs](src/Microsoft.Data.SqlClient/tests/ManualTests/SQL/ParameterTest/), [DateTimeVariantTests.cs](src/Microsoft.Data.SqlClient/tests/ManualTests/SQL/ParameterTest/), [OutputParameterTests.cs](src/Microsoft.Data.SqlClient/tests/ManualTests/SQL/ParameterTest/). Each class has its own baseline comparison method pointing to its split baseline files (Debug and Release variants), using `#if DEBUG` to select the appropriate baseline. Original `TestMain()` remains unchanged. **CI runs both old and new tests—all must pass.**

3. ✅ **Delete `TestMain()` and original combined baseline files** — Remove `TestMain()`, `RunTestCoreAndCompareWithBaseline()`, `RunTest()`, `FindDiffFromBaseline()` from [TvpTest.cs](src/Microsoft.Data.SqlClient/tests/ManualTests/SQL/ParameterTest/TvpTest.cs). Delete [SqlParameterTest_*.bsl](src/Microsoft.Data.SqlClient/tests/ManualTests/SQL/ParameterTest/) files. Keep shared helper methods/classes in TvpTest.cs or move to a shared utilities file. Per-group Debug and Release baseline files remain. **Reviewer can verify deletion is safe since steps 1-2 already validated equivalence and new tests passed in CI.**

4. ✅ **[Repeatable per group] Migrate one test class from baseline comparison to xUnit assertions** — For each group (start with smallest, e.g., `OutputParameterTests`): replace `Console.WriteLine` comparisons with `Assert.Equal()`, `Assert.True()`, etc., then delete that group's Debug and Release baseline files. Use `[Theory]` with `[MemberData]` pointing to static methods that yield all test case permutations with unique identifiers (e.g., `"StreamInput_Sync_DataLen100000_ParamLen-1_OldFalse"`)—xUnit displays each identifier in test output, allowing reviewers to diff test names against baseline to verify completeness. For DEBUG-only test cases, use `#if DEBUG` or `[Trait("Configuration", "Debug")]` to conditionally include them. **Each migration is a separate commit; reviewer sees direct assertion logic instead of string comparison.**

### Baseline Section Boundaries

| Test Group | Start Marker | End Marker | Debug | Release | Debug Azure | Release Azure |
|------------|--------------|------------|-------|---------|-------------|---------------|
| **StreamInputParameter** | `Starting test 'TvpTest'` (line 1) | Line before `+++++++ Iteration 0 ++++++++` | 1–478 | 1–17 | 1–478 | 1–17 |
| **TvpColumnBoundaries** | `+++++++ Iteration 0 ++++++++` | Line before `------- Sort order + uniqueness #1` | 479–943 | 18–482 | 479–943 | 18–482 |
| **TvpQueryHints** | `------- Sort order + uniqueness #1: simple -------` | Last `-------------` line before empty line | 944–974 | 483–513 | 944–974 | 483–513 |
| **SqlVariantParameter** | Empty line + `Starting test 'SqlVariantParam'` | `End test 'SqlVariantParam'` (inclusive) | 975–1073 | 514–612 | 975–1073 | 514–612 |
| **DateTimeVariant** | Line after `End test 'SqlVariantParam'` | Line before `Starting 'OutputParameter' tests` | 1074–2231 | 613–1770 | 1074–2231 | 613–1770 |
| **OutputParameter** | `Starting 'OutputParameter' tests` | `Done` (end of file) | 2232–2235 | 1771–1774 | 2232–2235 | 1771–1774 |

**Note:** Azure baseline files have identical line numbers to their non-Azure counterparts—differences are in content (different error messages/behaviors on Azure SQL), not structure.
