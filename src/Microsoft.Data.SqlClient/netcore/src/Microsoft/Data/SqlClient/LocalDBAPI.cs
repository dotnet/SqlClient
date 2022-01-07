// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Data
{
    internal static partial class LocalDBAPI
    {
        private const string LocalDbPrefix = @"(localdb)\";
        private const string LocalDbPrefix_NP = @"np:\\.\pipe\LOCALDB#";


        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private delegate int LocalDBFormatMessageDelegate(int hrLocalDB, uint dwFlags, uint dwLanguageId, StringBuilder buffer, ref uint buflen);

        // check if name is in format (localdb)\<InstanceName - not empty> and return instance name if it is
        // localDB can also have a format of np:\\.\pipe\LOCALDB#<some number>\tsql\query
        internal static string GetLocalDbInstanceNameFromServerName(string serverName)
        {
            string instanceName = null;
            serverName = serverName.TrimStart(); // it can start with spaces if specified in quotes
            bool isLocalDb = serverName.StartsWith(LocalDbPrefix, StringComparison.OrdinalIgnoreCase) || serverName.StartsWith(LocalDbPrefix_NP, StringComparison.OrdinalIgnoreCase);
            if (isLocalDb)
            {
                if (serverName.StartsWith(LocalDbPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    instanceName = serverName.Substring(LocalDbPrefix.Length).Trim();
                }
                else
                {
                    instanceName = serverName.TrimEnd();
                }
            }
            return instanceName;
        }
    }
}
