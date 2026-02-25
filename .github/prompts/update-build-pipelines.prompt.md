---
name: update-build-pipelines
description: Guided workflow for updating Azure DevOps CI/CD pipelines for Microsoft.Data.SqlClient.
argument-hint: <describe the pipeline change needed>
agent: agent
tools: ['edit/createFile', 'edit/editFiles', 'read/readFile', 'codebase/search']
---

Update the Azure DevOps build pipelines for: "${input:change}".

All pipeline files are in `eng/pipelines/`. The ADO organization is **sqlclientdrivers**, project **ADO.NET**.

Follow this workflow step-by-step:

## 1. Understand the Pipeline Architecture
- Read the relevant pipeline file(s) in `eng/pipelines/`.
- Key pipelines:
  - `dotnet-sqlclient-ci-core.yml` — Core CI pipeline (reusable by reference pipelines)
  - `dotnet-sqlclient-ci-project-reference-pipeline.yml` — CI with project references
  - `dotnet-sqlclient-ci-package-reference-pipeline.yml` — CI with package references
  - `sqlclient-pr-project-ref-pipeline.yml` — PR validation (project references)
  - `sqlclient-pr-package-ref-pipeline.yml` — PR validation (package references)
  - `dotnet-sqlclient-signing-pipeline.yml` — Package signing
  - `akv-official-pipeline.yml` — AKV provider official build (1ES/OneBranch)
  - `stress-tests-pipeline.yml` — Stress tests
- Shared templates live in `eng/pipelines/common/templates/` (jobs/, stages/, steps/).
- Variables are defined in `eng/pipelines/variables/` and `eng/pipelines/libraries/`.

## 2. Identify What Needs to Change
- Determine which pipeline files are affected.
- Check if the change impacts shared templates that are reused across multiple pipelines.
- Identify if new parameters, variables, or stages need to be added.
- Review existing parameters to understand the current configuration surface:
  - `targetFrameworks` / `targetFrameworksUnix` — test target frameworks
  - `referenceType` — Project or Package reference
  - `buildConfiguration` — Debug/Release
  - `useManagedSNI` — Managed vs Native SNI testing

## 3. Implement the Change
- Modify YAML files following the existing patterns and indentation style.
- When adding new stages, follow the existing stage ordering:
  1. `build_abstractions_package_stage`
  2. `build_sqlclient_package_stage`
  3. `build_azure_package_stage`
  4. `stress_tests_stage` (optional)
  5. `run_tests_stage`
- When adding new test parameters, ensure they are wired through to test execution steps.
- When modifying shared templates, verify all consuming pipelines still work.

## 4. Test Configuration Considerations
- Test sets for parallelization: `TestSet=1`, `TestSet=2`, `TestSet=3`, `TestSet=AE`.
- Test filters by platform: `nonnetfxtests`, `nonnetcoreapptests`, `nonwindowstests`, `nonlinuxtests`.
- SNI testing matrix: both Native (`useManagedSNI=false`) and Managed (`useManagedSNI=true`).
- Always Encrypted tests controlled by `runAlwaysEncryptedTests` parameter.

## 5. Validate
- Verify YAML syntax is valid.
- Ensure no hardcoded values that should be parameterized.
- Check that variable and parameter references use the correct syntax (`$(variable)` for runtime/macro variables, `${{ parameters.param }}` for compile-time/template parameters).
- Confirm template references use correct relative paths.

## 6. Checklist
- [ ] Pipeline YAML is syntactically valid
- [ ] Changes follow existing template patterns
- [ ] Shared templates still compatible with all consuming pipelines
- [ ] New parameters have sensible defaults
- [ ] Test matrix covers all required framework/OS/SNI combinations
- [ ] No secrets or sensitive values hardcoded
