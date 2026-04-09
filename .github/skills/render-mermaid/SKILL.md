---
name: render-mermaid
description: Renders Mermaid diagram text into ASCII art or SVG using the beautiful-mermaid library. Use this skill when asked to render, visualize, or convert Mermaid diagrams; when replacing mermaid code blocks with rendered output in markdown files; or when generating ASCII or SVG versions of flowcharts, sequence diagrams, or other Mermaid diagrams.
---

This skill renders Mermaid diagram definitions into ASCII art or SVG using the [beautiful-mermaid](https://github.com/lukilabs/beautiful-mermaid) library. When a rendered diagram replaces a mermaid code block in a document, the original mermaid source is preserved in a companion `.mmd` file so it can be edited and regenerated later.

## When to Use This Skill

- User asks to render, visualize, or display a Mermaid diagram
- User wants ASCII art or SVG output from a Mermaid definition
- User asks to replace mermaid code blocks in a markdown file with rendered output
- User asks to regenerate diagrams from `.mmd` source files
- User mentions "beautiful-mermaid" or wants pretty mermaid rendering

## Prerequisites

- Node.js 18+ must be available
- The `beautiful-mermaid` package will be installed on first use via `npx`

## Output Formats and File Extensions

| Format | Extension | Best For |
|--------|-----------|----------|
| `ascii` | `.txt` | Terminal display, plain-text docs, inline code blocks |
| `svg` | `.svg` | Slideshows, web pages, markdown image embeds, high-resolution output |

Always use the correct file extension for the chosen format:
- `--format ascii --output diagrams/flow.txt`
- `--format svg --output diagrams/flow.svg`

When replacing mermaid blocks in a document, choose the format based on the document type:
- **Marp / slideshow presentations**: Use **SVG** and embed with `![alt](diagrams/name.svg)`.
- **README or plain markdown**: Use **ASCII** in a fenced code block, or **SVG** as an image reference.
- **Terminal / stdout display**: Use **ASCII**.

## Context-Aware Diagram Formatting

When rendering diagrams for a specific context (e.g., slides, posters, documentation), **adapt the mermaid source to fit the target medium** before rendering. Only change formatting and layout — never change the meaning or logical content of the diagram.

### Slideshow / Presentation Optimization

Slides have limited vertical and horizontal space (~20–25 visible lines, ~80 characters wide). Before rendering a diagram for a slide:

1. **Prefer horizontal flow.** Use `flowchart LR` instead of `flowchart TD` when the diagram has ≤6 nodes in the main path. Horizontal flow uses less vertical space.

2. **Shorten node labels.** Replace verbose labels with concise equivalents:
   - `"Connection returned to pool"` → `"Return to pool"`
   - `"TransactionCompleted event fires"` → `"TxCompleted fires"`
   - Remove redundant context that the slide title already provides.

3. **Reduce line breaks in labels.** Replace `\n` line breaks with shorter single-line text where possible. Each `\n` adds a full line of height.

4. **Collapse linear chains.** If three nodes form a straight chain with no branches (`A --> B --> C`), consider whether the middle node can be removed or merged:
   - Keep it if it represents a meaningful decision or state.
   - Remove it if it's purely transitional (e.g., "Process starts" → "Do thing" → "Process ends" can become "Do thing").

5. **Split overly complex diagrams.** If a diagram has more than ~10 nodes, consider splitting it into two slides rather than squeezing everything onto one.

6. **Use subgraphs sparingly.** Each subgraph heading adds a line. Only use subgraphs when they are essential for grouping.

### Rules for Formatting Changes

**Allowed changes** (formatting only):
- Switching flow direction (`TD` ↔ `LR`)
- Shortening or rewording labels (same meaning)
- Removing decorative line breaks (`\n`) in labels
- Adjusting node shapes (`[]` vs `{}` vs `()`)
- Removing purely cosmetic subgraph wrappers
- Splitting one diagram into two for space

**Forbidden changes** (alter meaning):
- Adding or removing nodes
- Adding or removing edges
- Changing the direction of edges
- Changing decision outcomes (Yes/No labels)
- Removing conditional branches
- Changing node types in a way that alters semantics (e.g., decision `{}` → plain `[]`)

When you modify a `.mmd` file for a specific context, save the **adapted version** as the `.mmd` file (it is the source of truth for that rendering). If the user later wants the original un-adapted version, they can regenerate it from the mermaid block in the source document or version control.

## Instructions

### Rendering a Single Diagram

1. **Identify the mermaid source.** This may be:
   - Inline text provided by the user
   - A fenced `mermaid` code block in a markdown file
   - A `.mmd` file on disk

2. **Determine the output format.** Default to `ascii` unless the user explicitly requests `svg` or the target context implies it (e.g., slideshows → SVG).

3. **Check the target context.** If the diagram is being rendered for a specific medium (slideshow, documentation page, etc.), apply the formatting guidelines from the "Context-Aware Diagram Formatting" section above. Edit the `.mmd` source before rendering.

4. **Write the mermaid source to a `.mmd` file** if it isn't already in one. Place it alongside the target document:
   ```
   <document-dir>/diagrams/<descriptive-name>.mmd
   ```
   Use a descriptive kebab-case name derived from the diagram content (e.g., `return-paths-flow.mmd`, `pspe-sequence.mmd`).

5. **Run the render script** with the correct format and file extension:
   ```bash
   # ASCII → .txt
   node .github/skills/render-mermaid/render.mjs diagrams/flow.mmd --format ascii --output diagrams/flow.txt

   # SVG → .svg
   node .github/skills/render-mermaid/render.mjs diagrams/flow.mmd --format svg --output diagrams/flow.svg
   ```

6. **Capture the output** and use it as needed (insert into document, display to user, etc.).

### Replacing Mermaid Blocks in a Markdown File

When the user asks to render mermaid diagrams within a markdown file:

1. **Find all fenced mermaid code blocks** in the target file:
   ````
   ```mermaid
   flowchart LR
       A --> B
   ```
   ````

2. **Determine the document context.** Check the file's purpose:
   - Has `marp: true` frontmatter → **slideshow** → use SVG, apply slide formatting rules.
   - Is a README or documentation page → choose format based on user preference or default to SVG for image embeds.
   - Is displayed in terminal → use ASCII.

3. **For each mermaid block:**
   a. Extract the mermaid source text.
   b. Generate a descriptive filename from the diagram (e.g., from the first line or a heading above it).
   c. If the context requires formatting adaptation (e.g., slides), edit the mermaid source per the formatting guidelines. Keep changes formatting-only.
   d. Write the (possibly adapted) source to `<document-dir>/diagrams/<name>.mmd`.
   e. Run the render script with the appropriate format and file extension.
   f. Replace the fenced mermaid block in the document with:
      - **For SVG**: An image reference: `![Descriptive Alt Text](diagrams/<name>.svg)`
      - **For ASCII**: A fenced code block containing the ASCII art
   g. Add a comment linking back to the source file:
      ```markdown
      <!-- Diagram source: diagrams/<name>.mmd -->
      ```

4. **Preserve the originals.** Never delete the `.mmd` files. They are the editable source of truth.

### Regenerating Diagrams

When the user asks to regenerate or update diagrams:

1. **Find all `.mmd` files** in the relevant `diagrams/` directory.
2. **Check what rendered outputs already exist** (`.txt` and/or `.svg` siblings) to determine which formats to regenerate.
3. **Re-run the render script** for each `.mmd` file, producing the same format(s) as before.
4. **Update the rendered output** in the corresponding markdown file by finding the `<!-- Diagram source: ... -->` comment and replacing the image reference or code block above it.

## File Organization

```
document-directory/
├── my-document.md              # Contains rendered output + source comments
└── diagrams/
    ├── architecture-overview.mmd   # Editable mermaid source
    ├── architecture-overview.svg   # SVG render (for slides / image embeds)
    ├── architecture-overview.txt   # ASCII render (for code blocks / terminal)
    ├── return-paths-flow.mmd
    ├── return-paths-flow.svg
    └── sequence-diagram.mmd
```

## Error Handling

- If `npx beautiful-mermaid` fails to install, instruct the user to run `npm install -g beautiful-mermaid` manually.
- If a diagram has syntax errors, report the error from the render script and do NOT replace the original mermaid block. Show the error to the user.
- If Node.js is not available, inform the user that Node.js 18+ is required and provide installation guidance.
- If the render script times out (>30 seconds), the diagram may be too complex. Suggest simplifying it.
