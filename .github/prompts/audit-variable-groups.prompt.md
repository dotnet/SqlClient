---
name: audit-variable-groups
description: Audit Azure DevOps variable groups by searching repos/branches for usage and updating descriptions accordingly.
argument-hint: <optional: specific repos, branches, or variable groups to audit>
tools: ['execute/runInTerminal', 'execute/getTerminalOutput', 'edit/createFile', 'read/readFile']
---

Audit Azure DevOps variable groups in the **sqlclientdrivers** organization, **ADO.Net** project. Use the `az` CLI where possible; fall back to direct REST API calls where `az` doesn't provide sufficient coverage (e.g., repo file scanning, AzureKeyVault group updates).

## Safety constraints

- **Read-only by default.** Steps 1–4 are purely read-only (listing, searching, summarizing). Do NOT issue any write operations (`PUT`, `PATCH`, `POST`, `az pipelines variable-group update`, or any command that modifies state) until the user has explicitly approved changes in step 5.
- **No implicit approval.** Silence, ambiguous replies, or partial acknowledgements do NOT count as approval. You must receive an unambiguous "go" (or equivalent affirmative) from the user before proceeding to step 6.
- **Scope lock.** Only update variable group descriptions. Never delete variable groups, modify variables within a group, or change any pipeline definitions.
- **Dry-run first.** When presenting the summary in step 4, show the exact before/after description text for every group that would be modified so the user can verify the changes.
- **Abort on doubt.** If any step produces unexpected errors, ambiguous results, or data that contradicts expectations, stop and ask the user for guidance rather than proceeding.

## Inputs

If the user provided arguments, parse `${input:scope}` for overrides — it may contain specific repos, branches, or variable group names to scope the audit. Apply any recognized values as overrides to the defaults below; ignore unrecognized tokens.

The user may override any of the following defaults:

- **Organization**: `https://sqlclientdrivers.visualstudio.com`
- **Project**: `ADO.Net`
- **Repos & branches to search**:
  - `dotnet-sqlclient`: `internal/main`, `internal/release/7.0`, `internal/release/6.1`
  - `Microsoft.Data.SqlClient`: `ConfigFuzz`
  - `Microsoft.Data.SqlClient.Ctaip`: `certAuth`
  - `Microsoft.Data.SqlClient.sni`: `master`, `release/6.0`
- **Unused marker text**: `UNUSED - WILL BE DELETED SHORTLY`

## Workflow

### 1. List all variable groups

```
az pipelines variable-group list --org <ORG> --project <PROJECT> -o json
```

