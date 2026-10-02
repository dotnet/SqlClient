---
name: audit-variable-groups
description: Audit Azure DevOps variable groups by locating their usage and proposing description updates.
argument-hint: <optional organization, project, repository, branch, or variable-group scope>
# No `tools:` scoping on purpose. This prompt is access-agnostic and should use
# whatever authenticated Azure DevOps capabilities are available.
---

Audit Azure DevOps variable groups within the requested scope.

Resolve the target organization and project using this precedence:

1. Values explicitly supplied by the user.
2. Context already established in the conversation.
3. The current authenticated Azure DevOps or workspace context.
4. If the target remains ambiguous, ask the user to select it before continuing.

Use the user's established access preference when one exists. Otherwise, use any available
authenticated capability that can perform the required operation. The workflow describes
operations and expected results rather than requiring a specific client, protocol, extension,
or command-line interface.

## Safety constraints

- **Read-only by default.** Steps 1-4 are purely read-only. Do not perform any operation that modifies state until the user has explicitly approved changes in step 5.
- **No implicit approval.** Silence, ambiguous replies, or partial acknowledgements do not count as approval. Receive an unambiguous affirmative selection before proceeding to step 6.
- **Scope lock.** Only update variable group descriptions. Never delete variable groups, modify variables within a group, change provider configuration, or change pipeline definitions.
- **Dry-run first.** In step 4, show the exact current and proposed description for every group that would be modified.
- **Abort on doubt.** If results are ambiguous or contradictory, stop and ask the user for guidance.

## Inputs and scope

Parse `${input:scope}` for any organization, project, repository, branch, variable-group name or
ID, run-history lookback window, or unused-marker override supplied by the user. Honor
recognized scope values. Ask for clarification rather than silently ignoring an ambiguous value.

A YAML pipeline runs the YAML from whichever branch triggered it, so a variable group can be
referenced on any branch a pipeline runs from, not only its default branch. Resolve repository
and branch scope as follows:

1. Honor repositories and branches explicitly supplied by the user.
2. For each enabled YAML pipeline, include its default branch.
3. Include existing branches that match the pipeline's CI and PR trigger filters (include
   patterns minus exclude patterns), taken from its YAML and from any trigger overrides on the
   pipeline definition.
4. Include branches the pipeline ran from within a lookback window. Default to 90 days unless
   the user supplies another window.
5. Include repositories and refs referenced as templates, such as `resources.repositories` refs
   and cross-repository `template` or `extends` references, since template files can declare
   variable groups.
6. For in-scope repositories that no enabled pipeline uses, use the configured default branch.
7. Exclude repositories or branches associated only with disabled, deleted, paused, or retired
   pipelines unless the user explicitly includes them.

If a pipeline's trigger filters or run history cannot be determined, treat its branch scope as
incomplete: report every variable group that pipeline could reference as **usage unknown**,
never as unused.

Before starting the audit, present the resolved organization, project, repositories, branches,
lookback window, and any variable-group filter, indicating which branches were derived from
triggers, run history, or template references. Clearly state that branches not included in the
resolved scope are not covered by the audit.

Use the user-provided unused-marker text when supplied. Otherwise, preserve an existing
project-standard marker if one can be identified consistently; if not, use
`UNUSED - PENDING DELETION`.

## Workflow

### 1. List all variable groups

Retrieve every variable group in scope, handling pagination and collecting at least:

- ID
- Name
- Description
- Type
- Project references, when available

Identify which groups appear to be marked unused and which are active. Use **fuzzy matching** for
an existing unused marker: check for both "unused" and "delete", case-insensitively, because the
exact marker may vary. Ignore current descriptions when determining actual usage so they do not
bias the search.

### 2. Search repositories for variable-group references

For each repository and branch in scope:

1. Enumerate files recursively at the selected branch revision.
2. Restrict candidates to `.yml` and `.yaml` files.
3. Retrieve each candidate file's content without modifying the repository.
4. Search for every in-scope variable-group name.

