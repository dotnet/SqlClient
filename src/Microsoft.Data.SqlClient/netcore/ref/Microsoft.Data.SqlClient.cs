// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// NOTE: The current Microsoft.VSDesigner editor attributes are implemented for System.Data.SqlClient, and are not publicly available.
// New attributes that are designed to work with Microsoft.Data.SqlClient and are publicly documented should be included in future.

namespace Microsoft.Data.SqlClient.Diagnostics
{
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
}
namespace Microsoft.Data.SqlClient.Server
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SqlDataRecord/*'/>
    public partial class SqlDataRecord : System.Data.IDataRecord
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/ctor/*'/>
        public SqlDataRecord(params Microsoft.Data.SqlClient.Server.SqlMetaData[] metaData) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/FieldCount/*'/>
        public virtual int FieldCount { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/ItemOrdinal/*'/>
        public virtual object this[int ordinal] { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/ItemName/*'/>
        public virtual object this[string name] { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetBoolean/*'/>
        public virtual bool GetBoolean(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetByte/*'/>
        public virtual byte GetByte(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetBytes/*'/>
        public virtual long GetBytes(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetChar/*'/>
        public virtual char GetChar(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetChars/*'/>
        public virtual long GetChars(int ordinal, long fieldOffset, char[] buffer, int bufferOffset, int length) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetData/*'/>
        System.Data.IDataReader System.Data.IDataRecord.GetData(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDataTypeName/*'/>
        public virtual string GetDataTypeName(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDateTime/*'/>
        public virtual System.DateTime GetDateTime(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDateTimeOffset/*'/>
        public virtual System.DateTimeOffset GetDateTimeOffset(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDecimal/*'/>
        public virtual decimal GetDecimal(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDouble/*'/>
        public virtual double GetDouble(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetFieldType/*'/>
#if NET
        [return: System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
        public virtual System.Type GetFieldType(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetFloat/*'/>
        public virtual float GetFloat(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetGuid/*'/>
        public virtual System.Guid GetGuid(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetInt16/*'/>
        public virtual short GetInt16(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetInt32/*'/>
        public virtual int GetInt32(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetInt64/*'/>
        public virtual long GetInt64(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetName/*'/>
        public virtual string GetName(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetOrdinal/*'/>
        public virtual int GetOrdinal(string name) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlBinary/*'/>
        public virtual System.Data.SqlTypes.SqlBinary GetSqlBinary(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlBoolean/*'/>
        public virtual System.Data.SqlTypes.SqlBoolean GetSqlBoolean(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlByte/*'/>
        public virtual System.Data.SqlTypes.SqlByte GetSqlByte(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlBytes/*'/>
        public virtual System.Data.SqlTypes.SqlBytes GetSqlBytes(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlChars/*'/>
        public virtual System.Data.SqlTypes.SqlChars GetSqlChars(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlDateTime/*'/>
        public virtual System.Data.SqlTypes.SqlDateTime GetSqlDateTime(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlDecimal/*'/>
        public virtual System.Data.SqlTypes.SqlDecimal GetSqlDecimal(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlDouble/*'/>
        public virtual System.Data.SqlTypes.SqlDouble GetSqlDouble(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlFieldType/*'/>
        public virtual System.Type GetSqlFieldType(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlGuid/*'/>
        public virtual System.Data.SqlTypes.SqlGuid GetSqlGuid(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlInt16/*'/>
        public virtual System.Data.SqlTypes.SqlInt16 GetSqlInt16(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlInt32/*'/>
        public virtual System.Data.SqlTypes.SqlInt32 GetSqlInt32(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlInt64/*'/>
        public virtual System.Data.SqlTypes.SqlInt64 GetSqlInt64(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlMetaData/*'/>
        public virtual Microsoft.Data.SqlClient.Server.SqlMetaData GetSqlMetaData(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlMoney/*'/>
        public virtual System.Data.SqlTypes.SqlMoney GetSqlMoney(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlSingle/*'/>
        public virtual System.Data.SqlTypes.SqlSingle GetSqlSingle(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlString/*'/>
        public virtual System.Data.SqlTypes.SqlString GetSqlString(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlValue/*'/>
        public virtual object GetSqlValue(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlValues/*'/>
        public virtual int GetSqlValues(object[] values) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlXml/*'/>
        public virtual System.Data.SqlTypes.SqlXml GetSqlXml(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetString/*'/>
        public virtual string GetString(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetTimeSpan/*'/>
        public virtual System.TimeSpan GetTimeSpan(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetValue/*'/>
        public virtual object GetValue(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetValues/*'/>
        public virtual int GetValues(object[] values) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/IsDBNull/*'/>
        public virtual bool IsDBNull(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetBoolean/*'/>
        public virtual void SetBoolean(int ordinal, bool value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetByte/*'/>
        public virtual void SetByte(int ordinal, byte value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetBytes/*'/>
        public virtual void SetBytes(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetChar/*'/>
        public virtual void SetChar(int ordinal, char value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetChars/*'/>
        public virtual void SetChars(int ordinal, long fieldOffset, char[] buffer, int bufferOffset, int length) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDateTime/*'/>
        public virtual void SetDateTime(int ordinal, System.DateTime value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDateTimeOffset/*'/>
        public virtual void SetDateTimeOffset(int ordinal, System.DateTimeOffset value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDBNull/*'/>
        public virtual void SetDBNull(int ordinal) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDecimal/*'/>
        public virtual void SetDecimal(int ordinal, decimal value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDouble/*'/>
        public virtual void SetDouble(int ordinal, double value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetFloat/*'/>
        public virtual void SetFloat(int ordinal, float value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetGuid/*'/>
        public virtual void SetGuid(int ordinal, System.Guid value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetInt16/*'/>
        public virtual void SetInt16(int ordinal, short value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetInt32/*'/>
        public virtual void SetInt32(int ordinal, int value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetInt64/*'/>
        public virtual void SetInt64(int ordinal, long value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlBinary/*'/>
        public virtual void SetSqlBinary(int ordinal, System.Data.SqlTypes.SqlBinary value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlBoolean/*'/>
        public virtual void SetSqlBoolean(int ordinal, System.Data.SqlTypes.SqlBoolean value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlByte/*'/>
        public virtual void SetSqlByte(int ordinal, System.Data.SqlTypes.SqlByte value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlBytes/*'/>
        public virtual void SetSqlBytes(int ordinal, System.Data.SqlTypes.SqlBytes value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlChars/*'/>
        public virtual void SetSqlChars(int ordinal, System.Data.SqlTypes.SqlChars value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlDateTime/*'/>
        public virtual void SetSqlDateTime(int ordinal, System.Data.SqlTypes.SqlDateTime value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlDecimal/*'/>
        public virtual void SetSqlDecimal(int ordinal, System.Data.SqlTypes.SqlDecimal value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlDouble/*'/>
        public virtual void SetSqlDouble(int ordinal, System.Data.SqlTypes.SqlDouble value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlGuid/*'/>
        public virtual void SetSqlGuid(int ordinal, System.Data.SqlTypes.SqlGuid value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlInt16/*'/>
        public virtual void SetSqlInt16(int ordinal, System.Data.SqlTypes.SqlInt16 value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlInt32/*'/>
        public virtual void SetSqlInt32(int ordinal, System.Data.SqlTypes.SqlInt32 value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlInt64/*'/>
        public virtual void SetSqlInt64(int ordinal, System.Data.SqlTypes.SqlInt64 value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlMoney/*'/>
        public virtual void SetSqlMoney(int ordinal, System.Data.SqlTypes.SqlMoney value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlSingle/*'/>
        public virtual void SetSqlSingle(int ordinal, System.Data.SqlTypes.SqlSingle value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlString/*'/>
        public virtual void SetSqlString(int ordinal, System.Data.SqlTypes.SqlString value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlXml/*'/>
        public virtual void SetSqlXml(int ordinal, System.Data.SqlTypes.SqlXml value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetString/*'/>
        public virtual void SetString(int ordinal, string value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetTimeSpan/*'/>
        public virtual void SetTimeSpan(int ordinal, System.TimeSpan value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetValue/*'/>
        public virtual void SetValue(int ordinal, object value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetValues/*'/>
        public virtual int SetValues(params object[] values) { throw null; }
    }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/SqlMetaData/*' />
    public sealed partial class SqlMetaData
    {
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbType/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypePrecisionScale/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, byte precision, byte scale) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypePrecisionScaleUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, byte precision, byte scale, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLength/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLengthUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLengthPrecisionScaleLocaleCompareOptionsUserDefinedType/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, byte precision, byte scale, long locale, System.Data.SqlTypes.SqlCompareOptions compareOptions, System.Type userDefinedType) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLengthPrecisionScaleLocaleCompareOptionsUserDefinedTypeUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, byte precision, byte scale, long localeId, System.Data.SqlTypes.SqlCompareOptions compareOptions, System.Type userDefinedType, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLengthLocaleCompareOptions/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, long locale, System.Data.SqlTypes.SqlCompareOptions compareOptions) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLengthLocaleCompareOptionsUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, long locale, System.Data.SqlTypes.SqlCompareOptions compareOptions, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeDatabaseOwningSchemaObjectName/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, string database, string owningSchema, string objectName) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeDatabaseOwningSchemaObjectNameUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, string database, string owningSchema, string objectName, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeUserDefinedType/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, System.Type userDefinedType) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeUserDefinedTypeServerTypeName/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, System.Type userDefinedType, string serverTypeName) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeUserDefinedTypeServerTypeNameUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, System.Type userDefinedType, string serverTypeName, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/CompareOptions/*' />
        public System.Data.SqlTypes.SqlCompareOptions CompareOptions { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/DbType/*' />
        public System.Data.DbType DbType { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/IsUniqueKey/*' />
        public bool IsUniqueKey { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/LocaleId/*' />
        public long LocaleId { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/Max/*' />
        public static long Max { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/MaxLength/*' />
        public long MaxLength { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/Name/*' />
        public string Name { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/Precision/*' />
        public byte Precision { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/Scale/*' />
        public byte Scale { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/SortOrder/*' />
        public Microsoft.Data.SqlClient.SortOrder SortOrder { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/SortOrdinal/*' />
        public int SortOrdinal { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/SqlDbType/*' />
        public System.Data.SqlDbType SqlDbType { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/Type/*' />
        public System.Type Type { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/TypeName/*' />
        public string TypeName { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/UseServerDefault/*' />
        public bool UseServerDefault { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/XmlSchemaCollectionDatabase/*' />
        public string XmlSchemaCollectionDatabase { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/XmlSchemaCollectionName/*' />
        public string XmlSchemaCollectionName { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/XmlSchemaCollectionOwningSchema/*' />
        public string XmlSchemaCollectionOwningSchema { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue1/*' />
        public bool Adjust(bool value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue2/*' />
        public byte Adjust(byte value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue3/*' />
        public byte[] Adjust(byte[] value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue4/*' />
        public char Adjust(char value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue5/*' />
        public char[] Adjust(char[] value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue6/*' />
        public System.Data.SqlTypes.SqlBinary Adjust(System.Data.SqlTypes.SqlBinary value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue7/*' />
        public System.Data.SqlTypes.SqlBoolean Adjust(System.Data.SqlTypes.SqlBoolean value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue8/*' />
        public System.Data.SqlTypes.SqlByte Adjust(System.Data.SqlTypes.SqlByte value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue9/*' />
        public System.Data.SqlTypes.SqlBytes Adjust(System.Data.SqlTypes.SqlBytes value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue10/*' />
        public System.Data.SqlTypes.SqlChars Adjust(System.Data.SqlTypes.SqlChars value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue11/*' />
        public System.Data.SqlTypes.SqlDateTime Adjust(System.Data.SqlTypes.SqlDateTime value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue12/*' />
        public System.Data.SqlTypes.SqlDecimal Adjust(System.Data.SqlTypes.SqlDecimal value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue13/*' />
        public System.Data.SqlTypes.SqlDouble Adjust(System.Data.SqlTypes.SqlDouble value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue14/*' />
        public System.Data.SqlTypes.SqlGuid Adjust(System.Data.SqlTypes.SqlGuid value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue15/*' />
        public System.Data.SqlTypes.SqlInt16 Adjust(System.Data.SqlTypes.SqlInt16 value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue16/*' />
        public System.Data.SqlTypes.SqlInt32 Adjust(System.Data.SqlTypes.SqlInt32 value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue17/*' />
        public System.Data.SqlTypes.SqlInt64 Adjust(System.Data.SqlTypes.SqlInt64 value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue18/*' />
        public System.Data.SqlTypes.SqlMoney Adjust(System.Data.SqlTypes.SqlMoney value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue19/*' />
        public System.Data.SqlTypes.SqlSingle Adjust(System.Data.SqlTypes.SqlSingle value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue20/*' />
        public System.Data.SqlTypes.SqlString Adjust(System.Data.SqlTypes.SqlString value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue21/*' />
        public System.Data.SqlTypes.SqlXml Adjust(System.Data.SqlTypes.SqlXml value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue22/*' />
        public System.DateTime Adjust(System.DateTime value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue23/*' />
        public System.DateTimeOffset Adjust(System.DateTimeOffset value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue24/*' />
        public decimal Adjust(decimal value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue25/*' />
        public double Adjust(double value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue26/*' />
        public System.Guid Adjust(System.Guid value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue27/*' />
        public short Adjust(short value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue28/*' />
        public int Adjust(int value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue29/*' />
        public long Adjust(long value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue30/*' />
        public object Adjust(object value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue31/*' />
        public float Adjust(float value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue32/*' />
        public string Adjust(string value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue33/*' />
        public System.TimeSpan Adjust(System.TimeSpan value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/InferFromValue/*' />
        public static Microsoft.Data.SqlClient.Server.SqlMetaData InferFromValue(object value, string name) { throw null; }
    }
}
namespace Microsoft.Data.SqlClient.DataClassification
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/ColumnSensitivity.xml' path='docs/members[@name="ColumnSensitivity"]/ColumnSensitivity/*' />
    public partial class ColumnSensitivity
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/ColumnSensitivity.xml' path='docs/members[@name="ColumnSensitivity"]/ctor/*' />
        public ColumnSensitivity(System.Collections.Generic.IList<Microsoft.Data.SqlClient.DataClassification.SensitivityProperty> sensitivityProperties) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/ColumnSensitivity.xml' path='docs/members[@name="ColumnSensitivity"]/GetSensitivityProperties/*' />
        public System.Collections.ObjectModel.ReadOnlyCollection<Microsoft.Data.SqlClient.DataClassification.SensitivityProperty> SensitivityProperties { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/InformationType.xml' path='docs/members[@name="InformationType"]/InformationType/*' />
    public partial class InformationType
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/InformationType.xml' path='docs/members[@name="InformationType"]/ctor/*' />
        public InformationType(string name, string id) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/InformationType.xml' path='docs/members[@name="InformationType"]/Id/*' />
        public string Id { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/InformationType.xml' path='docs/members[@name="InformationType"]/Name/*' />
        public string Name { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/Label.xml' path='docs/members[@name="Label"]/Label/*' />
    public partial class Label
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/Label.xml' path='docs/members[@name="Label"]/ctor/*' />
        public Label(string name, string id) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/Label.xml' path='docs/members[@name="Label"]/Id/*' />
        public string Id { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/Label.xml' path='docs/members[@name="Label"]/Name/*' />
        public string Name { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/SensitivityRank/*' />
    public enum SensitivityRank
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/NotDefined/*' />
        NOT_DEFINED = -1,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/None/*' />
        NONE = 0,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/Low/*' />
        LOW = 10,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/Medium/*' />
        MEDIUM = 20,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/High/*' />
        HIGH = 30,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/Critical/*' />
        CRITICAL = 40
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/SensitivityClassification/*' />
    public partial class SensitivityClassification
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/ctor/*' />
        public SensitivityClassification(System.Collections.Generic.IList<Microsoft.Data.SqlClient.DataClassification.Label> labels, System.Collections.Generic.IList<Microsoft.Data.SqlClient.DataClassification.InformationType> informationTypes, System.Collections.Generic.IList<Microsoft.Data.SqlClient.DataClassification.ColumnSensitivity> columnSensitivity, SensitivityRank sensitivityRank) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/ColumnSensitivities/*' />
        public System.Collections.ObjectModel.ReadOnlyCollection<Microsoft.Data.SqlClient.DataClassification.ColumnSensitivity> ColumnSensitivities { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/InformationTypes/*' />
        public System.Collections.ObjectModel.ReadOnlyCollection<Microsoft.Data.SqlClient.DataClassification.InformationType> InformationTypes { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/Labels/*' />
        public System.Collections.ObjectModel.ReadOnlyCollection<Microsoft.Data.SqlClient.DataClassification.Label> Labels { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/SensitivityRank/*' />
        public SensitivityRank SensitivityRank { get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/SensitivityProperty/*' />
    public partial class SensitivityProperty
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/ctor/*' />
        public SensitivityProperty(Microsoft.Data.SqlClient.DataClassification.Label label, Microsoft.Data.SqlClient.DataClassification.InformationType informationType, SensitivityRank sensitivityRank) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/InformationType/*' />
        public Microsoft.Data.SqlClient.DataClassification.InformationType InformationType { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/Label/*' />
        public Microsoft.Data.SqlClient.DataClassification.Label Label { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/SensitivityRank/*' />
        public SensitivityRank SensitivityRank { get { throw null; } }
    }
}