- Save the output.
- Identify which groups are already marked with the unused marker text and which are active.
- Use **fuzzy matching** when detecting the unused marker: check for the presence of both "unused" and "delete" (case-insensitive) in the description, since the actual marker text may vary (e.g. a person's name inserted before "WILL DELETE").
- In later steps, ignore the current group description when determining usage to avoid biasing the search results.

### 2. Search repos for variable group references

For each repo/branch combination, use the Azure DevOps REST API (Items endpoint) to:

1. List all files recursively in the repo at the given branch.
2. Filter to `.yml` and `.yaml` files.
3. Fetch the content of each YAML file.
4. Search for each variable group name using **exact-match** patterns that prevent prefix false positives (e.g., searching for `Foo` must not match `FooBar`). Match against these forms, ensuring the name is delimited by quotes or end-of-value (whitespace/newline/comment):
   - `group: '<name>'`  (single-quoted — name bounded by quotes)
   - `group: "<name>"`  (double-quoted — name bounded by quotes)
   - `group: <name>` followed by end-of-line, whitespace, or `#` (unquoted — no trailing alphanumeric characters)

Use a Bearer token from `az account get-access-token --resource "499b84ac-1321-427f-aa17-267ca6975798" --query accessToken -o tsv`.

REST API endpoints:
- **List items**: `{org}/{project}/_apis/git/repositories/{repo}/items?recursionLevel=Full&versionDescriptor.version={branch}&versionDescriptor.versionType=branch&api-version=7.1`
- **Get file content**: Same endpoint with `path={URL-encoded filePath}&$format=text` (URL-encode the `path` value; use `$format=text` to retrieve raw file content instead of JSON metadata)

Avoid cloning repos. Only use the REST API to fetch file listings and content.

**Performance & rate-limiting guidance**:
- Filter the file listing to paths likely to contain pipelines (e.g., `eng/`, `pipelines/`, or root-level YAML files) before fetching content, to reduce API calls.
- Add a short delay (e.g., 200ms) between file-content fetches to avoid hitting Azure DevOps rate limits.
- If a `429 Too Many Requests` or `503` response is received, back off exponentially (1s, 2s, 4s, …) and retry up to 3 times before logging a warning and moving on.

### 3. Search Classic pipelines for variable group references

Query all **enabled** Classic build and release pipeline definitions for variable group usage.

#### Classic Build pipelines

```
GET {org}/{project}/_apis/build/definitions?api-version=7.1
```

- Page through all results (`$top` / `continuationToken` if needed).
- Keep only definitions where `queueStatus` is **`enabled`**.
- For each enabled definition, fetch its full JSON:
  ```
  GET {org}/{project}/_apis/build/definitions/{id}?api-version=7.1
  ```
- Inspect the `variableGroups` array; each element has an `id` that maps to a variable group ID.

#### Classic Release pipelines

The Release API lives on the `vsrm.` sub-domain, but direct REST calls to that sub-domain may fail with SSL certificate errors for `.visualstudio.com` organizations. Use `az devops invoke` instead:

```
az devops invoke --area release --resource definitions \
  --org <ORG> --route-parameters project=<PROJECT> \
  --query-parameters '$expand=environments' '$top=200' \
  -o json
```

- Page through results using `continuation_token` if present in the response.
- Exclude definitions where `isDeleted` is `true`.
- Each definition can reference variable groups at two levels:
  - **Definition level**: `variableGroups` array on the root object.
  - **Stage/environment level**: each element in `environments` has its own `variableGroups` array.
- Collect all referenced variable group IDs from both levels.

#### Recording results

For every variable group ID found, record:
- The pipeline **name** and **type** (Build / Release).
- The stage name (for release-environment-level references).

Merge these results with the repo/branch search results from step 2 so the summary in the next step covers both YAML and Classic usage.

### 4. Summarize findings

Present a clear summary table to the user **before making any changes**. The summary must include:

- **Used groups**: group name, ID, which repos/branches and/or Classic pipelines reference it, and the proposed new description.
- **Unused groups**: group name, ID, current description, and confirmation it will be marked with the unused marker.
- **Surprise findings**: any group already marked unused that is actually still referenced (these should be un-marked).
- **No-change groups**: groups already marked unused and confirmed unused.

### 5. Prompt for go/no-go

**This is a mandatory gate — do NOT skip or auto-approve.**

Ask the user to confirm before applying any changes. Use the ask-questions tool to present the options so the user must actively choose. Offer options:
- **Apply all** — apply every proposed change from step 4
- **Apply subset** — let the user specify which groups to update (by name or ID)
- **Export script** — write all update commands to a shell script file for the user to inspect and run manually. Do NOT execute any updates. Save the script to a path the user specifies (default: `./audit-variable-group-updates.sh`). The script must include the full `az` CLI commands and REST API `curl` fallbacks (for AzureKeyVault-type groups) with comments identifying each group by name and ID. Mark the file executable.
- **Abort** — make no changes at all

Do NOT proceed to step 6 unless the user selects "Apply all" or "Apply subset" and, for the subset case, clearly identifies which groups to update. If the user selects "Export script", generate the file and stop — do NOT execute step 6. If the user says "abort" or does not respond, stop here.

### 6. Apply description updates

**Prerequisites**: Step 5 must have completed with explicit user approval. If you have not received approval, do NOT execute this step.

For each group that needs updating, try:

```
az pipelines variable-group update --group-id <ID> --description "<new description>" --org <ORG> --project <PROJECT> --detect false
```

**Fallback for AzureKeyVault-type groups**: The `az pipelines variable-group update` command may fail with 500 errors on groups whose `type` is `AzureKeyVault` (it tries to refresh the vault connection). When this happens, fall back to the REST API:

1. `GET {org}/{project}/_apis/distributedtask/variablegroups/{id}?api-version=7.1`
2. Update **both** the top-level `description` field **and** every entry in `variableGroupProjectReferences[].description` in the JSON.
3. `PUT` the modified JSON back to the same URL with `Content-Type: application/json`.

**Description rules**:
- **Used groups**: Prepend `[Used by: <repo>: <branch1>, <branch2>; <repo2>: <branch>; Classic/<type>: <pipeline name>] ` to the existing description (after stripping any previous `[Used by: ...]` prefix). `<type>` is `Build` or `Release`.
- **Unused groups**: Set description to the unused marker text.
- **Incorrectly marked unused**: Replace the unused marker with `[Used by: ...]`.
- **Already correct**: Skip groups whose description would not change.

Report the outcome of each update (success/failure) and a final tally.

## Error handling

- If a repo or branch does not exist or returns an error, log a warning and continue with the remaining repos/branches.
- If a variable group update fails, log the error and continue with the remaining updates.
- At the end, report any failures so the user can address them manually.
