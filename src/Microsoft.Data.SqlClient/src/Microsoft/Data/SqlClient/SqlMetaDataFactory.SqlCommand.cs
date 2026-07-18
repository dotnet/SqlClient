// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.Common;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class SqlMetaDataFactory
    {
        private sealed class SupportedQuery
        {
            public string? MinimumVersion { get; init; }
            public string? MaximumVersion { get; init; }
            public string Query { get; init; }

            public SupportedQuery(string? minimumVersion, string? maximumVersion, string query)
            {
                MinimumVersion = minimumVersion;
                MaximumVersion = maximumVersion;
                Query = query;
            }
        }


        /// <summary>
        /// Returns one of the <see href="https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql-server-schema-collections">SQL Server Schema collections</see>
        /// </summary>
        private sealed class SqlCommandCollection : MetaDataCollectionBase
        {
            private readonly SupportedQuery[] Queries;
            public Restriction[] RestrictionParams { get; init; }

            public SqlCommandCollection(string collectionName, int numberOfRestrictions, int numberOfIdentifierParts, SupportedQuery[] queries, Restriction[] restrictions)
                : base(collectionName, numberOfRestrictions, numberOfIdentifierParts)
            {
                RestrictionParams = restrictions;
                Queries = queries;
            }

            public override bool SupportedByCurrentVersion(MetaDataContext context) => GetSupportedQuery(context, false) != null;

            private SupportedQuery? GetSupportedQuery(MetaDataContext context, bool throwIfNotFound)
            {
                SupportedQuery? validQuery = null;
                foreach (SupportedQuery query in Queries)
                {
                    // Since Azure DB always has latest stable version of SQL Server engine, take latest query 
                    if ((context.Caps.IsAzureSql && query.MaximumVersion == null) ||
                        // ..or if it's not Azure, check if the current version is within the supported range
                        (query.MinimumVersion == null || string.Compare(context.Caps.ServerVersion, query.MinimumVersion, StringComparison.OrdinalIgnoreCase) >= 0) &&
                        (query.MaximumVersion == null || string.Compare(context.Caps.ServerVersion, query.MaximumVersion, StringComparison.OrdinalIgnoreCase) <= 0))
                    {
                        Debug.Assert(validQuery == null, $"Two queries matches current version {context.Caps.ServerVersion} in collection {CollectionName}");
                        validQuery = query;
                    }
                }

                // If there are no queries matching current server version, then whole collection is undefined for this version
                if (validQuery == null && throwIfNotFound)
                {
                    throw ADP.UndefinedCollection(CollectionName);
                }

                return validQuery;
            }

            public async override ValueTask<DataTable> GetMetadata(MetaDataContext context, DataTable? accumulator = null)
            {
                Debug.Assert(NumberOfRestrictions >= (context.RestrictionValues?.Length ?? 0));

                context.CancellationToken.ThrowIfCancellationRequested();

                DataTable resultTable;

                if ((context.RestrictionValues is not null) && (context.RestrictionValues.Length > NumberOfRestrictions))
                {
                    throw ADP.TooManyRestrictions(CollectionName);
                }

                if (!ADP.IsEmptyArray(context.RestrictionValues))
                {
                    for (int i = 0; i < context.RestrictionValues!.Length; i++)
                    {
                        if ((context.RestrictionValues[i] is not null) && (context.RestrictionValues[i].Length > 4096))
                        {
                            // use a non-specific error because no new beta 2 error messages are allowed
                            // TODO: will add a more descriptive error in RTM
                            throw ADP.NotSupported();
                        }
                    }
                }

                SqlConnection castConnection = (SqlConnection)context.Connection;
                using SqlCommand command = castConnection.CreateCommand();

                command.CommandText = GetSupportedQuery(context, true)!.Query;
                command.CommandTimeout = Math.Max(command.CommandTimeout, 180);
                command.Transaction = castConnection?.GetOpenTdsConnection()?.CurrentTransaction?.Parent;

                for (int i = 0; i < NumberOfRestrictions; i++)
                {
                    SqlParameter restrictionParameter = command.CreateParameter();

                    if ((context.RestrictionValues is not null) && (i < context.RestrictionValues.Length) && (context.RestrictionValues[i] is not null))
                    {
                        restrictionParameter.Value = context.RestrictionValues[i];
                    }
                    else
                    {
                        // This is where we have to assign null to the value of the parameter.
                        restrictionParameter.Value = DBNull.Value;
                    }

                    restrictionParameter.ParameterName = RestrictionParams[i].ParameterName;
                    restrictionParameter.Direction = ParameterDirection.Input;
                    command.Parameters.Add(restrictionParameter);
                }

                SqlDataReader? reader = null;
                try
                {
                    try
                    {
                        reader = context.IsAsync ? await command.ExecuteReaderAsync(context.CancellationToken) : command.ExecuteReader();
                    }
                    catch (Exception e) when (ADP.IsCatchableExceptionType(e))
                    {
                        throw ADP.QueryFailed(CollectionName, e);
                    }

                    // reader.GetColumnSchema waits synchronously for the reader to receive its type metadata.
                    // Instead, we invoke reader.ReadAsync outside of the while loop, which will implicitly ensure that
                    // the metadata is available. ReadAsync/Read will throw an exception if necessary, so we can trust
                    // that the list of fields is available if the call returns.
                    bool firstResultAvailable = context.IsAsync ? await reader.ReadAsync(context.CancellationToken) : reader.Read();

                    if (accumulator != null)
                    {
                        resultTable = accumulator;
                    }
                    else
                    {
                        // Build a DataTable from the reader
                        resultTable = new DataTable(CollectionName)
                        {
                            Locale = CultureInfo.InvariantCulture
                        };

                        System.Collections.ObjectModel.ReadOnlyCollection<DbColumn> colSchema = reader.GetColumnSchema();
                        foreach (DbColumn col in colSchema)
                        {
                            resultTable.Columns.Add(col.ColumnName, col.DataType!);
                        }
                    }

                    if (firstResultAvailable)
                    {
                        object[] values = new object[resultTable.Columns.Count];
                        do
                        {
                            reader.GetValues(values);
                            resultTable.Rows.Add(values);
                        } while (context.IsAsync ? await reader.ReadAsync(context.CancellationToken) : reader.Read());
                    }
                }
                finally
                {
                    reader?.Dispose();
                }

                return resultTable;
            }
        }
    }
}
