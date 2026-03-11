// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Data.SqlClient.Extensions.Azure;

internal static partial class LogMessages
{
    [LoggerMessage(EventId = 11, EventName = nameof(BeforeLoadAuthenticationProvider),
        Level = LogLevel.Information,
        Message = $"<sc|{nameof(ActiveDirectoryAuthenticationProvider)}|{nameof(ActiveDirectoryAuthenticationProvider.BeforeLoad)}|INFO> Being loaded into SqlAuthProviders for {{authentication}}")]
    public static partial void BeforeLoadAuthenticationProvider(this ILogger logger, SqlAuthenticationMethod authentication);

    [LoggerMessage(EventId = 12, EventName = nameof(BeforeUnloadAuthenticationProvider),
        Level = LogLevel.Information,
        Message = $"<sc|{nameof(ActiveDirectoryAuthenticationProvider)}|{nameof(ActiveDirectoryAuthenticationProvider.BeforeUnload)}|INFO> Being unloaded from SqlAuthProviders for {{authentication}}")]
    public static partial void BeforeUnloadAuthenticationProvider(this ILogger logger, SqlAuthenticationMethod authentication);

    [LoggerMessage(EventId = 13, EventName = nameof(AcquiredDefaultAuthAccessToken),
        Level = LogLevel.Information,
        Message = $"{nameof(ActiveDirectoryAuthenticationProvider.AcquireTokenAsync)} | Acquired access token for Default auth mode. Expiry Time: {{expiryTime}}")]
    public static partial void AcquiredDefaultAuthAccessToken(this ILogger logger, DateTimeOffset expiryTime);

    [LoggerMessage(EventId = 14, EventName = nameof(AcquiredManagedIdentityAuthAccessToken),
        Level = LogLevel.Information,
        Message = $"{nameof(ActiveDirectoryAuthenticationProvider.AcquireTokenAsync)} | Acquired access token for Managed Identity auth mode. Expiry Time: {{expiryTime}}")]
    public static partial void AcquiredManagedIdentityAuthAccessToken(this ILogger logger, DateTimeOffset expiryTime);

    [LoggerMessage(EventId = 15, EventName = nameof(AcquiredActiveDirectoryServicePrincipalAuthAccessToken),
        Level = LogLevel.Information,
        Message = $"{nameof(ActiveDirectoryAuthenticationProvider.AcquireTokenAsync)} | Acquired access token for Active Directory Service Principal auth mode. Expiry Time: {{expiryTime}}")]
    public static partial void AcquiredActiveDirectoryServicePrincipalAuthAccessToken(this ILogger logger, DateTimeOffset expiryTime);

    [LoggerMessage(EventId = 16, EventName = nameof(AcquiredWorkloadIdentityAuthAccessToken),
        Level = LogLevel.Information,
        Message = $"{nameof(ActiveDirectoryAuthenticationProvider.AcquireTokenAsync)} | Acquired access token for Workload Identity auth mode. Expiry Time: {{expiryTime}}")]
    public static partial void AcquiredWorkloadIdentityAuthAccessToken(this ILogger logger, DateTimeOffset expiryTime);

    [LoggerMessage(EventId = 17, EventName = nameof(AcquiredActiveDirectoryIntegratedAuthAccessToken),
        Level = LogLevel.Information,
        Message = $"{nameof(ActiveDirectoryAuthenticationProvider.AcquireTokenAsync)} | Acquired access token for Active Directory Integrated auth mode. Expiry Time: {{expiryTime}}")]
    public static partial void AcquiredActiveDirectoryIntegratedAuthAccessToken(this ILogger logger, DateTimeOffset? expiryTime);

    [LoggerMessage(EventId = 18, EventName = nameof(AcquiredActiveDirectoryPasswordAuthAccessToken),
        Level = LogLevel.Information,
        Message = $"{nameof(ActiveDirectoryAuthenticationProvider.AcquireTokenAsync)} | Acquired access token for Active Directory Password auth mode. Expiry Time: {{expiryTime}}")]
    public static partial void AcquiredActiveDirectoryPasswordAuthAccessToken(this ILogger logger, DateTimeOffset? expiryTime);

    [LoggerMessage(EventId = 19, EventName = nameof(AcquiredSilentAuthAccessToken),
        Level = LogLevel.Information,
        Message = $"{nameof(ActiveDirectoryAuthenticationProvider.AcquireTokenAsync)} | Acquired access token (silent) for {{authMethod}} auth mode. Expiry Time: {{expiryTime}}")]
    public static partial void AcquiredSilentAuthAccessToken(this ILogger logger, SqlAuthenticationMethod authMethod, DateTimeOffset? expiryTime);

    [LoggerMessage(EventId = 20, EventName = nameof(AcquiredInteractiveAuthAccessToken),
        Level = LogLevel.Information,
        Message = $"{nameof(ActiveDirectoryAuthenticationProvider.AcquireTokenAsync)} | Acquired access token (interactive) for {{authMethod}} auth mode. Expiry Time: {{expiryTime}}")]
    public static partial void AcquiredInteractiveAuthAccessToken(this ILogger logger, SqlAuthenticationMethod authMethod, DateTimeOffset? expiryTime);

    [LoggerMessage(EventId = 21, EventName = nameof(UnsupportedAuthenticationMode),
        Level = LogLevel.Information,
        Message = $"{nameof(ActiveDirectoryAuthenticationProvider.AcquireTokenAsync)} | {{authMethod}} authentication mode not supported by {nameof(ActiveDirectoryAuthenticationProvider)} class.")]
    public static partial void UnsupportedAuthenticationMode(this ILogger logger, SqlAuthenticationMethod authMethod);

    [LoggerMessage(EventId = 22, EventName = nameof(AccessTokenAcquisitionTimeout),
        Level = LogLevel.Information,
        Message = $"AcquireTokenInteractiveDeviceFlowAsync | Operation timed out while acquiring access token.")]
    public static partial void AccessTokenAcquisitionTimeout(this ILogger logger);

    [LoggerMessage(EventId = 23, EventName = nameof(DeviceFlowCallbackTriggered),
        Level = LogLevel.Information,
        Message = $"AcquireTokenInteractiveDeviceFlowAsync | Callback triggered with Device Code Result: {{deviceCodeResult}}.")]
    public static partial void DeviceFlowCallbackTriggered(this ILogger logger, string deviceCodeResult);
}
