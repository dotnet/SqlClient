// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET
using System.Threading.Tasks;
using System.Threading;

internal struct PoolPruningParameters
{
    internal int _minIdleCount;
    internal readonly PeriodicTimer PruningTimer { get; init; }
    internal readonly PeriodicTimer MinIdleCountTimer { get; init; }
    internal readonly ValueTask PruningTimerListener { get; init; }
    internal Task PruningTask { get; set; }
    internal readonly ValueTask UpdateMinIdleCountTask { get; init; }
    internal readonly SemaphoreSlim PruningLock { get; init; }
}
#endif