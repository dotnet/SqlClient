// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/SqlClientCommandAfter/*'/>
public sealed class SqlClientCommandAfter : System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/Name/*'/>
    public const string Name = "Microsoft.Data.SqlClient.WriteCommandAfter";
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
    public System.Guid OperationId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
    public string Operation => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
    public long Timestamp => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/ConnectionId/*'/>
    public System.Guid? ConnectionId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/TransactionId/*'/>
    public long? TransactionId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/Command/*'/>
    public SqlCommand Command => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/Statistics/*'/>
    public System.Collections.IDictionary Statistics => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
    public int Count => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
    public System.Collections.Generic.KeyValuePair<string, object> this[int index] => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    public System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<string, object>> GetEnumerator() => throw null;
}

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandBefore"]/SqlClientCommandBefore/*'/>
public sealed class SqlClientCommandBefore : System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandBefore"]/SqlClientCommandBefore/*'/>
    public const string Name = "Microsoft.Data.SqlClient.WriteCommandBefore";
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
    public System.Guid OperationId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
    public string Operation => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
    public long Timestamp => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandBefore"]/ConnectionId/*'/>
    public System.Guid? ConnectionId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandBefore"]/TransactionId/*'/>
    public long? TransactionId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandBefore"]/Command/*'/>
    public SqlCommand Command => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
    public int Count => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
    public System.Collections.Generic.KeyValuePair<string, object> this[int index] => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    public System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<string, object>> GetEnumerator() => throw null;
}

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/SqlClientCommandError/*'/>
public sealed class SqlClientCommandError : System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/SqlClientCommandError/*'/>
    public const string Name = "Microsoft.Data.SqlClient.WriteCommandError";
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
    public System.Guid OperationId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
    public string Operation => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
    public long Timestamp => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/ConnectionId/*'/>
    public System.Guid? ConnectionId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/TransactionId/*'/>
    public long? TransactionId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/Command/*'/>
    public SqlCommand Command => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/Exception/*'/>
    public System.Exception Exception { get; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
    public int Count => throw null;
    /// <inheritdoc/>>/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
    public System.Collections.Generic.KeyValuePair<string, object> this[int index] => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    public System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<string, object>> GetEnumerator() => throw null;
}

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseAfter"]/SqlClientConnectionCloseAfter/*'/>
public sealed class SqlClientConnectionCloseAfter : System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseAfter"]/Name/*'/>
    public const string Name = "Microsoft.Data.SqlClient.WriteConnectionCloseAfter";
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
    public System.Guid OperationId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
    public string Operation => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
    public long Timestamp => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseAfter"]/ConnectionId/*'/>
    public System.Guid? ConnectionId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseAfter"]/Connection/*'/>
    public SqlConnection Connection => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseAfter"]/Statistics/*'/>
    public System.Collections.IDictionary Statistics => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
    public int Count => throw null;
    /// <inheritdoc/>>/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
    public System.Collections.Generic.KeyValuePair<string, object> this[int index] => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    public System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<string, object>> GetEnumerator() => throw null;
}

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseBefore"]/SqlClientConnectionCloseBefore/*'/>
public sealed class SqlClientConnectionCloseBefore : System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseBefore"]//*'/>
    public const string Name = "Microsoft.Data.SqlClient.WriteConnectionCloseBefore";
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
    public System.Guid OperationId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
    public string Operation => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
    public long Timestamp => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseBefore"]/ConnectionId/*'/>
    public System.Guid? ConnectionId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseBefore"]/Connection/*'/>
    public SqlConnection Connection => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseBefore"]/Statistics/*'/>
    public System.Collections.IDictionary Statistics => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
    public int Count => throw null;
    /// <inheritdoc/>>/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
    public System.Collections.Generic.KeyValuePair<string, object> this[int index] => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    public System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<string, object>> GetEnumerator() => throw null;
}

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/SqlClientConnectionCloseError/*'/>
public sealed class SqlClientConnectionCloseError : System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/Name/*'/>
    public const string Name = "Microsoft.Data.SqlClient.WriteConnectionCloseError";
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
    public System.Guid OperationId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
    public string Operation => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
    public long Timestamp => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/ConnectionId/*'/>
    public System.Guid? ConnectionId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/Connection/*'/>
    public SqlConnection Connection => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/Statistics/*'/>
    public System.Collections.IDictionary Statistics => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/Exception/*'/>
    public System.Exception Exception => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
    public int Count => throw null;
    /// <inheritdoc/>>/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
    public System.Collections.Generic.KeyValuePair<string, object> this[int index] => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    public System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<string, object>> GetEnumerator() => throw null;
}

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/SqlClientConnectionOpenAfter/*'/>
public sealed class SqlClientConnectionOpenAfter : System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/Name/*'/>
    public const string Name = "Microsoft.Data.SqlClient.WriteConnectionOpenAfter";
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
    public System.Guid OperationId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
    public string Operation => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
    public long Timestamp => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/ConnectionId/*'/>
    public System.Guid ConnectionId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/Connection/*'/>
    public SqlConnection Connection => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/ClientVersion/*'/>
    public string ClientVersion => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/Statistics/*'/>
    public System.Collections.IDictionary Statistics => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
    public int Count => throw null;
    /// <inheritdoc/>>/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
    public System.Collections.Generic.KeyValuePair<string, object> this[int index] => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    public System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<string, object>> GetEnumerator() => throw null;
}

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenBefore"]/SqlClientConnectionOpenBefore/*'/>
public sealed class SqlClientConnectionOpenBefore : System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenBefore"]/Name/*'/>
    public const string Name = "Microsoft.Data.SqlClient.WriteConnectionOpenBefore";
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
    public System.Guid OperationId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
    public string Operation => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
    public long Timestamp => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenBefore"]/Connection/*'/>
    public SqlConnection Connection => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenBefore"]/ClientVersion/*'/>
    public string ClientVersion => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
    public int Count => throw null;
    /// <inheritdoc/>>/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
    public System.Collections.Generic.KeyValuePair<string, object> this[int index] => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    public System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<string, object>> GetEnumerator() => throw null;
}

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/SqlClientConnectionOpenError/*'/>
public sealed class SqlClientConnectionOpenError : System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/Name/*'/>
    public const string Name = "Microsoft.Data.SqlClient.WriteConnectionOpenError";
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
    public System.Guid OperationId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
    public string Operation => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
    public long Timestamp => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/ConnectionId/*'/>
    public System.Guid ConnectionId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/Connection/*'/>
    public SqlConnection Connection => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/ClientVersion/*'/>
    public string ClientVersion => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/Exception/*'/>
    public System.Exception Exception => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
    public int Count => throw null;
    /// <inheritdoc/>>/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
    public System.Collections.Generic.KeyValuePair<string, object> this[int index] => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    public System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<string, object>> GetEnumerator() => throw null;
}

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitAfter"]/SqlClientTransactionCommitAfter/*'/>
public sealed class SqlClientTransactionCommitAfter : System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitAfter"]/Name/*'/>
    public const string Name = "Microsoft.Data.SqlClient.WriteTransactionCommitAfter";
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
    public System.Guid OperationId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
    public string Operation => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
    public long Timestamp => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitAfter"]/IsolationLevel/*'/>
    public System.Data.IsolationLevel IsolationLevel => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitAfter"]/Connection/*'/>
    public SqlConnection Connection => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitAfter"]/TransactionId/*'/>
    public long? TransactionId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
    public int Count => throw null;
    /// <inheritdoc/>>/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
    public System.Collections.Generic.KeyValuePair<string, object> this[int index] => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    public System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<string, object>> GetEnumerator() => throw null;
}

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitBefore"]/SqlClientTransactionCommitBefore/*'/>
public sealed class SqlClientTransactionCommitBefore : System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitBefore"]/Name/*'/>
    public const string Name = "Microsoft.Data.SqlClient.WriteTransactionCommitBefore";
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
    public System.Guid OperationId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
    public string Operation => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
    public long Timestamp => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitBefore"]/IsolationLevel/*'/>
    public System.Data.IsolationLevel IsolationLevel => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitBefore"]/Connection/*'/>
    public SqlConnection Connection => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitBefore"]/TransactionId/*'/>
    public long? TransactionId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
    public int Count => throw null;
    /// <inheritdoc/>>/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
    public System.Collections.Generic.KeyValuePair<string, object> this[int index] => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    public System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<string, object>> GetEnumerator() => throw null;
}

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/SqlClientTransactionCommitError/*'/>
public sealed class SqlClientTransactionCommitError : System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/Name/*'/>
    public const string Name = "Microsoft.Data.SqlClient.WriteTransactionCommitError";
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
    public System.Guid OperationId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
    public string Operation => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
    public long Timestamp => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/IsolationLevel/*'/>
    public System.Data.IsolationLevel IsolationLevel => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/Connection/*'/>
    public SqlConnection Connection => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/TransactionId/*'/>
    public long? TransactionId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/Exception/*'/>
    public System.Exception Exception => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
    public int Count => throw null;
    /// <inheritdoc/>>/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
    public System.Collections.Generic.KeyValuePair<string, object> this[int index] => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    public System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<string, object>> GetEnumerator() => throw null;
}

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/SqlClientTransactionRollbackAfter/*'/>
public sealed class SqlClientTransactionRollbackAfter : System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/Name/*'/>
    public const string Name = "Microsoft.Data.SqlClient.WriteTransactionRollbackAfter";
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
    public System.Guid OperationId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
    public string Operation => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
    public long Timestamp => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/IsolationLevel/*'/>
    public System.Data.IsolationLevel IsolationLevel => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/Connection/*'/>
    public SqlConnection Connection => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/TransactionId/*'/>
    public long? TransactionId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/TransactionName/*'/>
    public string TransactionName => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
    public int Count => throw null;
    /// <inheritdoc/>>/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
    public System.Collections.Generic.KeyValuePair<string, object> this[int index] => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    public System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<string, object>> GetEnumerator() => throw null;
}

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/SqlClientTransactionRollbackBefore/*'/>
public sealed class SqlClientTransactionRollbackBefore : System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/SqlClientTransactionRollbackBefore/*'/>
    public const string Name = "Microsoft.Data.SqlClient.WriteTransactionRollbackBefore";
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
    public System.Guid OperationId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
    public string Operation => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
    public long Timestamp => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/IsolationLevel/*'/>
    public System.Data.IsolationLevel IsolationLevel => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/Connection/*'/>
    public SqlConnection Connection => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/TransactionId/*'/>
    public long? TransactionId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/TransactionName/*'/>
    public string TransactionName => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
    public int Count => throw null;
    /// <inheritdoc/>>/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
    public System.Collections.Generic.KeyValuePair<string, object> this[int index] => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    public System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<string, object>> GetEnumerator() => throw null;
}

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackError"]/SqlClientTransactionRollbackError/*'/>
public sealed class SqlClientTransactionRollbackError : System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object>>
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackError"]/Name/*'/>
    public const string Name = "Microsoft.Data.SqlClient.WriteTransactionRollbackError";
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
    public System.Guid OperationId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
    public string Operation => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
    public long Timestamp => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackError"]/IsolationLevel/*'/>
    public System.Data.IsolationLevel IsolationLevel => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackError"]/Connection/*'/>
    public SqlConnection Connection => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackError"]/TransactionId/*'/>
    public long? TransactionId => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackError"]/TransactionName/*'/>
    public string TransactionName => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackError"]/Exception/*'/>
    public System.Exception Exception => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
    public int Count => throw null;
    /// <inheritdoc/>>/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
    public System.Collections.Generic.KeyValuePair<string, object> this[int index] => throw null;
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
    public System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<string, object>> GetEnumerator() => throw null;
}
