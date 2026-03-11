using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Logging;

/// <summary>
/// <see cref="ILogger"/> extension methods for use by Microsoft.Data.SqlClient.
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Begins a logical operation scope, defined by the class name, calling member and a scope identifier.
    /// </summary>
    /// <param name="logger"><see cref="ILogger"/> instance to use to begin the scope.</param>
    /// <param name="className">Class containing the calling member.</param>
    /// <param name="memberName">Name of the calling member.</param>
    /// <returns>An <see cref="IDisposable"/> that ends the logical operation scope on dispose.</returns>
    public static IDisposable? BeginMemberScope(this ILogger logger, string className, [CallerMemberName] string memberName = "") =>
        logger.BeginScope($"{className}.{memberName} | INFO | SCOPE | Entering Scope {{0}}");
}
