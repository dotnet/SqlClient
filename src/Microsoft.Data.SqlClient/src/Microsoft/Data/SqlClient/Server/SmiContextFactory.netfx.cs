// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

namespace Microsoft.Data.SqlClient.Server
{
    internal sealed class SmiContextFactory
    {
        public static readonly SmiContextFactory Instance = new SmiContextFactory();

        internal const ulong Sql2005Version = 100;
        internal const ulong Sql2008Version = 210;
        internal const ulong LatestVersion = Sql2008Version;

        private SmiContextFactory()
        {
        }

        internal ulong NegotiatedSmiVersion
        {
            get => throw SQL.ContextUnavailableOutOfProc();    // Must not be a valid version of Sql Server, or not be SqlCLR
        }

        internal string ServerVersion
        {
            get => throw SQL.ContextUnavailableOutOfProc();    // Must not be a valid version of Sql Server, or not be SqlCLR
        }

        internal SmiContext GetCurrentContext() =>
            throw SQL.ContextUnavailableOutOfProc();    // Must not be a valid version of Sql Server, or not be SqlCLR
    }
}

#endif
