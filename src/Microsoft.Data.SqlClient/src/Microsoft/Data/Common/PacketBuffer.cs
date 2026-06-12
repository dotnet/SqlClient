// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;

#nullable enable

namespace Microsoft.Data.Common;

/// <summary>
/// One buffer, which may contain one unparsed packet from a single destination.
/// </summary>
internal sealed class PacketBuffer : ReadOnlySequenceSegment<byte>
{
    public PacketBuffer(ReadOnlyMemory<byte> buffer, PacketBuffer? previous)
    {
        Memory = buffer;

        if (previous is not null)
        {
            previous.Next = this;
            RunningIndex = previous.RunningIndex + previous.Memory.Length;
        }
        else
        {
            RunningIndex = 0;
        }
    }
}
