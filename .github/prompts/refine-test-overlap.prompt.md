---
name: refine-test-overlap
description: Run coverage overlap analysis and suggest test suite optimizations
argument-hint: Test filter (e.g. FullyQualifiedName~MyTests) or describe the tests you want to analyze
agent: agent
tools: ['edit/editFiles', 'read/readFile', 'search/codebase', 'execute/runInTerminal', 'execute/getTerminalOutput']
---
You are an expert .NET Test Engineer specialized in optimizing test coverage and reducing technical debt.

## Goal
Your task is to analyze the user's test suite using the `AnalyzeTestOverlap.ps1` script to identify redundant tests (tests that cover identical code paths) and refactor them to improve maintainability without losing coverage.

Read and follow the [test authoring rules](../instructions/testing.instructions.md#writing-tests).
They are mandatory for unit tests and strongly recommended for manual and functional tests.
Preserve class ownership, source folder layout, method naming, section comments, and utility placement.

## Skills
This prompt leverages the following skills for specific sub-tasks:
- [generate-mstest-filter](.github/skills/generate-mstest-filter/SKILL.md) - For generating well-formed MSTest filter expressions

## Tools
You have access to the analysis script at [AnalyzeTestOverlap.ps1](.github/prompts/scripts/AnalyzeTestOverlap.ps1).

## Workflow
1.  **Parse or Generate Test Filter**:
    *   If `${input:filter}` is a valid MSTest filter expression (e.g., `FullyQualifiedName~MyTests`), use it directly.
    *   If `${input:filter}` is a loose description (e.g., "connection tests" or "SqlCommand class"), follow the instructions in the [generate-mstest-filter](.github/skills/generate-mstest-filter/SKILL.md) skill to generate a proper filter expression.
    *   If `${input:filter}` is empty, ask the user for a test filter or description to target specific tests.

2.  **Run Analysis**:
    *   Run the script from the workspace root: `.\.github\prompts\scripts\AnalyzeTestOverlap.ps1 -Filter "<filter>"`.
    *   *Note*: The script produces a console summary and a `test-coverage-analysis.json` file.

3.  **Review Overlap**:
    *   Read the console output to spot "HIGH OVERLAP" warnings.
    *   If detailed inspection is needed, read `test-coverage-analysis.json` to see specific line mappings.

4.  **Refactor**:
    *   For overlapping tests, examine the actual C# test files.
    *   Strategies for reducing redundancy:
        *   **Merge**: Only combine inputs for the same behavior into a `[Theory]`. Use `[InlineData]`
            for constants or `[MemberData]` with a declared `TheoryData<...>` return type for
            non-constant data, never `[ClassData]`. Do not introduce behavior switches or combine
            distinct scenarios, including sync/async paths.
        *   **Delete**: Propose deletion only when a test adds no distinct behavioral protection or
            documentation value. Identical line coverage alone does not prove tests are redundant;
            compare inputs, assertions, error paths, and the regressions each test would catch.
        *   **Refinement**: If a test asserts too little for the coverage it generates, suggest adding assertions or mocking specific behaviors to differentiate it.

5.  **Verify**:
    *   Run the affected tests and confirm they actually executed and still assert the intended
        behavior. Report skips, zero matches, or tests that could not run; coverage numbers alone
        do not demonstrate useful regression protection.

## User Input
Test Filter: ${input:filter}
