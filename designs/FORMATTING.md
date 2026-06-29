# Formatting Rules for Plan Documents

All markdown files under `plans/` must follow these rules and pass `markdownlint-cli2` with the
config in `plans/.markdownlint.jsonc`.

## Linting

### Installing markdownlint-cli2

`markdownlint-cli2` is a Node.js package. Install it with:

```bash
sudo apt update && sudo apt install -y nodejs npm
sudo npm install -g markdownlint-cli2
```

If you already have `npm`, skip the `apt` step.

### Running markdownlint-cli2

Run the linter before committing:

```bash
markdownlint-cli2 "plans/async-performance/**/*.md"
```

The VS Code **markdownlint** extension uses the same engine and config file automatically.

The config sets:

- **MD007** unordered list indent to 2 spaces per level
- **MD013** line length to 100 (code blocks and tables exempt)
- **MD029** ordered list style to `:ordered` (sequential: 1, 2, 3)
- **MD033** disabled (inline HTML allowed for generic type syntax like `IAsyncEnumerable<T>` in
  headings)

## Line Length

- **Max line width: 100 characters**
- Wrap paragraph text and list items at 100 chars
- Do NOT wrap these elements (even if over 100 chars):
  - Table rows
  - Headings
  - Fenced code blocks (``` or ~~~)
  - Lines containing a single indivisible token (e.g., a backtick-wrapped file path or a markdown
    link)
  - Horizontal rules

## Encoding

- **UTF-8**, no BOM
- **LF** line endings (no CR or CRLF)
- Files must end with exactly one trailing newline

## Markdown Structure

These rules correspond to markdownlint rules that are enforced:

- **Blank lines around headings** (MD022) — always put an empty line before and after `#` headings
- **Blank lines around lists** (MD032) — always put an empty line before the first list item and
  after the last
- **Blank lines around fenced code blocks** (MD031) — always put an empty line before ``` and after
  the closing ```
- **Ordered list numbering** (MD029) — use sequential numbers (1, 2, 3), not all `1.`
- **No trailing punctuation in headings** (MD026) — don't end headings with `:` or `.`
- **ATX headings only** (MD003) — use `#` style, not underline (setext) style
- **No duplicate sibling headings** (MD024) — differentiate repeated heading text (e.g.,
  "Recommendation" under each priority should be unique like "P1 Recommendation")
- **No multiple consecutive blank lines** — use a single blank line as separator

## Wrapping Details

- List continuation lines align to the content start of the list marker (e.g., 2 spaces past `-` or
  `1.`)
- Preserve existing indentation levels
- Do not break words or hyphenated terms across lines
- Markdown links `[text](url)` are indivisible — never split a link across two lines

## AI Agent Instructions

When generating or editing markdown files under `plans/`, fill lines to the **100-character
maximum**. A common mistake is wrapping at ~80 characters (a typical editor default). To avoid this:

1. **Set your wrap target to exactly 100.** Do not use 80. The MD013 rule in
   `.markdownlint.jsonc` enforces 100.
2. **Fill each line as close to 100 as possible** before wrapping to the next line. If the next word
   fits within 100 characters, it belongs on the current line.
3. **Check your output.** If most paragraph lines end around column 75–85, you are wrapping too
   short. Re-wrap to 100.
4. **Treat markdown links as single tokens.** Never break `[display text](url)` across lines. If a
   link won't fit on the current line, move the entire link to the next line.
5. **Keep metadata lines separate.** Lines like `**Priority:** High` and `**Complexity:** Low` are
   standalone key-value pairs — do not merge them into the preceding or following paragraph.
