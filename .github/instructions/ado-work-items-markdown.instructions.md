---
applyTo: "**"
---
# Azure DevOps Work Items: Markdown Description Rules

Use this guide whenever creating or updating Azure DevOps work items that include rich text in `System.Description`.

## Goals

- Ensure descriptions render as Markdown (not HTML/plain text)
- Preserve newline characters and list structure
- Verify work items after every batch update

## Tool Access

Use whatever Azure DevOps access is available, in this order of preference:

1. A **previously established preference** — if the user has already chosen a mechanism in this
   conversation, in memory/instructions, or by explicit request, keep using it.
2. An **Azure DevOps MCP server**, if one is connected (activate its work item tools if the host
   requires activation first).
3. The **`az` CLI** (`az rest`, `az boards`).
4. **Direct ADO REST** calls over HTTPS with a bearer token.

Probe availability rather than assuming, fall back to the next option when one is unavailable or
errors, and keep using whichever works for the rest of the run.

The rules below are about *request shape*, not about which client sends it. They apply equally to
an MCP tool call and to `az rest`. If the MCP tool in use cannot set
`multilineFieldsFormat.System.Description`, fall back to `az rest` for that update — the format
field is what makes the description render as Markdown.

## Required Behavior

1. Set the description content type through any supported client, provided the request can set
   `multilineFieldsFormat.System.Description`.
2. Use `application/json-patch+json` for PATCH requests.
3. Set `multilineFieldsFormat.System.Description` to `markdown`.
4. Preserve exact newlines in the Markdown body.
5. Verify both format and newline integrity after updates.

## Authentication and Resource

When using the `az` CLI or direct REST, use the Azure DevOps resource audience. An MCP server
handles its own authentication, so skip this step in that case.

- Resource: `499b84ac-1321-427f-aa17-267ca6975798`

Example auth check:

```bash
az rest \
  --method GET \
  --resource 499b84ac-1321-427f-aa17-267ca6975798 \
  --url "https://dev.azure.com/<org>/_apis/projects?api-version=7.1-preview.4"
```

Note: API versions can differ by endpoint. The examples below use `7.1-preview.3` for work item PATCH calls, while this auth check uses `7.1-preview.4`.

## Safe Update Pattern (Prevents Type/Value Errors)

Some work items reject a direct type switch unless a valid value is provided. Use this two-step process:

### Step 1: Capture current description

```bash
az boards work-item show --id <id> | jq -j '.fields["System.Description"] // ""' > ./desc-backup.txt
```

> **NOTE**: Writing to a file preserves all characters including trailing newlines.
> Command substitution (`$(...)`) silently strips trailing newlines, which would
> corrupt multi-line Markdown content.

### Step 2: Force markdown type with temporary empty value

```bash
PATCH_STEP1_FILE="${PATCH_STEP1_FILE:-./patch-step1.json}"

jq -n '[
  {"op":"replace","path":"/fields/System.Description","value":""},
  {"op":"replace","path":"/multilineFieldsFormat/System.Description","value":"markdown"}
]' >"$PATCH_STEP1_FILE"

az rest \
  --method PATCH \
  --resource 499b84ac-1321-427f-aa17-267ca6975798 \
  --url "https://dev.azure.com/<org>/<project>/_apis/wit/workitems/<id>?api-version=7.1-preview.3" \
  --headers "Content-Type=application/json-patch+json" \
  --body @"$PATCH_STEP1_FILE"
```

### Step 3: Restore exact Markdown text

```bash
PATCH_STEP2_FILE="${PATCH_STEP2_FILE:-./patch-step2.json}"

jq -n --rawfile d ./desc-backup.txt '[
  {"op":"replace","path":"/fields/System.Description","value":$d}
]' >"$PATCH_STEP2_FILE"

az rest \
  --method PATCH \
  --resource 499b84ac-1321-427f-aa17-267ca6975798 \
  --url "https://dev.azure.com/<org>/<project>/_apis/wit/workitems/<id>?api-version=7.1-preview.3" \
  --headers "Content-Type=application/json-patch+json" \
  --body @"$PATCH_STEP2_FILE"
```

## Newline Integrity Checks

After updates, confirm newline characters are still present and structure was not flattened. The
commands below use the `az` CLI; an equivalent MCP work item read that exposes
`multilineFieldsFormat` and the raw description works just as well.

### Check format and description sample

```bash
az boards work-item show --id <id> | jq '.multilineFieldsFormat, .fields["System.Description"][0:200]'
```

Expected:

- `multilineFieldsFormat.System.Description == "markdown"`
- Description text contains `\n` where line breaks are expected

### Check line count did not collapse

```bash
az boards work-item show --id <id> \
| jq -r '.fields["System.Description"]' \
| awk 'END { print NR }'
```

If a multi-line description unexpectedly returns `1`, newline content was likely lost.

## Batch Verification Script

Use this after bulk updates:

```bash
python3 - <<'PY'
import json, subprocess
ids = [12345, 12346]  # replace with your target IDs
bad = []
for i in ids:
    out = subprocess.check_output(["az", "boards", "work-item", "show", "--id", str(i)], text=True)
    j = json.loads(out)
    fmt = (j.get("multilineFieldsFormat") or {}).get("System.Description")
    desc = j.get("fields", {}).get("System.Description") or ""
    if fmt != "markdown" or "\n" not in desc:
        bad.append((i, fmt, "has_newlines" if "\n" in desc else "missing_newlines"))
print("noncompliant:", len(bad))
for row in bad:
    print(row)
PY
```

## Common Failure Modes

- `401` or `TF400813`: wrong token audience or insufficient auth context
- `Content-Type ... not supported`: must use `application/json-patch+json`
- `type changed without a value`: use two-step pattern (empty + markdown type, then restore text)
