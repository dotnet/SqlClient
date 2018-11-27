// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    class TestFixtures
    {
        public static byte[] GenerateTestEncryptedBytes(byte version, short keyPathLength, short ciphertextLength, short signature)
        {
            byte[] v = new byte[] { version };
            byte[] kpl = BitConverter.GetBytes(keyPathLength);
            byte[] ctl = BitConverter.GetBytes(ciphertextLength);

            int index = 0;
            byte[] data = new byte[v.Length + kpl.Length + ctl.Length + keyPathLength + ciphertextLength + signature];
            Buffer.BlockCopy(v, 0, data, index, v.Length);
            index += v.Length;
            Buffer.BlockCopy(kpl, 0, data, index, kpl.Length);
            index += kpl.Length;
            Buffer.BlockCopy(ctl, 0, data, index, ctl.Length);

            return data;
        }
    }
}
