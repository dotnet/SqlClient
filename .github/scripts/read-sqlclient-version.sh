#!/usr/bin/env bash

read_sqlclient_version() {
  local versions_file="${1:-${SQLCLIENT_VERSIONS_FILE:-src/Microsoft.Data.SqlClient/Versions.props}}"
  local version_property="${2:-${SQLCLIENT_VERSION_PROPERTY:-SqlClientNextVersion}}"
  local property_count
  local version

  if [[ ! -r "${versions_file}" ]]; then
    echo "::error::Unable to read SqlClient version file '${versions_file}'." >&2
    return 1
  fi
  if [[ ! "${version_property}" =~ ^[A-Za-z_][A-Za-z0-9_.-]*$ ]]; then
    echo "::error::Invalid version property name '${version_property}'." >&2
    return 1
  fi

  property_count=$(awk -v tag="<${version_property}>" '
    {
      remaining = $0
      while ((position = index(remaining, tag)) > 0) {
        count++
        remaining = substr(remaining, position + length(tag))
      }
    }
    END { print count + 0 }
  ' "${versions_file}")
  if [[ "${property_count}" != "1" ]]; then
    echo "::error::Expected exactly one ${version_property} in '${versions_file}'." >&2
    return 1
  fi

  if ! version=$(sed -nE "s/.*<${version_property}>([^<]*)<\\/${version_property}>.*/\\1/p" "${versions_file}"); then
    echo "::error::Unable to read ${version_property} from '${versions_file}'." >&2
    return 1
  fi
  if [[ -z "${version}" || "${version}" == *$'\n'* ]]; then
    echo "::error::Expected exactly one ${version_property} value in '${versions_file}'." >&2
    return 1
  fi

  printf '%s' "${version}"
}
