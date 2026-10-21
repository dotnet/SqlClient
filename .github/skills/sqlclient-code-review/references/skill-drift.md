# Reporting skill drift

The reviewer is the only one who sees both this skill and what the code really
did. Capture the gap at review time; rebuilding it later from threads costs more.

Drift is about the skill, not the PR. Never put it in the PR review.

## When to report

- You retracted or downgraded a finding after checking it, especially one this
  skill told you to check.
- A defect got past the skill and CI, a human, or a later PR caught it.
- A fact in the skill is stale: a path, a check name, a pipeline filter, a
  policy rule.
- A step cost time without changing the outcome, or a finding class recurs that
  a lint, analyzer, test, or CI check could catch.

The bar: would a future review repeat the mistake? A one-off you could not have
anticipated is not drift.

## Output

Every review ends with a **Skill drift** line in the chat output: one line per
observation, or `none`. A missing section reads as "not checked".

**Security exception.** For suspected or confirmed vulnerabilities, write only
`Private MSRC reporting required`. Follow the MSRC process in `SECURITY.md` and
`CONTRIBUTING.md`. Do not put details in public issues, comments, review output,
or unattended logs, even redacted.

## Filing

Writing an issue or comment needs the same explicit authorization as posting a
review. Permission to post a PR review does not cover issue writes. Without it,
include the prepared report in chat and stop.

Use the host's designated issue/comment write tools. If the host requires
`create_issue`, use it rather than `gh issue create`; a generic tool failure is
not permission to switch channels. The CLI write examples below apply only when
the host permits that route. With any client, preserve the issue form's headings,
HTML comments, and field values; render form fields as `### Label` headings and
use exact dropdown options. A URL or available tool does not grant write authority.

1. Search first, including closed issues and comments, using terms for the
   drift mechanism, not only the local symbol:

   ```bash
   gh issue list -R dotnet/SqlClient --state all --limit 1000 \
     --search "\"[review-skill]\" in:title,body,comments <mechanism terms>"
   ```

   If results hit the limit, split by `created:` date ranges. A truncated or
   failed search does not prove there is no match.
2. If a match describes the same drift, add the report as a comment. Recurrence
   is what promotes an entry.

   ```bash
   gh issue comment <number> -R dotnet/SqlClient --body-file <path>
   ```

3. Otherwise file one issue per observation through the designated tool or,
   when permitted, interactively use the
   [Code Review Skill Drift form](../../../ISSUE_TEMPLATE/code_review_skill_drift.yml).
   The permitted CLI fallback does not apply forms, so supply their structure
   explicitly:

   ```bash
   gh issue create -R dotnet/SqlClient \
     --title "[review-skill] <one-line drift>" --body-file <path>
   ```

## Body

```markdown
<!-- Do not report security vulnerabilities here. Use the private MSRC process in SECURITY.md. -->
<!-- Redact connection strings, passwords, tokens, customer data, and non-public source from every field. -->
### Drift class
<one exact option from the form>
### Where it happened
<PR, thread, or comment URL; for a local review, base and head commit IDs>
### What the skill says today
<quote the line, or "nothing covers this">
### What actually turned out to be true
<the observation, in terms a future reviewer needs>
### Evidence
<redacted file:line, command and output, spec section, or settling thread>
### What it cost
<one exact option from the form>
### Prior occurrences
<links, or N/A>
### Proposed change
<section and replacement, automated check, or N/A>
```

Maintainers read these issues to propose skill edits. The skill has a size
budget, so a proposed addition should name what it replaces, or the check that
would make it unnecessary.
