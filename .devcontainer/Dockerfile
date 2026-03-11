FROM mcr.microsoft.com/devcontainers/dotnet:latest

# Install .NET 8.0 and 9.0 runtimes (needed to run tests targeting these TFMs).
# The base image ships the latest .NET SDK (10.0) which can build for older TFMs,
# but the matching runtimes are required to *execute* those targets.
COPY --from=mcr.microsoft.com/dotnet/runtime:8.0 /usr/share/dotnet/shared /usr/share/dotnet/shared
COPY --from=mcr.microsoft.com/dotnet/runtime:9.0 /usr/share/dotnet/shared /usr/share/dotnet/shared