Use **exact-match** patterns that prevent prefix false positives: searching for `Foo` must not
match `FooBar`. Recognize these YAML forms:

- `group: '<name>'`, with the name bounded by single quotes.
- `group: "<name>"`, with the name bounded by double quotes.
- `group: <name>`, with the value followed by end-of-line, whitespace, or `#`.

Prefer likely pipeline locations when a repository is large, but do not omit root-level YAML
files. Handle pagination and transient throttling using the conventions of the active
integration. Retry transient failures with bounded exponential backoff.

### 3. Search Classic pipelines for variable-group references

Retrieve all enabled Classic build and release definitions in scope, handling pagination and
loading expanded definition details when the initial listing does not contain variable-group
references.

Exclude definitions that are disabled, paused, deleted, or retired.

For build definitions, inspect all variable-group references associated with the definition.

For release definitions, inspect variable-group references at both:

- **Definition level**
- **Stage or environment level**

For every reference, record:

- Variable-group ID
- Pipeline name
- Pipeline type
- Stage or environment name, when applicable

Merge these results with the repository and branch results from step 2.

### 4. Summarize findings

Present a clear summary before making any changes. Include:

- **Used groups**: group name, ID, repository, branch, and/or Classic pipeline references, plus the proposed description.
- **Unused groups**: group name, ID, current description, and the proposed unused marker.
- **Surprise findings**: groups marked unused that are still referenced and should be unmarked.
- **No-change groups**: groups whose current descriptions already match the findings.
- **Usage unknown**: groups that cannot be classified because some relevant scope could not be inspected.

Show the exact current and proposed description for every group that would change. Never classify
a group as unused when any relevant repository, branch, pipeline family, or result page could not
be inspected.

### 5. Prompt for go/no-go

**This is a mandatory gate. Do not skip or auto-approve it.**

Ask the user to choose one of:

- **Apply all** - apply every proposed change from step 4.
- **Apply subset** - let the user specify groups to update by name or ID.
- **Export update plan** - write a non-executing remediation artifact for inspection. Include each variable-group ID, current description, proposed description, and intended operation. Use JSON by default or another format explicitly requested by the user. Do not apply changes.
- **Abort** - make no changes.

Do not proceed to step 6 unless the user selects **Apply all** or **Apply subset**. For a subset,
require the user to identify the groups clearly. If the user selects **Export update plan**,
create the artifact and stop without applying changes. If the user aborts or does not respond,
stop without making changes.

### 6. Apply approved description updates

**Prerequisite:** Step 5 must have completed with explicit approval.

For every approved group:

1. Retrieve its complete current representation immediately before updating it.
2. Change only its description.
3. Preserve its type, variables, provider configuration, project references, and every other setting.
4. If description metadata is duplicated in project references, update those descriptions consistently as part of the same logical change.
5. Verify the persisted description after the update.
6. Report success or failure for that group.

Some variable-group types cannot be safely changed through a simplified update operation. In
those cases, use an available operation that preserves and resubmits the complete resource
representation. Never reconstruct omitted fields or replace a resource with a partial
representation.

Apply these description rules:

- **Used groups**: prepend `[Used by: <repo>: <branch1>, <branch2>; <repo2>: <branch>; Classic/<type>: <pipeline name>] ` to the existing description after stripping any previous `[Used by: ...]` prefix. `<type>` is `Build` or `Release`.
- **Unused groups**: set the description to the resolved unused marker.
- **Incorrectly marked unused**: replace the unused marker with `[Used by: ...]`.
- **Already correct**: skip groups whose description would not change.

Report each update result and a final tally.

## Error handling

- Retry throttling and transient service failures according to the active integration's guidance.
- If a repository, branch, pipeline family, or result page cannot be inspected, mark the affected audit scope as incomplete.
- Never treat an inspection failure as evidence that a variable group is unused.
- Report affected groups as **usage unknown** when incomplete scope could contain a reference.
- If an approved update fails, continue with independent updates and report the exact failure without claiming complete success.
