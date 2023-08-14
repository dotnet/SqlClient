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
            if (serverName is not null)
            {
                // it can start with spaces if specified in quotes
                // Memory allocation is reduced by using ReadOnlySpan
                ReadOnlySpan<char> input = serverName.AsSpan().Trim();
                if (input.StartsWith(LocalDbPrefix.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    input = input.Slice(LocalDbPrefix.Length);
                    if (!input.IsEmpty)
                    {
                        return input.ToString();
                    }
                }
                else if (input.StartsWith(LocalDbPrefix_NP.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return input.ToString();
                }
            }
            return null;
        }
    }
}
