// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.Extensions.Abstractions.Logging;
using Microsoft.Data.SqlClient.Extensions.Logging.EventSourceCategories;
using Microsoft.Extensions.Logging;

namespace Microsoft.Data.SqlClient.Extensions.Logging;

/// <summary>
/// Represents a type that can create instances of <see cref="TraceLogger"/> which write
/// events to Microsoft.Data.SqlClient's dedicated event source.
/// </summary>
[ProviderAlias(nameof(SqlClientEventSourceLoggerProvider))]
public sealed class SqlClientEventSourceLoggerProvider : ILoggerProvider
{
    /// <summary>
    /// Creates a new <see cref="ILogger"/> instance.
    /// </summary>
    /// <param name="categoryName">The category name for messages produced by the logger.</param>
    /// <returns>The instance of <see cref="ILogger"/> that was created.</returns>
    public ILogger CreateLogger(string categoryName)
    {
        return categoryName switch
        {
            CategoryNames.Trace => TraceLogger.Instance,
            _ => TraceLogger.Instance,
        };
    }

    /// <summary>
    /// Empty implementation.
    /// </summary>
    public void Dispose()
    {
        // Implementation deliberately left blank.
    }
}
