using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Data.SqlClient.Analysis.AsyncRoslyn;

/// <summary>
/// Shared helpers for resolving the containing member of a node, its async
/// modifier, and a single-line source snippet.
/// </summary>
internal static class SyntaxHelpers
{
    /// <summary>
    /// Walks ancestors to build a "Type.Member" name for the member that
    /// contains <paramref name="node"/>, and reports whether it is async.
    /// </summary>
    public static (string? Container, bool IsAsync) ResolveContainer(SyntaxNode node)
    {
        string? memberName = null;
        bool isAsync = false;

        for (SyntaxNode? current = node; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case MethodDeclarationSyntax m when memberName is null:
                    memberName = m.Identifier.Text;
                    isAsync = HasAsync(m.Modifiers);
                    break;
                case LocalFunctionStatementSyntax lf when memberName is null:
                    memberName = lf.Identifier.Text;
                    isAsync = HasAsync(lf.Modifiers);
                    break;
                case ConstructorDeclarationSyntax c when memberName is null:
                    memberName = ".ctor";
                    break;
                case AccessorDeclarationSyntax a when memberName is null:
                    memberName = a.Keyword.Text;
                    isAsync = HasAsync(a.Modifiers);
                    break;
                case PropertyDeclarationSyntax p when memberName is null:
                    memberName = p.Identifier.Text;
                    break;
                case TypeDeclarationSyntax t:
                    memberName = memberName is null ? t.Identifier.Text : $"{t.Identifier.Text}.{memberName}";
                    break;
            }
        }

        return (memberName, isAsync);
    }

    private static bool HasAsync(SyntaxTokenList modifiers)
    {
        return modifiers.Any(SyntaxKind.AsyncKeyword);
    }

    /// <summary>Returns the trimmed text of the source line containing the node.</summary>
    public static string Snippet(SyntaxNode node, SourceText text)
    {
        int line = node.GetLocation().GetLineSpan().StartLinePosition.Line;
        if (line < 0 || line >= text.Lines.Count)
        {
            return string.Empty;
        }

        return text.Lines[line].ToString().Trim();
    }

    /// <summary>Extracts the simple (rightmost) name invoked by an expression.</summary>
    public static string? InvokedName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            GenericNameSyntax g => g.Identifier.Text,
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            MemberBindingExpressionSyntax mb => mb.Name.Identifier.Text,
            _ => null,
        };
    }
}

/// <summary>Base walker that records findings against one build configuration.</summary>
internal abstract class FindingWalker : CSharpSyntaxWalker
{
    protected FindingWalker(string kind, string file, SourceText text, List<Finding> sink)
    {
        Kind = kind;
        File = file;
        Text = text;
        Sink = sink;
    }

    protected string Kind { get; }

    protected string File { get; }

    protected SourceText Text { get; }

    protected List<Finding> Sink { get; }

    protected void Add(SyntaxNode node, string detail)
    {
        FileLinePositionSpan span = node.GetLocation().GetLineSpan();
        (string? container, bool isAsync) = SyntaxHelpers.ResolveContainer(node);

        Sink.Add(new Finding
        {
            Kind = Kind,
            File = File,
            Line = span.StartLinePosition.Line + 1,
            Column = span.StartLinePosition.Character + 1,
            Container = container,
            ContainerIsAsync = isAsync,
            Detail = detail,
            Snippet = SyntaxHelpers.Snippet(node, Text),
        });
    }
}

/// <summary>Finds invocations of a configured set of target method names.</summary>
internal sealed class CallSiteWalker : FindingWalker
{
    private readonly HashSet<string> _targets;

    public CallSiteWalker(IEnumerable<string> targets, string file, SourceText text, List<Finding> sink)
        : base("call-site", file, text, sink)
    {
        _targets = new HashSet<string>(targets, StringComparer.Ordinal);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        string? name = SyntaxHelpers.InvokedName(node.Expression);
        if (name is not null && _targets.Contains(name))
        {
            Add(node, name);
        }

        base.VisitInvocationExpression(node);
    }
}

