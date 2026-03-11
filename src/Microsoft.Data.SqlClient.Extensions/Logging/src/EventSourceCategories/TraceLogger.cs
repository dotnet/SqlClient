// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Data.SqlClient.Extensions.Logging.EventSourceCategories;

internal sealed class TraceLogger : ILogger
{
    public static TraceLogger Instance => field ??= new TraceLogger();
    private TraceLogger() { }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        if (!SqlClientEventSource.Log.IsScopeEnabled())
        {
            return null;
        }

        return SqlClientEventAsyncScope.Create("{0}", state);
    }

    public bool IsEnabled(LogLevel logLevel) =>
        SqlClientEventSource.Log.IsTraceEnabled() || SqlClientEventSource.Log.IsScopeEnabled();

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!SqlClientEventSource.Log.IsTraceEnabled())
        {
            return;
        }

        string message = formatter(state, exception);

        SqlClientEventSource.Log.Trace(message);
    }
}
