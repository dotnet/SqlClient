# C# Coding Style

We follow a similar coding style as [dotnet/runtime](https://github.com/dotnet/runtime).

An [EditorConfig](https://editorconfig.org "EditorConfig homepage") file (`.editorconfig`) has been
provided at the root of the sqlclient repository, enabling C# auto-formatting conforming to the
guidelines below.

For non code files (xml, etc), our current best guidance is consistency. When editing files, keep
new code and changes consistent with the style in the files. For new files, it should conform to the
style for that component. If there is a completely new component, anything that is reasonably
broadly accepted is fine.

The general rule we follow is "_use Visual Studio defaults_".

1. Text files should be UTF-8 encoded, no BOM, and use Unix line endings (LF).  The exception is
   Windows script files, which may use CRLF.
1. Lines should be a maximum of 100 characters.  Exceptions are made for long content such as URLs,
   and when breaking text would be less readable.
1. We use [Allman style](http://en.wikipedia.org/wiki/Indent_style#Allman_style) braces, where each
   brace begins on a new line. A single line statement block can go without braces but the block
   must be properly indented on its own line and must not be nested in other statement blocks that
   use braces. One exception is that a `using` statement is permitted
   to be nested within another `using` statement by starting on the following line at the same
   indentation level, even if the nested `using` contains a controlled block.
1. We use four spaces of indentation (no tabs).
1. We use `_camelCase` for internal and private fields and use `readonly` where possible. Prefix
   internal and private instance fields with `_`, static fields with `s_` and thread static fields
   with `t_`. When used on static fields, `readonly` should come after `static` (e.g. `static
   readonly` not `readonly static`).  Public fields should be used sparingly and should use
   PascalCasing with no prefix when used.
1. We avoid `this.` unless absolutely necessary.
1. We always specify the visibility, even if it's the default (e.g. `private string _foo` not
   `string _foo`). Visibility should be the first modifier (e.g. `public abstract` not `abstract
   public`).
1. Namespace imports should be specified at the top of the file, *outside* of `namespace`
   declarations, and should be sorted alphabetically, with the exception of `System.*` namespaces,
   which are to be placed on top of all others.
1. Avoid more than one empty line at any time. For example, do not have two blank lines between
   members of a type.
1. Avoid spurious free spaces. For example avoid `if (someVar == 0)...`, where the dots mark the
   spurious free spaces. Consider enabling "View White Space (Ctrl+R, Ctrl+W)" or "Edit -> Advanced
   -> View White Space" if using Visual Studio to aid detection.
1. If a file happens to differ in style from these guidelines (e.g. private members are named
   `m_member` rather than `_member`), the existing style in that file takes precedence.
1. We only use `var` when it's obvious what the variable type is (e.g. `var stream = new
   FileStream(...)` not `var stream = OpenStandardInput()`).
1. We use language keywords instead of BCL types (e.g. `int, string, float` instead of `Int32,
   String, Single`, etc) for both type references as well as method calls (e.g. `int.Parse` instead
   of `Int32.Parse`).
1. We use PascalCasing to name all our constant local variables and fields. The only exception is
   for interop code where the constant value should exactly match the name and value of the code
   you are calling via interop.
1. We use ```nameof(...)``` instead of ```"..."``` whenever possible and relevant.
1. Fields should be specified at the top within type declarations.
1. When including non-ASCII characters in the source code use Unicode escape sequences (\uXXXX)
   instead of literal characters. Literal non-ASCII characters occasionally get garbled by a tool
   or editor.
1. When using labels (for goto), indent the label one less than the current indentation.
1. When using a single-statement if, we follow these conventions:
   - Never use single-line form (for example: `if (source == null) throw new
     ArgumentNullException("source");`)
   - Using braces is always accepted, and required if any block of an `if`/`else if`/.../`else`
     compound statement uses braces or if a single statement body spans multiple lines.
   - Braces may be omitted only if the body of *every* block associated with an `if`/`else
     if`/.../`else` compound statement is placed on a single line.
