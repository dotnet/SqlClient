---
applyTo: "**"
---
# Azure DevOps Work Items: Markdown Description Rules

Use this guide whenever creating or updating Azure DevOps work items that include rich text in `System.Description`.

## Goals

- Ensure descriptions render as Markdown (not HTML/plain text)
- Preserve newline characters and list structure
- Verify work items after every batch update

## Required Behavior

1. Always use `az rest` for description content-type changes.
2. Use `application/json-patch+json` for PATCH requests.
3. Set `multilineFieldsFormat.System.Description` to `markdown`.
4. Preserve exact newlines in the Markdown body.
5. Verify both format and newline integrity after updates.

## Authentication and Resource

Use Azure DevOps resource audience when calling `az rest`:

- Resource: `499b84ac-1321-427f-aa17-267ca6975798`

Example auth check:

```bash
az rest \
  --method GET \
  --resource 499b84ac-1321-427f-aa17-267ca6975798 \
  --url "https://dev.azure.com/<org>/_apis/projects?api-version=7.1-preview.4"
```

## Safe Update Pattern (Prevents Type/Value Errors)

Some work items reject a direct type switch unless a valid value is provided. Use this two-step process:

### Step 1: Capture current description

```bash
desc=$(az boards work-item show --id <id> | jq -r '.fields["System.Description"] // ""')
```

### Step 2: Force markdown type with temporary empty value

```bash
jq -n '[
  {"op":"replace","path":"/fields/System.Description","value":""},
  {"op":"replace","path":"/multilineFieldsFormat/System.Description","value":"markdown"}
]' >/tmp/patch-step1.json

az rest \
  --method PATCH \
  --resource 499b84ac-1321-427f-aa17-267ca6975798 \
  --url "https://dev.azure.com/<org>/<project>/_apis/wit/workitems/<id>?api-version=7.1-preview.3" \
  --headers "Content-Type=application/json-patch+json" \
  --body @/tmp/patch-step1.json
```

### Step 3: Restore exact Markdown text

```bash
jq -n --arg d "$desc" '[
  {"op":"replace","path":"/fields/System.Description","value":$d}
]' >/tmp/patch-step2.json

az rest \
  --method PATCH \
  --resource 499b84ac-1321-427f-aa17-267ca6975798 \
  --url "https://dev.azure.com/<org>/<project>/_apis/wit/workitems/<id>?api-version=7.1-preview.3" \
  --headers "Content-Type=application/json-patch+json" \
  --body @/tmp/patch-step2.json
```

## Newline Integrity Checks

After updates, confirm newline characters are still present and structure was not flattened.

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
ids = [44787, 44794]  # replace with your target IDs
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
