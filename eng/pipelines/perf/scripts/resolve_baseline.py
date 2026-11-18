"""Resolve the highest stable Microsoft.Data.SqlClient version available in the governed feed."""

import json
import re
import sys
from pathlib import Path
from urllib.error import URLError
from urllib.request import urlopen
import xml.etree.ElementTree as ET


NUGET_CONFIG = Path(__file__).resolve().parents[4] / "NuGet.config"


def read_json(url):
    """Fetch a NuGet service resource with a bounded timeout."""
    with urlopen(url, timeout=30) as response:
        return json.load(response)


def governed_package_index(config):
    """Read the governed source from NuGet.config and discover its package-list endpoint."""
    source = ET.parse(config).find("./packageSources/add[@key='governed']")
    if source is None or not source.get("value"):
        raise ValueError("NuGet.config must define the governed package source")
    service = read_json(source.get("value"))
    resources = service.get("resources") if isinstance(service, dict) else None
    if not isinstance(resources, list):
        raise ValueError("Governed feed service index must contain a resources array")
    for resource in resources:
        if (isinstance(resource, dict)
                and resource.get("@type") == "PackageBaseAddress/3.0.0"
                and isinstance(resource.get("@id"), str)):
            return resource["@id"].rstrip("/") + "/microsoft.data.sqlclient/index.json"
    raise ValueError("Governed feed does not expose PackageBaseAddress/3.0.0")


def latest_stable(index):
    """Select stable versions numerically, excluding prerelease and metadata suffixes."""
    versions = index.get("versions") if isinstance(index, dict) else None
    if not isinstance(versions, list) or any(not isinstance(v, str) for v in versions):
        raise ValueError("NuGet package index must contain a versions array of strings")
    stable = [v for v in versions if re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", v)]
    if not stable:
        raise ValueError("NuGet package index contains no stable Microsoft.Data.SqlClient version")
    return max(stable, key=lambda v: tuple(int(part) for part in v.split(".")))


def main():
    """Fetch the index with a bounded timeout and fail explicitly if resolution fails."""
    try:
        version = latest_stable(read_json(governed_package_index(NUGET_CONFIG)))
    except (URLError, OSError, ValueError, ET.ParseError) as error:
        print(f"Failed to resolve latest stable Microsoft.Data.SqlClient: {error}", file=sys.stderr)
        return 1
    print(version)
    return 0


if __name__ == "__main__":
    sys.exit(main())
