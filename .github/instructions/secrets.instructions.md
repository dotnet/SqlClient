---
applyTo: "**"
---
# Secrets and Credential Handling

This guide describes how to avoid committing secrets, how to write credential
placeholders that do **not** trip secret scanners (e.g. GitHub Advanced Security
/ 1ES push protection), and what to do when a push is blocked.

## Golden Rules

1. **Never commit a real secret** — passwords, connection strings with live
   credentials, access keys, SAS tokens, client secrets, certificates/PFX
   files, or bearer tokens. This applies to source, tests, docs, samples,
   scripts, pipeline YAML, and config files.
2. **Never route a secret through tooling or the model.** When a value is truly
   secret, have the user type it directly into their terminal or set it as an
   environment variable. Do not paste it into files, prompts, or chat.
3. **Prefer indirection over literals.** Read credentials from environment
   variables, a secret store (Azure Key Vault), user secrets, or CI secret
   variables — not from committed text.
4. **Assume public.** This repository mirrors to public GitHub via autosync.
   Anything committed is effectively public and permanent in history.

## Approved Placeholder Formats

When you need to show the *shape* of a connection string or credential in code,
docs, comments, or samples, use one of these placeholder styles. These are
recognized as non-secrets by the scanner:

| Style | Example | Use for |
|-------|---------|---------|
| Angle brackets | `User ID=<user>;Password=<pwd>` | Docs, READMEs, comments, samples |
| Descriptive angle brackets | `Password=<myPassword>`, `User Id=<AppId>` | Samples that name the value |
| Masked | `Password=********` or `Password=***` | Illustrative output / redaction |
| Env var expansion | `Password=${SA_PASSWORD}` (bash), `Password=$(SqlPwd)` (ADO) | Scripts and pipelines |
| Named token (docs prose) | `Password=<Secret>` | Narrative documentation |

### Do NOT use these placeholder styles

- **Ellipsis values** — `Password=...` or `User ID=...;Password=...`.
  The literal `...` after `Password=` is treated as a credential value and
  **will** trip `SEC101/037 SqlLegacyCredentials`. Use `<pwd>` instead.
- **Realistic-looking fake secrets** — `Password=P@ssw0rd123`,
  `Password=abc123def456`. Even fake values that look like real passwords can
  be flagged and set a bad example.
- **Bare word secrets** — `Password=secret`, `Password=mypassword` inside a
  connection-string literal. Prefer `<pwd>`.

### Full connection-string placeholder examples

```text
Server=<server>;Database=<db>;User ID=<user>;Password=<pwd>;TrustServerCertificate=true
Server=tcp:<servername>.database.windows.net;Database=<dbname>;Authentication=Active Directory Service Principal;User Id=<AppId>;Password=<Secret>
```

```bash
# Have the user set the value directly; never write the real value into a file:
export SNICLOSE_CONNSTR="Server=<server>;User ID=<user>;Password=<pwd>"
```

## Reading Secrets at Runtime (preferred patterns)

- **Environment variables**: read connection strings from an env var
  (e.g. `SNICLOSE_CONNSTR`) so the password never lands in a committed file.
- **SecureString / SqlCredential**: use `SqlCredential` and `SecureString`
  rather than embedding a password in the connection string.
- **Integrated auth**: prefer `Integrated Security=true` or an
  `Authentication=ActiveDirectory*` mode where no password is stored.
- **CI/CD**: reference pipeline secret variables (`$(mySecret)`), never inline
  literals in YAML.

## When a Push Is Blocked by Secret Scanning

Error shape: `VS403654:BypassableBlock ... push was rejected because it contains
one or more secrets` with a `SEC101/...` rule id and `commit`/`paths` details.

1. **Locate it.** Inspect the exact committed blob:
   `git show <commit>:<path>` and go to the reported line/columns.
2. **Determine real vs. false positive.**
   - *Real secret*: rotate/revoke it immediately, then remove it from the file.
     If it is in the tip commit only, amend; if it is deeper in **unshared**
     history, rewrite that history. Never rewrite commits already public.
   - *False positive* (a placeholder like `Password=...`): reword to an approved
     placeholder (`Password=<pwd>`) so future commits don't recur.
3. **Already-public / mirrored commits.** If the flagged content lives in a
   commit that is already on public GitHub (e.g. an autosync mirror replaying
   `github/main`), you cannot scrub that specific commit without rewriting
   shared/public history. For a confirmed false positive, **bypass** the block
   via the 1ES/Advanced Security push-protection flow
   (https://aka.ms/1esSecretScanning/PushProtectionBypassableBlock) with a clear
   reason, or dismiss the alert as *False positive*. Then land the placeholder
   reword going forward so new files don't trip the rule again.
4. **Never** disable secret scanning or use `--no-verify`-style bypasses to work
   around a *real* secret.

## Common Scanner Rules to Watch

| Rule | Triggers on |
|------|-------------|
| `SEC101/037 SqlLegacyCredentials` | `User ID=...;Password=<value>` connection-string shapes |
| `SEC101/*` (general) | Cloud keys, SAS tokens, client secrets, PATs, bearer tokens |

If in doubt, use an approved placeholder from the table above.
