// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

using System;
using System.Diagnostics;
using Microsoft.Data.SqlClient.Server;

namespace Microsoft.Data.SqlClient
{
    internal static class AssemblyCache
    {
        internal static int GetLength(object inst)
        {
            // Caller should have allocated enough, based on MaxByteSize
            return SerializationHelperSql9.SizeInBytes(inst);
        }

        internal static SqlUdtInfo GetInfoFromType(Type t)
        {
            Debug.Assert(t != null, "Type object cant be NULL");

            Type orig = t;
            do
            {
                SqlUdtInfo attr = SqlUdtInfo.TryGetFromType(t);

                if (attr != null)
                {
                    return attr;
                }

                t = t.BaseType;
            }
            while (t != null);

            throw SQL.UDTInvalidSqlType(orig.AssemblyQualifiedName);
        }
    }
}

#endif
