// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient.Server
{

    internal partial class SerializationHelperSql9
    {

        private static SqlUserDefinedTypeAttribute GetUdtAttributeFrameworkSpecific(Type t)
        {
            SqlUserDefinedTypeAttribute udtAttr = null;
            object[] attr = GetCustomAttributes(t);

            if (attr != null && attr.Length == 1)
            {
                udtAttr = (SqlUserDefinedTypeAttribute)attr[0];
            }
            else
            {
                Type InvalidUdtExceptionType = typeof(InvalidUdtException);
                var arguments = new Type[] { typeof(Type), typeof(String) };
                MethodInfo Create = InvalidUdtExceptionType.GetMethod("Create", arguments);
                Create.Invoke(null, new object[] { t, Strings.SqlUdtReason_NoUdtAttribute });
            }
            return udtAttr;
        }
    }

    internal sealed partial class BinarySerializeSerializer : Serializer
    {
        private void SerializeFrameworkSpecific(Stream s, object o)
        {
            BinaryWriter w = new BinaryWriter(s);
            if (o is Microsoft.SqlServer.Server.IBinarySerialize)
            {
                ((SqlServer.Server.IBinarySerialize)o).Write(w);
            }
            else
            {
                ((IBinarySerialize)o).Write(w);
            }
        }

        private object DeserializeFrameworkSpecific(Stream s)
        {
            object instance = Activator.CreateInstance(_type);
            BinaryReader r = new BinaryReader(s);
            if (instance is Microsoft.SqlServer.Server.IBinarySerialize)
            {
                ((SqlServer.Server.IBinarySerialize)instance).Read(r);
            }
            else
            {
                ((IBinarySerialize)instance).Read(r);
            }
            return instance;
        }

    }


}
