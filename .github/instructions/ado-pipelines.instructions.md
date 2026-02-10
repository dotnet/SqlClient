---
applyTo: "eng/pipelines/**,*.yml"
---
# Azure DevOps Pipelines Guide

## Overview

This repository uses Azure DevOps Pipelines for CI/CD. The pipeline configurations are located in `eng/pipelines/`.

**ADO Organization**: sqlclientdrivers
**ADO Project**: ADO.NET

## Pipeline Structure

```
eng/pipelines/
├── abstractions/               # Abstractions package pipelines
├── azure/                      # Azure package pipelines
├── common/                     # Shared templates
│   └── templates/
│       ├── jobs/               # Reusable job templates
│       ├── stages/             # Reusable stage templates
│       └── steps/              # Reusable step templates
├── jobs/                       # Top-level job definitions
├── libraries/                  # Shared variable definitions
├── stages/                     # Stage definitions
├── steps/                      # Step definitions
├── variables/                  # Variable templates
├── akv-official-pipeline.yml                   # AKV provider official/signing build
├── dotnet-sqlclient-ci-core.yml                # Core CI pipeline (reusable)
├── dotnet-sqlclient-ci-package-reference-pipeline.yml  # CI with package references
├── dotnet-sqlclient-ci-project-reference-pipeline.yml  # CI with project references
├── dotnet-sqlclient-signing-pipeline.yml       # Package signing pipeline
├── sqlclient-pr-package-ref-pipeline.yml       # PR validation (package ref)
├── sqlclient-pr-project-ref-pipeline.yml       # PR validation (project ref)
└── stress-tests-pipeline.yml                   # Stress testing
```

## Main Pipelines

### CI Core Pipeline (`dotnet-sqlclient-ci-core.yml`)
Reusable core CI pipeline consumed by both project-reference and package-reference CI pipelines. Configurable parameters:

| Parameter | Description | Default |
|-----------|-------------|---------|
| `targetFrameworks` | Windows test frameworks | `[net462, net8.0, net9.0, net10.0]` |
| `targetFrameworksUnix` | Unix test frameworks | `[net8.0, net9.0, net10.0]` |
| `referenceType` | Project or Package reference | Required |
| `buildConfiguration` | Debug or Release | Required |
| `useManagedSNI` | Test with managed SNI | `[false, true]` |
| `testJobTimeout` | Test job timeout (minutes) | Required |
| `runAlwaysEncryptedTests` | Include AE tests | `true` |
| `enableStressTests` | Include stress test stage | `false` |

### CI Reference Pipelines
- `dotnet-sqlclient-ci-project-reference-pipeline.yml` — Full CI using project references (builds from source)
- `dotnet-sqlclient-ci-package-reference-pipeline.yml` — Full CI using package references (tests against published NuGet packages)

### PR Validation Pipelines
- `sqlclient-pr-project-ref-pipeline.yml` — PR validation with project references
- `sqlclient-pr-package-ref-pipeline.yml` — PR validation with package references

These pipelines trigger on pull requests and run a subset of the full CI matrix to provide fast feedback.

### Official/Signing Pipeline (`dotnet-sqlclient-signing-pipeline.yml`)
Signs and publishes NuGet packages. Used for official releases. Requires secure service connections and key vault access for code signing.

### AKV Official Pipeline (`akv-official-pipeline.yml`)
Builds and signs the `Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider` add-on package separately from the main driver. Uses 1ES pipeline templates for compliance.

### Stress Tests Pipeline (`stress-tests-pipeline.yml`)
Optional pipeline for long-running stress and endurance testing. Enabled via `enableStressTests` parameter in CI core.

## Build Stages

1. **build_abstractions_package_stage**: Build and pack abstractions
2. **build_mds_akv_packages_stage**: Build main driver and AKV packages
3. **build_azure_package_stage**: Build Azure extensions package
4. **stress_tests_stage**: Optional stress testing
5. **run_tests_stage**: Execute all test suites

