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

Parse `${input:scope}` for any organization, project, repository, branch or tag, variable-group
name or ID, or unused-marker override supplied by the user. Honor recognized scope values. Ask
for clarification rather than silently ignoring an ambiguous value.

A YAML pipeline can run the YAML from any branch or tag on which its YAML file exists. CI, PR,
scheduled, and pipeline-completion or resource triggers each select a ref, and a manual run can
select any ref, so neither trigger configuration nor run history bounds where a variable group
is consumed. Resolve repository and ref scope as follows:

1. Honor repositories, branches, and tags explicitly supplied by the user.
2. For each enabled or paused YAML pipeline, include every branch and tag in its repository on
   which the pipeline's YAML file exists.
3. Include repositories and refs referenced as templates from any included ref, such as
   `resources.repositories` refs and cross-repository `template` or `extends` references, since
   template files can declare variable groups.
4. A repository that no enabled or paused pipeline uses, directly or as a template source,
   contributes no YAML usage. Report any such user-supplied repository as having no pipeline
   roots rather than scanning it.
5. Exclude disabled and deleted pipelines unless the user explicitly includes them. Include
   paused pipelines, because they can run again once resumed.

The audit scope is incomplete when the user restricts repositories, branches, or tags, when the
run-capable refs of any pipeline cannot be fully enumerated, or when a template or group
reference cannot be resolved (see step 2). In any of these cases, report every variable group
that is not referenced within the audited scope as **usage unknown**, never as unused.

Before starting the audit, present the resolved organization, project, repositories, the number
of refs per repository and how they were derived, and any variable-group filter. State whether
the scope is complete; if it is not, state that unreferenced groups will be reported as usage
unknown.

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

Identify which groups appear to be marked unused and which are active. Treat a description as
marked unused when it contains the resolved unused marker, or when it contains both "unused" and
the stem "delet" (matching "delete", "deleted", and "deletion"), case-insensitively, because
the exact marker may vary. Ignore current descriptions when determining actual usage so they do
not bias the search.

### 2. Search YAML pipelines for variable-group references

For each enabled or paused YAML pipeline and each run-capable ref in scope:

1. Start from the pipeline definition's configured YAML path at that ref.
2. Follow every template reference transitively, including stage, job, step, and variable
   templates, `extends` templates, and templates from other repositories through
   `resources.repositories` aliases and their refs.
3. Retrieve each reached file's content without modifying the repository.
4. Search the reached files for every in-scope variable-group name.

Do not scan YAML files that are not reachable from an included pipeline's YAML path. Examples,
test fixtures, and files belonging to disabled or deleted pipelines must not count as usage.

Resolve template expressions from values fixed in the YAML. When a template path, repository
ref, or `group:` value depends on a parameter that declares a `values` list, evaluate every
allowed value. If it depends on anything else that cannot be resolved, mark that pipeline's
scope incomplete instead of scanning unrelated YAML.

Many refs share identical files. Fetch each distinct file version, identified by its object ID,
once and reuse the result for every ref that contains it.

Use **exact-match** patterns that prevent prefix false positives: searching for `Foo` must not
match `FooBar`. Recognize these YAML forms:

- `group: '<name>'`, with the name bounded by single quotes.
- `group: "<name>"`, with the name bounded by double quotes.
- `group: <name>`, with the value followed by end-of-line, whitespace, or `#`.

Handle pagination and transient throttling using the conventions of the active integration.
Retry transient failures with bounded exponential backoff.

### 3. Search Classic pipelines for variable-group references

Retrieve all Classic build and release definitions in scope, handling pagination and
loading expanded definition details when the initial listing does not contain variable-group
references.

Exclude disabled and deleted definitions. Include paused definitions, because they can run again
once resumed.

For build definitions, inspect all variable-group references associated with the definition.

For release definitions, inspect variable-group references at both:

- **Definition level**
- **Stage or environment level**

For every reference, record:

- Variable-group ID
- Pipeline name
- Pipeline type
- Stage or environment name, when applicable

Merge these results with the repository and ref results from step 2.

### 4. Summarize findings

Present a clear summary before making any changes. Include:

- **Used groups**: group name, ID, repository, branch, and/or Classic pipeline references, plus the proposed description.
- **Unused groups**: group name, ID, current description, and the proposed unused marker.
- **Surprise findings**: groups marked unused that are still referenced and should be unmarked.
- **No-change groups**: groups whose current descriptions already match the findings.
- **Usage unknown**: groups that cannot be classified because some relevant scope could not be
  inspected or was excluded by a user restriction.

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
  When a group is referenced on more than five refs in a repository, list five of those refs,
  putting the default branch first only if it is one of them, and summarize the rest as a
  count, for example `<repo>: <ref1>, <ref2>, <ref3>, <ref4>, <ref5> +12 refs`.
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
