---
name: doc-comments
description: Generate XML documentation comments for C# code following .NET best practices.
argument-hint: <code>
agent: agent
tools: ['edit/editFiles', 'read/readFile']
---

You are an expert .NET developer and technical writer. Your task is to generate high-quality XML documentation comments for the following C# code.

${input:code}

Follow these best practices and guidelines derived from standard .NET documentation conventions:

### 1. Standard XML Tags
- **`<summary>`**: Provide a clear, concise description of the type or member. Start with a verb in the third person (e.g., "Gets", "Sets", "Initializes", "Calculates", "Determines"). For `const` fields, explicitly mention the value or unit in the description (e.g., "The cache expiration time (2 hours).").
- **`<param name="name">`**: Describe each parameter, including its purpose and any specific constraints (e.g., "cannot be null").
- **`<returns>`**: Describe the return value for non-void methods.
- **`<exception cref="ExceptionType">`**: Document specific exceptions that the method is known to throw, especially those validation-related (like `ArgumentNullException`).
- **`<value>`**: Use this for property descriptions to describe the value stored in the property.
- **`<remarks>`**: Use for additional details, implementation notes, or complex usage scenarios that don't fit in the summary.
- **`<inheritdoc/>`**: Use this tag when the member overrides a base member or implements an interface member and the documentation should be inherited.

### 2. Formatting and References
- **Code References**: Use `<see cref="TargetType"/>` or `<see cref="TargetMember"/>` to reference other types or members within the documentation.
- **Keywords**: Use `<see langword="keyword"/>` for C# keywords (e.g., `<see langword="true"/>`, `<see langword="false"/>`, `<see langword="null"/>`, `<see langword="async"/>`).
- **Inline Code**: Use `<c>` tags for literal values or short inline code snippets (e.g., `<c>0</c>`).
- **Paragraphs**: Use `<para>` tags to separate paragraphs within `<summary>` or `<remarks>` for readability.

### 3. Writing Style
- **Focus on Intent**: Do not start summaries with "A helper class...", "A wrapper for...", or "An instance of...". Instead, describe the specific role or responsibility of the type (e.g., "Uniquely identifies a client application configuration..." instead of "A key class...").
- **Completeness**: Use complete sentences ending with a period.
- **Properties**: 
  - For `get; set;` properties: "Gets or sets..."
  - For `get;` properties: "Gets..."
  - Boolean properties: "Gets a value indicating whether..."
- **Constructors**: "Initializes a new instance of the <see cref="ClassName"/> class."
- **Avoid Content-Free Comments**: Do not simply repeat the name of the member (e.g., avoid "Gets the count" for `Count`; instead use "Gets the number of elements in the collection.").

### 4. Analysis
- **Exceptions**: Analyze the method body to identify thrown exceptions and document them using `<exception>` tags.
- **Nullability**: Explicitly mention nullability constraints in parameter descriptions.

### 5. Repository Constraints
- **Public APIs**: Do **not** generate inline XML documentation comments for `public` members of `public` types. These are documented via external XML files using `<include>` tags.
- **Internal Implementation**: **Do** generate inline XML documentation for:
  - Non-public types and members (`internal`, `private`, `protected`).
  - `public` members within non-public types (e.g. a `public` method inside an `internal` class).

**Output:**
Return the provided C# code with the generated XML documentation annotations inserted above the corresponding elements. Maintain existing indentation and code structure.