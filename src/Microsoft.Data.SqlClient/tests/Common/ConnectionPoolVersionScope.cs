// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Tests.Common;

/// <summary>
/// Selects the connection pool implementation (<c>WaitHandleDbConnectionPool</c> or
/// <c>ChannelDbConnectionPool</c>) for the duration of a test.
///
/// A pool is bound to an implementation when it is created, so simply flipping the
/// <c>UseConnectionPoolV2</c> switch is not enough: pools created before the switch was flipped
/// keep their original implementation, and pools created inside the scope would otherwise outlive
/// it and leak the chosen implementation into unrelated tests. This scope therefore clears all
/// pools both on entry and on exit.
///
/// This follows the RAII pattern; construct it at the start of a test and dispose it at the end.
/// Like <see cref="LocalAppContextSwitchesHelper"/>, it manipulates global state and enforces a
/// single-instance policy, so it must not be held for longer than necessary.
/// </summary>
public sealed class ConnectionPoolVersionScope : IDisposable
{
    private readonly LocalAppContextSwitchesHelper _switches;

    /// <summary>
    /// Clears all existing pools and selects the requested pool implementation.
    /// </summary>
    /// <param name="usePoolV2">
    /// True to use <c>ChannelDbConnectionPool</c>; false to use <c>WaitHandleDbConnectionPool</c>.
    /// </param>
    public ConnectionPoolVersionScope(bool usePoolV2)
    {
        _switches = new LocalAppContextSwitchesHelper();

        try
        {
            SqlConnection.ClearAllPools();
            _switches.UseConnectionPoolV2 = usePoolV2;
        }
        catch
        {
            _switches.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Clears all pools created under the selected implementation and restores the original
    /// switch values.
    /// </summary>
    public void Dispose()
    {
        try
        {
            SqlConnection.ClearAllPools();
        }
        finally
        {
            _switches.Dispose();
        }
    }
}
