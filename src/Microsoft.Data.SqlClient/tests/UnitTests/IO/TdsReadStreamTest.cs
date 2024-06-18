// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.IO;
using Xunit;

namespace Microsoft.Data.SqlClient.NetCore.UnitTests.IO
{
    public class TdsReadStreamTest
    {
        [Fact]
        public void ReadStream_ReadSinglePacket()
        {
            MemoryStream underlyingStream = new();
            TdsReadStream stream = new TdsReadStream(underlyingStream);
            
        }
    }
}
