// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.Data.SqlClient.Tests.Common;

/// <summary>Holds parameter streaming incomplete until a test releases or faults the read.</summary>
public sealed class GatedReadStream : MemoryStream
{
    private readonly TaskCompletionSource<int> _read =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Reports whether parameter serialization has reached the asynchronous read.</summary>
    public bool ReadStarted { get; private set; }

    /// <summary>Suspends parameter serialization without relying on network buffer sizes.</summary>
    public override Task<int> ReadAsync(
        byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ReadStarted = true;
        return _read.Task;
    }

    /// <summary>Completes the parameter as an empty binary value.</summary>
    public void Complete() => _read.TrySetResult(0);

    /// <summary>Fails serialization before an RPC reaches the simulated server.</summary>
    public void Fail() => _read.TrySetException(new IOException("Parameter stream failure."));
}
