// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class SqlBulkCopyTest
    {
        public static string ConnectionString => DataTestUtility.TCPConnectionString;
        public static bool AreConnectionStringsSetup() => DataTestUtility.AreConnStringsSetup();
        public static bool IsAzureServer() => !DataTestUtility.IsNotAzureServer();
        public static bool IsNotAzureServer() => DataTestUtility.IsNotAzureServer();
        public static bool IsNotAzureSynapse() => DataTestUtility.IsNotAzureSynapse();

        public static string AddGuid(string stringin)
        {
            stringin += "_" + Guid.NewGuid().ToString().Replace('-', '_');
            return stringin;
        }
    }
}
