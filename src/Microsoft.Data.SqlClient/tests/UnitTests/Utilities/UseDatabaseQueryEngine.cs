// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.EnvChange;
using Microsoft.SqlServer.TDS.Servers;
using Microsoft.SqlServer.TDS.SQLBatch;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities
{
    /// <summary>
    /// Answers USE batches with the database change token, so
    /// <see cref="SqlConnection.ChangeDatabase(string)"/> moves the client to the new database.
    /// </summary>
    internal sealed class UseDatabaseQueryEngine : QueryEngine
    {
        private static readonly Regex s_use = new(
            @"^\s*use\s+\[?(?<db>[^\]\s;]+)\]?\s*;?\s*$",
            RegexOptions.IgnoreCase);

        internal UseDatabaseQueryEngine(TdsServerArguments arguments)
            : base(arguments)
        {
        }

        protected override TDSMessageCollection CreateQueryResponse(
            ITDSServerSession session, TDSSQLBatchToken batchRequest)
        {
            // The simulated batch parser can leave NUL padding around the text.
            Match match = s_use.Match(batchRequest.Text.Trim('\0'));
            if (!match.Success)
            {
                return base.CreateQueryResponse(session, batchRequest);
            }

            // Move the session and report the change the way a server does.
            string oldDatabase = session.Database;
            session.Database = match.Groups["db"].Value;
            return new TDSMessageCollection(new TDSMessage(
                TDSMessageType.Response,
                new TDSEnvChangeToken(
                    TDSEnvChangeTokenType.Database, session.Database, oldDatabase),
                new TDSDoneToken(TDSDoneTokenStatusType.Final)));
        }
    }
}
