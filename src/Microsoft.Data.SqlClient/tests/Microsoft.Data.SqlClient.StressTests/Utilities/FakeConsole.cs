// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Data.SqlClient.StressTests.Utilities
{
    // @TODO: Replace with usages of console streams.
    public static class FakeConsole
    {
        public static void Write(string value)
        {
#if DEBUG
            Console.Write(value);
#endif
        }

        public static void WriteLine(string value)
        {
#if DEBUG
            Console.WriteLine(value);
#endif
        }

        public static void WriteLine(string format, params object[] arg)
        {
#if DEBUG
            Console.WriteLine(format, arg);
#endif
        }
    }
}
