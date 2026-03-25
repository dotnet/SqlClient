// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Data.SqlClient.Tests
{
    public static class TestUtility
    {
        public static readonly bool IsNotArmProcess = RuntimeInformation.ProcessArchitecture != Architecture.Arm;
        public static bool IsNet
        {
            get
            {
                return
                    !IsNetCore && !IsNetFramework && !IsNetNative &&
                    RuntimeInformation.FrameworkDescription.StartsWith(".NET");
            }
        }

        public static bool IsNetCore => RuntimeInformation.FrameworkDescription.StartsWith(".NET Core");
        public static bool IsNetFramework => RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework");
        public static bool IsNetNative => RuntimeInformation.FrameworkDescription.StartsWith(".NET Native");
    }
}
