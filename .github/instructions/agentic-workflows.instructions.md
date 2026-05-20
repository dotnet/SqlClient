---
applyTo: ".github/workflows/**/*.md"
description: Rules for editing gh-aw agentic workflow Markdown files.
---

# Agentic Workflow Edit Rules (`gh aw`)

This repository authors GitHub Actions agentic workflows in Markdown using
[`gh aw`](https://github.com/githubnext/gh-aw). Each workflow `.md` file under
`.github/workflows/` compiles to a sibling `.lock.yml`, and **only the
`.lock.yml` is executed by GitHub Actions at runtime.**

## Mandatory rule

Whenever you create, edit, rename, or delete a file matching
`.github/workflows/**/*.md`, you **MUST**, in the **same commit / PR**:

1. Run `gh aw compile` from the repository root.
2. Stage and commit the regenerated sibling `<name>.lock.yml`.
3. If you deleted a workflow `.md`, also delete its `.lock.yml`.

If the `.lock.yml` is stale or missing, the workflow fails at runtime
(see PR #4279 for the exact failure mode). The
`Verify gh aw lock files` CI check will block the PR in that case.

## How to verify locally

```bash
gh aw compile
git status        # both the .md and .lock.yml should appear
gh aw compile     # second run must be a no-op (clean diff)
```

## Code-review checklist

When reviewing a PR that touches `.github/workflows/**/*.md`:

- [ ] A matching `.lock.yml` is updated in the same PR.
- [ ] `gh aw compile` produces no further diff on top of the PR.
- [ ] If new tools, network endpoints, or permissions are added in the `.md`,
      they are present in the regenerated `.lock.yml`.

## Out of scope

- Do **not** hand-edit `.lock.yml` files. They are generated; edit the `.md`
  source and recompile.
- For deeper authoring guidance (creating, debugging, upgrading workflows),
  invoke the `agentic-workflows` agent at
  `.github/agents/agentic-workflows.agent.md`.
