# Issue 3148 Investigation App

This folder contains a small standalone diagnostic app for investigating the
`0x8007007E` `Microsoft.Data.SqlClient.SNI.dll` load failure discussed in
issue `#3148`.

The app is aimed at the path-ordering hypothesis:

- show the current process architecture
- show the runtime architecture
- show the `PATH` entries that contain `dotnet`
- resolve `dotnet` from `PATH` the same way process launch does
- optionally relaunch itself with x86 `dotnet` ordered before x64 `dotnet` on Windows
- optionally try `SqlConnection.Open()` if a connection string is provided

## Build

```bash
dotnet build investigations/issue-3148/Issue3148PathProbe.csproj
```

## Run diagnostics only

```bash
dotnet run --project investigations/issue-3148/Issue3148PathProbe.csproj
```

## Run with a connection attempt

```bash
dotnet run --project investigations/issue-3148/Issue3148PathProbe.csproj -- \
  --connection-string "Server=...;Database=...;User ID=...;Password=...;Encrypt=True;TrustServerCertificate=True"
```

## Force Windows PATH ordering

This mode is intended to actively test the hypothesis behind issue `#3148`.
On Windows, the probe starts a fresh child process with `Program Files (x86)\\dotnet`
placed before `Program Files\\dotnet` in `PATH`, then runs the same diagnostics and
optional connection test in that child.

This mode must be run as a framework-dependent app through `dotnet`, for example via
`dotnet run` or `dotnet Issue3148PathProbe.dll`. It does not validate host selection for
published executables directly.

```bash
dotnet run --project investigations/issue-3148/Issue3148PathProbe.csproj -- \
  --force-x86-dotnet-first
```

You can combine it with a connection attempt:

```bash
dotnet run --project investigations/issue-3148/Issue3148PathProbe.csproj -- \
  --force-x86-dotnet-first \
  --connection-string "Server=...;Database=...;User ID=...;Password=...;Encrypt=True;TrustServerCertificate=True"
```

## Publish a Windows single-file probe

This is useful if you want to mirror the original report more closely.

```bash
dotnet publish investigations/issue-3148/Issue3148PathProbe.csproj \
  -c Release \
  -r win-x64 \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true
```

## What to look for

- If the first `dotnet` resolved from `PATH` is under `Program Files (x86)`,
  that supports the path-ordering suspicion.
- If `--force-x86-dotnet-first` reproduces the failure in the child process,
  that is strong evidence that launcher/runtime selection is part of the issue.
- If the process architecture and runtime architecture do not match the intended
  deployment, that is another strong signal.
- If `SqlConnection.Open()` fails, the app prints the full exception chain so
  the loader error code can be captured.