FROM mcr.microsoft.com/devcontainers/dotnet:10.0-preview-trixie-slim

# Install .NET 8.0 and 9.0 runtimes (needed to run tests targeting these TFMs).
# The base image ships the latest .NET SDK (10.0) which can build for older TFMs,
# but the matching runtimes are required to *execute* those targets.
COPY --from=mcr.microsoft.com/dotnet/runtime:8.0 /usr/share/dotnet/shared /usr/share/dotnet/shared
COPY --from=mcr.microsoft.com/dotnet/runtime:9.0 /usr/share/dotnet/shared /usr/share/dotnet/shared

# Install go-sqlcmd (standalone binary, works on Debian Trixie without dependency issues).
RUN arch="$(dpkg --print-architecture)" \
    && case "$arch" in \
        amd64) SQLCMD_ARCH="linux-amd64" ;; \
        arm64) SQLCMD_ARCH="linux-arm64" ;; \
        *) echo "Unsupported architecture: $arch" && exit 1 ;; \
    esac \
    && curl -fsSL "https://github.com/microsoft/go-sqlcmd/releases/latest/download/sqlcmd-${SQLCMD_ARCH}.tar.bz2" \
       -o /tmp/sqlcmd.tar.bz2 \
    && tar xjf /tmp/sqlcmd.tar.bz2 -C /usr/local/bin/ sqlcmd \
    && chmod +x /usr/local/bin/sqlcmd \
    && rm -f /tmp/sqlcmd.tar.bz2
