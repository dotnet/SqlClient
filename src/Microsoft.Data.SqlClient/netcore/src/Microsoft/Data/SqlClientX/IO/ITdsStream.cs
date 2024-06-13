// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.Data.SqlClientX.IO
{
    internal interface ITdsStream
    {
        public void SetPacketSize(int bufferSize);

        public void ReplaceUnderlyingStream(Stream stream);
    }
}
