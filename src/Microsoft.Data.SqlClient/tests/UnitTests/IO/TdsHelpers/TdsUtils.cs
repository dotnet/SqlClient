// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.UnitTests.IO.TdsHelpers
{
    internal class TdsUtils
    {
        public static ushort GenerateSpid() => (ushort)new Random().Next();
    }
}
