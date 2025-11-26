// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Data;
using System.Diagnostics;
using IsolationLevel = System.Data.IsolationLevel;

namespace Microsoft.Data.Common
{
    internal static partial class ADP
    {
        // COM+
        internal static PlatformNotSupportedException DbTypeNotSupported(string dbType) =>
            new(StringsHelper.GetString(Strings.SQL_DbTypeNotSupportedOnThisPlatform, dbType));

        // ConnectionUtil
        internal static Exception IncorrectPhysicalConnectionType() =>
            new ArgumentException(StringsHelper.GetString(Strings.SNI_IncorrectPhysicalConnectionType));
    }
}

#endif
