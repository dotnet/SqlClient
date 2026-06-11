// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Data.SqlClient.Diagnostics;

internal static class TelemetryAttributes
{
    /// <summary>
    /// Attributes prefixed with "db." and "sqlclient.db.".
    /// </summary>
    /// <seealso href="https://opentelemetry.io/docs/specs/semconv/registry/attributes/db/"/>
    /// <seealso href="https://opentelemetry.io/docs/specs/semconv/db/sql-server/"/>
    public static class Database
    {
        private const string StandardsPrefix = "db.";

        private const string LibrarySpecificPrefix = "sqlclient.db.";

        /// <summary>
        /// This must always be <see cref="TelemetryAttributeValues.Database.SystemName" />.
        /// </summary>
        public const string SystemName = $"{StandardsPrefix}system.name";

        /// <summary>
        /// This is always in the format <c>{instance name}|{database name}</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The instance name is not included if the default instance is used.
        /// </para>
        /// <para>
        /// This namespace is a <i>logical</i> construct, and does not necessarily reflect
        /// the instance which the connection is currently connected to, or the value specified
        /// in the connection string:
        /// <list type="number">
        /// <item>
        /// An AlwaysOn Availability Group between two named SQL Server instances which are accessed
        /// through a listener. The namespace will not contain an instance name; it will only be aware
        /// of connecting to the default instance on the listener.
        /// </item>
        /// <item>
        /// Database mirroring between two SQL Server instances. The failover partner details supplied
        /// by the server will contain a port number, not an instance name. The namespace will contain
        /// an instance name prior to failover, and will not contain an instance name after failover.
        /// </item>
        /// <item>
        /// On Windows, configuring a client-side alias for a non-default SQL Server instance. The connection
        /// string will not contain an instance name, but alias resolution will discover it. The namespace
        /// will contain an instance name.
        /// </item>
        /// <item>
        /// An AlwaysOn Availability Group between two named SQL Server instances, accessed directly and
        /// using read-only routing. The namespace will contain an instance name when connected using a
        /// connection intent of <c>ReadWrite</c>, and will not contain an instance name when connected
        /// using a connection intent of <c>ReadOnly</c> (since read-only routing will re-route the connection
        /// to a server based on port number, and will not provide an instance name.)
        /// </item>
        /// </list>
        /// </para>
        /// <para>
        /// In these more complex scenarios, clients are recommended to use the <see cref="Server.Address"/>
        /// and <see cref="Server.Port"/> attributes to uniquely identify the server being connected to, and
        /// the <see cref="DatabaseName"/> attribute to identify the database being used.
        /// </para>
        /// </remarks>
        public const string Namespace = $"{StandardsPrefix}namespace";

        public const string DatabaseName = $"{LibrarySpecificPrefix}database.name";

        public const string OperationName = $"{StandardsPrefix}operation.name";

        public const string StoredProcedureName = $"{StandardsPrefix}stored_procedure.name";

        public const string ResponseStatusCode = $"{StandardsPrefix}response.status_code";

        public const string QueryText = $"{StandardsPrefix}query.text";

        public const string OperationBatchSize = $"{StandardsPrefix}operation.batch.size";
    }

    /// <summary>
    /// Attributes prefixed with "error.".
    /// </summary>
    /// <seealso href="https://opentelemetry.io/docs/specs/semconv/registry/attributes/error/"/>
    /// <seealso href="https://opentelemetry.io/docs/specs/semconv/db/sql-server/"/>
    public static class Error
    {
        private const string StandardsPrefix = "error.";

        public const string Type = $"{StandardsPrefix}type";
    }

    /// <summary>
    /// Attributes prefixed with "exception.".
    /// </summary>
    /// <seealso href="https://opentelemetry.io/docs/specs/semconv/registry/attributes/exception/"/>
    /// <seealso href="https://opentelemetry.io/docs/specs/semconv/db/sql-server/"/>
    /// <seealso href="https://opentelemetry.io/docs/specs/semconv/general/recording-errors/"/>
    /// <seealso href="https://opentelemetry.io/docs/specs/semconv/exceptions/exceptions-spans/"/>
    public static class Exception
    {
        private const string StandardsPrefix = "exception.";

        public const string Type = $"{StandardsPrefix}type";

        public const string Message = $"{StandardsPrefix}message";

        public const string StackTrace = $"{StandardsPrefix}stacktrace";
    }

    /// <summary>
    /// Attributes prefixed with "server.".
    /// </summary>
    /// <seealso href="https://opentelemetry.io/docs/specs/semconv/registry/attributes/server/"/>
    /// <seealso href="https://opentelemetry.io/docs/specs/semconv/db/sql-server/"/>
    public static class Server
    {
        private const string StandardsPrefix = "server.";

        public const string Address = $"{StandardsPrefix}address";

        public const string Port = $"{StandardsPrefix}port";
    }
}

internal static class TelemetryAttributeValues
{
    /// <summary>
    /// Values for attributes described in <see cref="TelemetryAttributes.Database" />.
    /// </summary>
    /// <seealso href="https://opentelemetry.io/docs/specs/semconv/database/sql-server/"/>
    public static class Database
    {
        public const string SystemName = "microsoft.sql_server";

        public const string ExecuteOperationName = "EXECUTE";
    }
}

internal static class TelemetryEventNames
{
    /// <summary>
    /// The name of an exception event.
    /// </summary>
    /// <seealso href="https://opentelemetry.io/docs/specs/semconv/exceptions/exceptions-spans/"/>
    public const string Exception = "exception";
}
