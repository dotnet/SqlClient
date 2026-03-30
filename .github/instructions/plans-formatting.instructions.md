---
applyTo: "plans/**/*.md"
---
# Formatting Rules for Plan Documents

All markdown files under `plans/` must follow the rules in `plans/FORMATTING.md` and pass
`markdownlint-cli2` with the config at `plans/.markdownlint.jsonc`.

## Mandatory Workflow

1. Read `plans/FORMATTING.md` before creating or editing any markdown file under `plans/`.
2. After making edits, run the linter and fix all errors before finishing:

   ```bash
   markdownlint-cli2 "plans/**/*.md"
   ```

## Key Rules

- **Line width: 100 characters** — not 80. Fill lines close to 100 before wrapping.
- Blank lines required around: headings (MD022), lists (MD032), fenced code blocks (MD031).
- Fenced code blocks must have a language specified (MD040) — e.g., `csharp`, `text`, `bash`.
- Ordered lists use sequential numbers (1, 2, 3), not all `1.` (MD029).
- No trailing punctuation (`:` or `.`) in headings (MD026).
- No duplicate sibling headings — make each unique (MD024).
- Table pipes must have consistent spacing (MD060).
- Markdown links `[text](url)` are indivisible — never split across lines.
- Do not let issue references (e.g., `#3356`) start a line — this triggers MD018 (no-missing-space-atx).
- Metadata lines (`**Priority:** High`) are standalone key-value pairs — do not merge into paragraphs.
- UTF-8 encoding, LF line endings, single trailing newline.
