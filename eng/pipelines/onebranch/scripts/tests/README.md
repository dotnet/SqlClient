# OneBranch PowerShell Tests

Pester tests for the symbol publishing and endpoint reachability scripts.

## Prerequisites

- PowerShell 7+ (`pwsh`). Windows PowerShell 5.1 is not supported: the scripts and
  tests use PowerShell Core types such as `Microsoft.PowerShell.Commands.HttpResponseException`.
- [Pester v5](https://pester.dev/) (`Install-Module Pester -MinimumVersion 5.0 -Scope CurrentUser`)
- Azure CLI (`az`) available on PATH so Pester can mock the command. The publishing
  tests mock all Azure CLI and REST calls; no Azure credentials are required.

## Running the Tests

From the repository root:

```console
pwsh -NoProfile -Command "Invoke-Pester ./eng/pipelines/onebranch/scripts/tests/ -Output Detailed -CI"
```

## Test Coverage

| Area | What's tested |
| ---- | ------------- |
| Parameter validation | Empty strings rejected for mandatory publishing parameters |
| URL construction | Registration and request URLs built from the supplied parameters |
| Request bodies | JSON registration body, default publishing flags, and flag overrides |
| Error handling | Failed or empty token acquisition and failed registration, publishing, or status calls |
| Status validation | Failed/Cancelled results rejected, disabled destinations ignored, Succeeded/Pending accepted |
| Token diagnostics | Allowlisted claims only, malformed payload handling, diagnostics disabled by default |
| Command logging | Opt-in logging with unconditional Authorization redaction |
| HTTP failure detail | HTTP status, correlation IDs, exception types, and missing responses |
| Endpoint reachability | Host list parsing, DNS and TCP failures, socket error codes, and continued probing |

The reachability tests use real sockets against loopback and the RFC 2606 reserved
`.invalid` TLD; they do not contact the symbol publishing service.

## Pipeline Behavior

The publishing step template calls the [publishing script](../publish-symbols.ps1).
The pipeline's `debug` parameter is forwarded through the build stages and jobs to
`-VerboseDiagnostics`. It defaults to false in the publishing template.

When debug is enabled, an [endpoint probe](../test-endpoint-reachability.ps1) runs
before publication. An unreachable endpoint is a diagnostic finding, not a reason
to skip publishing. HTTP failure details are always reported, even without debug.

These tests do not validate live Azure authentication or OneBranch template expansion;
those require validation in Azure DevOps.
