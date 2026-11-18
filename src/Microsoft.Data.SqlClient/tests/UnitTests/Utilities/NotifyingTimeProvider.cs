// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.Extensions.Time.Testing;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities;

/// <summary>
/// Reports when a timer has been registered with the virtual clock.
/// </summary>
internal sealed class NotifyingTimeProvider : FakeTimeProvider
{
    private readonly Action _timerCreated;

    internal NotifyingTimeProvider(Action timerCreated)
    {
        _timerCreated = timerCreated;
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(
        TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ITimer timer = base.CreateTimer(callback, state, dueTime, period);
        _timerCreated();
        return timer;
    }
}
