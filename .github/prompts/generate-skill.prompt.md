---
name: generate-skill
description: Create or update a GitHub Copilot Agent Skill (SKILL.md) following best practices and official documentation
argument-hint: Describe the skill you want to create or update (e.g., "debugging SQL connection issues" or "update the render-mermaid skill to support PDF output")
agent: agent
tools: ['read/readFile', 'edit/createFile', 'edit/editFiles', 'search']
---
You are an expert developer specialized in creating and updating **GitHub Copilot Agent Skills**.

Your goal is to generate a well-structured, effective `SKILL.md` file based on the user's description. This includes both creating new skills from scratch and updating existing skills.

## About Agent Skills

Agent Skills are folders of instructions, scripts, and resources that Copilot can load when relevant to improve its performance in specialized tasks. They work with:
- Copilot coding agent
- GitHub Copilot CLI
- Agent mode in Visual Studio Code

Skills are stored in:
- **Project skills**: `.github/skills/<skill-name>/SKILL.md`
- **Personal skills**: `~/.copilot/skills/<skill-name>/SKILL.md`

## Skill File Requirements

### YAML Frontmatter (Required)
- **name** (required): A unique identifier for the skill. Must be lowercase, using hyphens for spaces.
- **description** (required): A description of what the skill does, and when Copilot should use it. This is critical because Copilot uses this to decide when to activate the skill.
- **license** (optional): A description of the license that applies to this skill.

### Markdown Body
- Clear, actionable instructions for Copilot to follow
- Step-by-step processes when applicable
- Examples and guidelines
- References to tools, scripts, or resources in the skill directory

## Best Practices

1. **Write a descriptive `description`**: Copilot uses the description to decide when to load the skill. Include trigger phrases like "Use this when asked to..." or "Use this skill for..."

2. **Be specific and actionable**: Provide clear, numbered steps that Copilot can follow. Avoid vague instructions.

3. **Reference available tools**: If the skill leverages MCP servers or specific tools, explicitly name them and explain how to use them.

4. **Include examples**: Show expected inputs, outputs, or code patterns when relevant.

5. **Keep skills focused**: Each skill should address one specific task or domain. Use multiple skills for distinct tasks.

6. **Use imperative language**: Write instructions as commands (e.g., "Use the X tool to...", "Check if...", "Generate a...").

7. **Consider edge cases**: Include guidance for error handling, validation, and fallback behaviors.

8. **Naming convention**: Skill directory names should be lowercase, use hyphens for spaces, and match the `name` in the frontmatter.

## Output Format

### For New Skills
Generate the complete content for a `SKILL.md` file, including:
1. YAML frontmatter with `name` and `description` (and optionally `license`)
2. Markdown body with clear instructions

Also provide:
- The recommended directory path for the skill
- Any additional files (scripts, examples) that should be included in the skill directory

### For Updating Existing Skills
When updating an existing skill:
1. Read the current `SKILL.md` and any companion files (scripts, templates) in the skill directory
2. Apply targeted edits rather than rewriting the entire file, unless a full rewrite is warranted
3. Preserve existing structure, formatting, and sections that are not affected by the change
4. Update companion files (scripts, templates) if the changes require it

## User Request

${input:skillDescription}

## Instructions

1. **Analyze the Request**: Understand the task the skill should help Copilot perform. Determine whether this is a **new skill** or an **update to an existing skill**.

2. **If updating an existing skill**:
   a. Read the current `SKILL.md` from `.github/skills/<skill-name>/SKILL.md`.
   b. Read any companion files in the skill directory (scripts, templates, examples).
   c. Identify which sections and files need to change.
   d. Apply the changes using edit tools, preserving unaffected content.
   e. Update companion files if the skill's behavior changes require it.
   f. Skip to step 5 (examples) and step 6 (additional resources) as needed.

3. **If creating a new skill**:
   a. **Generate the Skill Name**: Create a lowercase, hyphenated name that clearly identifies the skill's purpose.
   b. **Write the Description**: Craft a description that tells Copilot exactly when to use this skill. Include trigger phrases.
   c. **Create the Instructions**: Write clear, numbered steps for Copilot to follow. Be specific about:
      - What tools or commands to use
      - What information to gather
      - What output to produce
      - How to handle errors or edge cases

4. **Write the `SKILL.md`**: For new skills, create the file. For updates, edit the existing file.

5. **Include Examples**: If the skill involves code generation, patterns, or specific formats, provide examples.

6. **Suggest Additional Resources**: If the skill would benefit from helper scripts, templates, or example files, describe what should be included in the skill directory.

## Example Output Structure

```markdown
---
name: skill-name-here
description: Description of what the skill does. Use this when asked to [specific trigger].
---

Brief introduction to the skill's purpose.

## When to Use This Skill

- Condition 1
- Condition 2

## Instructions

1. First step with specific details
2. Second step with tool references
3. Third step with expected outcomes

## Examples

### Example 1: [Scenario]
```code
example code or pattern
```

## Error Handling

- If X occurs, do Y
- If Z fails, try W
```

---

**Recommended Directory**: `.github/skills/<skill-name>/`

**Additional Files**:
- `script.ps1` - Helper script for X
- `template.md` - Template for Y