/// <summary>
/// Flags sync-over-async patterns: <c>.Result</c>, <c>.Wait()</c>,
/// <c>.GetAwaiter().GetResult()</c>, and <c>.RunSynchronously()</c>.
/// </summary>
internal sealed class SyncOverAsyncWalker : FindingWalker
{
    public SyncOverAsyncWalker(string file, SourceText text, List<Finding> sink)
        : base("sync-over-async", file, text, sink)
    {
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // Only flag ".Result" reads, not assignments or declarations named Result.
        if (node.Name.Identifier.Text == "Result"
            && node.Parent is not InvocationExpressionSyntax)
        {
            Add(node, ".Result");
        }

        base.VisitMemberAccessExpression(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Expression is MemberAccessExpressionSyntax ma)
        {
            string name = ma.Name.Identifier.Text;
            if (name == "Wait" && node.ArgumentList.Arguments.Count <= 1)
            {
                Add(node, ".Wait()");
            }
            else if (name == "RunSynchronously")
            {
                Add(node, ".RunSynchronously()");
            }
            else if (name == "GetResult"
                && ma.Expression is InvocationExpressionSyntax inner
                && inner.Expression is MemberAccessExpressionSyntax innerMa
                && innerMa.Name.Identifier.Text == "GetAwaiter")
            {
                Add(node, ".GetAwaiter().GetResult()");
            }
        }

        base.VisitInvocationExpression(node);
    }
}

/// <summary>
/// Flags blocking synchronization that sits on (or under) the async path:
/// <c>lock</c> statements, <c>Monitor.Enter/TryEnter</c>, and
/// <c>WaitHandle.WaitOne</c> / <c>SemaphoreSlim.Wait</c> blocking calls.
/// </summary>
internal sealed class BlockingWalker : FindingWalker
{
    public BlockingWalker(string file, SourceText text, List<Finding> sink)
        : base("blocking-sync", file, text, sink)
    {
    }

    public override void VisitLockStatement(LockStatementSyntax node)
    {
        string detail = node.Expression is ThisExpressionSyntax ? "lock(this)" : "lock";
        Add(node, detail);
        base.VisitLockStatement(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Expression is MemberAccessExpressionSyntax ma)
        {
            string name = ma.Name.Identifier.Text;
            string? receiver = (ma.Expression as IdentifierNameSyntax)?.Identifier.Text
                ?? (ma.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.Text;

            if (receiver == "Monitor" && (name == "Enter" || name == "TryEnter" || name == "Exit"))
            {
                Add(node, $"Monitor.{name}");
            }
            else if (name == "WaitOne")
            {
                Add(node, ".WaitOne()");
            }
            else if (name == "Wait" && receiver is not null
                && receiver.Contains("emaphore", StringComparison.Ordinal))
            {
                Add(node, $"{receiver}.Wait() (blocking)");
            }
        }

        base.VisitInvocationExpression(node);
    }
}

/// <summary>
/// Flags per-call heap allocations on the read/replay path: <c>new byte[]</c>,
/// <c>new char[]</c>, and <c>new TaskCompletionSource(...)</c>.
/// </summary>
internal sealed class AllocationWalker : FindingWalker
{
    public AllocationWalker(string file, SourceText text, List<Finding> sink)
        : base("allocation", file, text, sink)
    {
    }

    public override void VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
    {
        if (node.Type.ElementType is PredefinedTypeSyntax pt)
        {
            string kw = pt.Keyword.Text;
            if (kw == "byte" || kw == "char")
            {
                Add(node, $"new {kw}[]");
            }
        }

        base.VisitArrayCreationExpression(node);
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        string? typeName = (node.Type as IdentifierNameSyntax)?.Identifier.Text
            ?? (node.Type as GenericNameSyntax)?.Identifier.Text;

        if (typeName is not null && typeName.StartsWith("TaskCompletionSource", StringComparison.Ordinal))
        {
            Add(node, "new TaskCompletionSource");
        }

        base.VisitObjectCreationExpression(node);
    }
}

/// <summary>Flags <c>await</c> expressions missing <c>.ConfigureAwait(false)</c>.</summary>
internal sealed class ConfigureAwaitWalker : FindingWalker
{
    public ConfigureAwaitWalker(string file, SourceText text, List<Finding> sink)
        : base("missing-configureawait", file, text, sink)
    {
    }

    public override void VisitAwaitExpression(AwaitExpressionSyntax node)
    {
        if (!IsConfigured(node.Expression))
        {
            Add(node, "await without ConfigureAwait(false)");
        }

        base.VisitAwaitExpression(node);
    }

    private static bool IsConfigured(ExpressionSyntax awaited)
    {
        return awaited is InvocationExpressionSyntax invocation
            && invocation.Expression is MemberAccessExpressionSyntax ma
            && ma.Name.Identifier.Text == "ConfigureAwait";
    }
}
