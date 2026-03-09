using Microsoft.Extensions.Logging;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Logging;

internal static partial class LogMessages
{
    [LoggerMessage(EventId = 1, EventName = nameof(AssemblyNotFound),
        Level = LogLevel.Warning,
        Message = $"SqlAuthenticationProvider.Internal | MDS assembly={{assemblyName}} not found; Get/SetProvider() will not function.")]
    public static partial void AssemblyNotFound(this ILogger logger, string assemblyName);

    [LoggerMessage(EventId = 2, EventName = nameof(AuthManagerClassNotFound),
        Level = LogLevel.Warning,
        Message = $"SqlAuthenticationProvider.Internal | MDS auth manager class {{className}} not found; Get/SetProvider() will not function.")]
    public static partial void AuthManagerClassNotFound(this ILogger logger, string className);

    [LoggerMessage(EventId = 3, EventName = nameof(GetProviderMethodNotFound),
        Level = LogLevel.Warning,
        Message = $"SqlAuthenticationProvider.Internal | MDS GetProvider() method not found; Get/SetProvider() will not function.")]
    public static partial void GetProviderMethodNotFound(this ILogger logger);

    [LoggerMessage(EventId = 4, EventName = nameof(SetProviderMethodNotFound),
        Level = LogLevel.Warning,
        Message = $"SqlAuthenticationProvider.Internal | MDS SetProvider() method not found; Get/SetProvider() will not function.")]
    public static partial void SetProviderMethodNotFound(this ILogger logger);

    [LoggerMessage(EventId = 5, EventName = nameof(AssemblyNotFoundOrUsable),
        Level = LogLevel.Warning,
        Message = $"SqlAuthenticationProvider.Internal | MDS assembly={{assemblyName}} not found or not usable; Get/SetProvider() will not function: {{exceptionString}}")]
    public static partial void AssemblyNotFoundOrUsable(this ILogger logger, string assemblyName, string exceptionString);

    [LoggerMessage(EventId = 6, EventName = nameof(GetProviderInvocationFailed),
        Level = LogLevel.Warning,
        Message = $"SqlAuthenticationProvider.Internal | GetProvider() invocation failed: {{exceptionType}}: {{exceptionString}}")]
    public static partial void GetProviderInvocationFailed(this ILogger logger, string exceptionType, string exceptionString);

    [LoggerMessage(EventId = 7, EventName = nameof(SetProviderInvocationReturnedNull),
        Level = LogLevel.Warning,
        Message = $"SqlAuthenticationProvider.Internal | SetProvider() invocation returned null; translating to false.")]
    public static partial void SetProviderInvocationReturnedNull(this ILogger logger);

    [LoggerMessage(EventId = 8, EventName = nameof(SetProviderInvocationFailed),
        Level = LogLevel.Warning,
        Message = $"SqlAuthenticationProvider.Internal | SetProvider() invocation failed: {{exceptionType}}: {{exceptionString}}")]
    public static partial void SetProviderInvocationFailed(this ILogger logger, string exceptionType, string exceptionString);
}
