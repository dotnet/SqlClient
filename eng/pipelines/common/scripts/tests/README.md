# Shared Pipeline Script Tests

Pester tests for helpers shared by PR and OneBranch pipelines.
Use the [repository PowerShell setup](../../../../../BUILDGUIDE.md#powershell-modules), then run
from the repository root:

```powershell
dotnet tool run pwsh -- -NoProfile
Import-Module Pester
Invoke-Pester ./eng/pipelines/common/scripts/tests/
```

| File | Covers |
| ---- | ------ |
| `validate-xml-docs.Tests.ps1` | Documentation IDs, local and dependency cross-references, snippets, generated and packaged XML, lib/ref trimming, missing documentation, enum remarks, unresolved includes, unexpected elements, allowlisting, and report-only behavior. |
| `pipeline-invocation.Tests.ps1` | Shared step argument composition and strict/report-only invocation under the PowerShell task's dot-sourcing behavior. |

OneBranch-specific job wiring remains in
[`onebranch/scripts/tests/pipeline-invocation.Tests.ps1`](../../../onebranch/scripts/tests/pipeline-invocation.Tests.ps1).
