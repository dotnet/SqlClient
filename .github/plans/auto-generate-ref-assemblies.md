# Plan: Auto-Generate Ref Assemblies from Unified Src Project

## Summary

Enable `ProduceReferenceAssembly` in the unified src project so ref assemblies are compiler-generated during normal builds. Create a small dedicated project for `PlatformNotSupportedException` stubs. Use `Microsoft.DotNet.ApiCompat.Tool` to compare auto-generated ref assemblies against the checked-in ref sources as a baseline. Update the nuspec to consume artifacts from the new output paths.

## Phase 1: Enable auto-generated ref assemblies

1. In `src/Microsoft.Data.SqlClient/src/Microsoft.Data.SqlClient.csproj`, add two properties:
   - `<ProduceReferenceAssembly>true</ProduceReferenceAssembly>` (for all TFMs, including net462 which doesn't default to true)
   - `<GenerateDocumentationFile>true</GenerateDocumentationFile>` (so XML docs are generated alongside the build)

2. Add a post-build target in the unified csproj (or a new `.targets` file imported by it) that copies the ref assembly from `$(IntermediateOutputPath)ref/$(TargetFileName)` to `$(OutputPath)ref/$(TargetFileName)`. The SDK only places the ref DLL in the intermediate `obj/` directory — this copy step puts it alongside the main assembly output at a predictable location like `artifacts/Microsoft.Data.SqlClient/{Config}/{os}/{tfm}/ref/Microsoft.Data.SqlClient.dll`.

3. Also copy the generated XML doc file (`$(OutputPath)$(AssemblyName).xml`) to `$(OutputPath)ref/$(AssemblyName).xml` so the ref assembly has companion IntelliSense XML in the same directory.

4. Import `tools/targets/TrimDocsForIntelliSense.targets` in the unified src project to trim the XML doc file to only public API content (removing docs for internal/private members that the compiler includes).

## Phase 2: Create a dedicated PNSE stub project

5. Create a new project at `src/Microsoft.Data.SqlClient/notsupported/Microsoft.Data.SqlClient.NotSupported.csproj` targeting `net8.0;net9.0;netstandard2.0`. This project:
   - Sets `<GeneratePlatformNotSupportedAssemblyMessage>Microsoft.Data.SqlClient is not supported on this platform.</GeneratePlatformNotSupportedAssemblyMessage>`
   - Sets `<AssemblyName>Microsoft.Data.SqlClient</AssemblyName>` (same assembly name as the real one)
   - Imports `tools/targets/ResolveContract.targets` and `tools/targets/NotSupported.targets`
   - Points `ContractAssemblyPath` to the ref assembly produced by Phase 1 (for net8.0 or net9.0), or adds a `ProjectReference` to the unified src project with `OutputItemType=ResolvedMatchingContract` and `ReferenceOutputAssembly=false`
   - GenAPI reads the ref assembly, generates `.notsupported.cs` source with `PlatformNotSupportedException` bodies, and the project compiles it into DLLs for all three TFMs
   - Output goes to a well-known path (e.g., `artifacts/Microsoft.Data.SqlClient/{Config}/anyos/`)

6. Update `tools/targets/ResolveContract.targets` if needed — the current `ContractProject` default points to the legacy `netcore/ref/` project. The new PNSE project should either override `ContractProject` to point to the unified src project, or set `ContractAssemblyPath` directly to the Phase 1 ref assembly output.

## Phase 3: API compat checking

7. Add `Microsoft.DotNet.ApiCompat.Tool` to `dotnet-tools.json` as a local dotnet tool. This provides the `dotnet apicompat` command.

8. Create a new MSBuild target (e.g., `ValidateApiCompat` in a `.targets` file or in `build.proj`) that:
   - Builds the existing checked-in ref project at `src/Microsoft.Data.SqlClient/ref/Microsoft.Data.SqlClient.csproj` to produce baseline ref DLLs (for net462, net8.0, net9.0)
   - Runs `dotnet apicompat` comparing the auto-generated ref assemblies (from Phase 1) against these baseline DLLs
   - Fails the build if breaking changes are detected (new APIs are allowed; removed/changed APIs are errors)
   - This target should run as part of CI but be opt-in for local development (e.g., gated on a property like `ValidateApi=true`)

9. The checked-in ref sources at `src/Microsoft.Data.SqlClient/ref/` remain in the repo as the API baseline. They are no longer used for packaging — only for compat validation. Later, these can be replaced with a baseline from the last published NuGet package.

## Phase 4: Update build infrastructure

10. Update `build.proj` targets:
    - The `BuildNetCoreAllOS` target's AnyOS invocation (line 232) should build the new PNSE project instead of the legacy `netcore/src` with `OSGroup=AnyOS`
    - The `BuildNetStandard` target (line 241-242) should also use the PNSE project instead of the legacy `netcore/ref` with `BuildForLib=true`
    - Remove or deprecate the `OSGroup=AnyOS` invocation of `@(NetCoreDriver)` — it's replaced by the PNSE project
    - Add a new target (e.g., `BuildNotSupported`) that builds the PNSE project

11. Update `tools/specs/Microsoft.Data.SqlClient.nuspec` source paths:
    - **`ref/net462/`**: Change from `artifacts/$ReferenceType$/bin/Windows_NT/{Config}/Microsoft.Data.SqlClient/ref/net462/` → `artifacts/Microsoft.Data.SqlClient/{Config}/windows_nt/net462/ref/`
    - **`ref/net8.0/`**: Change from `artifacts/$ReferenceType$/bin/AnyOS/{Config}/Microsoft.Data.SqlClient/ref/net8.0/` → `artifacts/Microsoft.Data.SqlClient/{Config}/windows_nt/net8.0/ref/` (ref assemblies are OS-independent; use Windows build output)
    - **`ref/net9.0/`**: Same pattern as net8.0
    - **`ref/netstandard2.0/`**: Remove (no separate ref assembly for netstandard2.0 since the unified src project doesn't target it; the PNSE stub in `lib/netstandard2.0/` serves as the netstandard2.0 surface)
    - **`lib/net8.0/`** and **`lib/net9.0/`** (AnyOS stubs): Change to point to the PNSE project output
    - **`lib/netstandard2.0/`**: Change to point to the PNSE project output
    - Keep `lib/net462/`, `runtimes/win/`, and `runtimes/unix/` paths pointing to unified src project output

12. Verify that the `ref/netstandard2.0/` slot in the nuspec is still needed. If the package has `lib/netstandard2.0/` (the PNSE stub), NuGet may need a matching `ref/netstandard2.0/` for compile-time. Options:
    - Use the PNSE stub DLL itself as both `ref/` and `lib/` for netstandard2.0 (since it has the right API surface)
    - Or produce a ref assembly from the PNSE project too (via `ProduceReferenceAssembly` on the PNSE project)
    - Or use the net8.0 ref assembly re-targeted (may cause issues)

## Verification

- Build the unified src project and confirm ref assemblies + XML docs appear at the expected output paths for all three TFMs (net462, net8.0, net9.0)
- Build the PNSE project and confirm it produces `PlatformNotSupportedException` stubs for net8.0, net9.0, and netstandard2.0 — verify by decompiling or instantiating a type
- Run `dotnet apicompat` between the auto-generated ref assemblies and the checked-in baseline — should pass with no breaking changes
- Run `nuget pack` on the updated nuspec and verify the package structure matches the expected layout from `.github/instructions/nuget-package-structure.instructions.md`
- Run existing unit and functional tests to ensure no regressions

## Decisions

- **ProduceReferenceAssembly over GenAPI for ref generation**: Chose compiler-generated ref assemblies because they are guaranteed to match the implementation's public API surface, require no manual maintenance, and preserve XML doc content from the shared `doc/snippets/` files
- **Dedicated PNSE project over extending unified src**: Chose a separate project to keep concerns clean — the src project produces real implementations + ref assemblies, the PNSE project produces stubs. This avoids adding `AnyOS` mode complexity and `netstandard2.0` TFM to the unified src project
- **Checked-in ref sources as baseline**: Keeps the existing hand-maintained ref `.cs` files as the API compat baseline for now, with a planned future migration to comparing against the last published NuGet package
- **Update nuspec paths**: Chose to update the nuspec to point to the unified project's output paths rather than adding copy steps to bridge to the legacy path structure