## Test Configuration

### Test Sets
Tests are divided into sets for parallelization:
- `TestSet=1` — First partition of tests
- `TestSet=2` — Second partition
- `TestSet=3` — Third partition
- `TestSet=AE` — Always Encrypted tests

### Test Filters
Tests use category-based filtering. The default filter excludes both `failing` and `flaky` tests:
```
category!=failing&category!=flaky
```

Category values:
- `nonnetfxtests` — Excluded on .NET Framework
- `nonnetcoreapptests` — Excluded on .NET Core
- `nonwindowstests` — Excluded on Windows
- `nonlinuxtests` — Excluded on Linux
- `failing` — Known permanent failures (excluded from all runs)
- `flaky` — Intermittently failing tests (quarantined, run separately)

### Flaky Test Quarantine in Pipelines
Quarantined tests (`[Trait("Category", "flaky")]`) run in **separate pipeline steps** after the main test steps. This ensures:
- Main test runs are **not blocked** by intermittent failures
- Flaky tests are still **monitored** for regression or resolution
- Code coverage is **not collected** for flaky test runs
- Results appear in pipeline output for visibility

The quarantine steps are configured in:
- `eng/pipelines/common/templates/steps/build-and-run-tests-netcore-step.yml`
- `eng/pipelines/common/templates/steps/build-and-run-tests-netfx-step.yml`
- `eng/pipelines/common/templates/steps/run-all-tests-step.yml`

### Test Timeout
All test runs use `--blame-hang-timeout 10m` (configured in `build.proj`). Tests exceeding 10 minutes are killed and reported as failures.

### SNI Testing
The `useManagedSNI` parameter controls testing with:
- Native SNI (`false`) - Windows native library
- Managed SNI (`true`) - Cross-platform managed implementation

## Variables

### Build Variables (`ci-build-variables.yml`)
Common build configuration:
- Package versions
- Build paths
- Signing configuration

### Runtime Variables
Set via pipeline parameters or UI:
- `Configuration` - Debug/Release
- `Platform` - AnyCPU/x86/x64
- `TF` - Target framework

## Creating Pipeline Changes

### Adding New Test Categories
1. Add category attribute to tests: `[Category("newcategory")]`
2. Update filter expressions in test job templates
3. Document category purpose in test documentation

### Adding New Pipeline Parameters
1. Define parameter in appropriate `.yml` file
2. Add to parameter passing in calling templates
3. Document in this file

### Modifying Build Steps
1. Changes should be made in template files for reusability
2. Test changes locally when possible
3. Submit as PR - validation will run

## Best Practices

### Template Design
- Use templates for reusable definitions
- Pass parameters explicitly (avoid global variables)
- Use descriptive stage/job/step names

### Variable Management
- Use template variables for shared values
- Use pipeline parameters for per-run configuration
- Avoid hardcoding versions (use Directory.Packages.props)

### Test Infrastructure
- Ensure tests are properly categorized
- Handle test configuration files properly
- Use test matrix for cross-platform coverage

## Troubleshooting

### Common Issues
1. **Test failures due to missing config**: Ensure `config.json` exists
2. **Platform-specific failures**: Check platform exclusion categories
3. **Timeout issues**: Increase `testJobTimeout` parameter

### Debugging Pipelines
- Enable debug mode via `debug: true` parameter
- Use `dotnetVerbosity: diagnostic` for detailed output
- Check build logs in Azure DevOps

## Security Considerations

- Pipelines use service connections for artifact publishing
- Signing uses secure key vault integration
- Sensitive configuration should use pipeline secrets
- Never commit credentials in pipeline files

## Related Documentation

- [BUILDGUIDE.md](../../BUILDGUIDE.md) - Local build instructions
- [Azure DevOps Documentation](https://learn.microsoft.com/azure/devops/pipelines/)
