// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

﻿using System;
using System.Runtime.InteropServices;

namespace Microsoft.Data.SqlClient.Tests
{
    public static class TestUtility
    {
        public static readonly bool s_isNotArmProcess = RuntimeInformation.ProcessArchitecture != Architecture.Arm;
        public static bool s_isFullFramework => RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework");
        public static bool s_netNative => RuntimeInformation.FrameworkDescription.StartsWith(".NET Native");
    }
}
