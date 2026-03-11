FROM mcr.microsoft.com/devcontainers/dotnet:10.0-noble

# Install .NET 8.0 and 9.0 sdks (needed to build and run tests targeting these TFMs).
# The base image ships the latest .NET SDK (10.0) which can build for older TFMs,
# but the matching runtimes are required to *execute* those targets.
COPY --from=mcr.microsoft.com/dotnet/sdk:8.0 /usr/share/dotnet/shared /usr/share/dotnet/shared
COPY --from=mcr.microsoft.com/dotnet/sdk:9.0 /usr/share/dotnet/shared /usr/share/dotnet/shared

# Install go-sqlcmd (standalone binary, works on Ubuntu without dependency issues).
ARG SQLCMD_VERSION=v1.9.0
ARG SQLCMD_SHA256_amd64="b896bac71cf6ad8feca125bb42be61999116d8566a078ed475893372ccf8c0f2"
ARG SQLCMD_SHA256_arm64="e5de98d635bfecf337acadf9d84a8f4f3cdcf38b2978b9dcd83aed20425c64ef"
RUN arch="$(dpkg --print-architecture)" \
    && case "$arch" in \
        amd64) SQLCMD_ARCH="linux-amd64" ;; \
        arm64) SQLCMD_ARCH="linux-arm64" ;; \
        *) echo "Unsupported architecture: $arch" && exit 1 ;; \
    esac \
    && curl -fsSL "https://github.com/microsoft/go-sqlcmd/releases/download/${SQLCMD_VERSION}/sqlcmd-${SQLCMD_ARCH}.tar.bz2" \
       -o /tmp/sqlcmd.tar.bz2 \
    && if [ "$arch" = "amd64" ]; then echo "$SQLCMD_SHA256_amd64 /tmp/sqlcmd.tar.bz2" | sha256sum -c -; fi \
    && if [ "$arch" = "arm64" ]; then echo "$SQLCMD_SHA256_arm64 /tmp/sqlcmd.tar.bz2" | sha256sum -c -; fi \
    && tar xjf /tmp/sqlcmd.tar.bz2 -C /usr/local/bin/ sqlcmd \
    && chmod +x /usr/local/bin/sqlcmd \
    && rm -f /tmp/sqlcmd.tar.bz2
