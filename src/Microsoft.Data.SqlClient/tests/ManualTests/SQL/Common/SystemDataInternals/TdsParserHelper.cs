// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SystemDataInternals
{
    internal static class TdsParserHelper
    {
        private static FieldInfo s_tdsParserPhysicalStateObject = typeof(TdsParser).GetField("_physicalStateObj", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static object GetStateObject(TdsParser parser)
        {
            return s_tdsParserPhysicalStateObject.GetValue(parser);
        }
    }
}
