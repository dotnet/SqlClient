// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Data.SqlClient.Server;

namespace Microsoft.Data.SqlClient
{
    internal class SqlUdtInfo
    {
        internal readonly Format SerializationFormat;
        internal readonly bool IsByteOrdered;
        internal readonly bool IsFixedLength;
        internal readonly int MaxByteSize;
        internal readonly string Name;
        internal readonly string ValidationMethodName;

        private SqlUdtInfo(SqlUserDefinedTypeAttribute attr)
        {
            SerializationFormat = (Format)attr.Format;
            IsByteOrdered = attr.IsByteOrdered;
            IsFixedLength = attr.IsFixedLength;
            MaxByteSize = attr.MaxByteSize;
            Name = attr.Name;
            ValidationMethodName = attr.ValidationMethodName;
        }
        internal static SqlUdtInfo GetFromType(Type target)
        {
            SqlUdtInfo udtAttr = TryGetFromType(target);
            if (udtAttr == null)
            {
                Type myType = typeof(InvalidUdtException);
                var arguments = new Type[] { typeof(Type), typeof(String) };
                MethodInfo Create = myType.GetMethod("Create", arguments);
                Create.Invoke(null, new object[] { Strings.SqlUdtReason_NoUdtAttribute });
            }
            return udtAttr;
        }

        // VSTFDEVDIV 479671: Type.GetCustomAttributes is an time-expensive call.
        // Improve UDT serialization performance by caching the resulted UDT type information using type-safe dictionary.
        // Use a per-thread cache, so we do not need to synchronize access to it
        [ThreadStatic]
        private static Dictionary<Type, SqlUdtInfo> m_types2UdtInfo;

        internal static SqlUdtInfo TryGetFromType(Type target)
        {
            if (m_types2UdtInfo == null)
                m_types2UdtInfo = new Dictionary<Type, SqlUdtInfo>();

            SqlUdtInfo udtAttr = null;
            if (!m_types2UdtInfo.TryGetValue(target, out udtAttr))
            {
                // query SqlUserDefinedTypeAttribute first time and cache the result
                object[] attr = target.GetCustomAttributes(typeof(SqlUserDefinedTypeAttribute), false);
                if (attr != null && attr.Length == 1)
                {
                    udtAttr = new SqlUdtInfo((SqlUserDefinedTypeAttribute)attr[0]);
                }
                m_types2UdtInfo.Add(target, udtAttr);
            }
            return udtAttr;
        }
    }
}
