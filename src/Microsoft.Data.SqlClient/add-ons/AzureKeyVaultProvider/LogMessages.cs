// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;

internal static partial class LogMessages
{
    [LoggerMessage(EventId = 31, EventName = nameof(FetchedKeyFromCache),
        Level = LogLevel.Information,
        Message = $"Fetched key name={{keyName}} from cache")]
    public static partial void FetchedKeyFromCache(this ILogger logger, string keyName);

    [LoggerMessage(EventId = 32, EventName = nameof(KeyMissingFromCache),
        Level = LogLevel.Information,
        Message = $"Key not found; URI={{keyIdentifierUri}}")]
    public static partial void KeyMissingFromCache(this ILogger logger, string keyIdentifierUri);

    [LoggerMessage(EventId = 33, EventName = nameof(VerifyingData),
        Level = LogLevel.Information,
        Message = $"Sending request to verify data.")]
    public static partial void VerifyingData(this ILogger logger);

    [LoggerMessage(EventId = 34, EventName = nameof(UnwrappingKey),
        Level = LogLevel.Information,
        Message = $"Sending request to unwrap key.")]
    public static partial void UnwrappingKey(this ILogger logger);

    [LoggerMessage(EventId = 35, EventName = nameof(WrappingKey),
        Level = LogLevel.Information,
        Message = $"Sending request to wrap key.")]
    public static partial void WrappingKey(this ILogger logger);

    [LoggerMessage(EventId = 36, EventName = nameof(FetchingKeyFromKeyVault),
        Level = LogLevel.Information,
        Message = $"Fetching key name={{keyName}}")]
    public static partial void FetchingKeyFromKeyVault(this ILogger logger, string keyName);

    [LoggerMessage(EventId = 37, EventName = nameof(InvalidKeyVaultResponse),
        Level = LogLevel.Error,
        Message = $"Get Key failed to fetch Key from Azure Key Vault for key {{keyName}}, version {{keyVersion}}")]
    public static partial void InvalidKeyVaultResponse(this ILogger logger, string keyName, string keyVersion);

    [LoggerMessage(EventId = 38, EventName = nameof(InvalidKeyVaultResponseCode),
        Level = LogLevel.Error,
        Message = $"Response status {{statusCode}} : {{reasonPhrase}}")]
    public static partial void InvalidKeyVaultResponseCode(this ILogger logger, int statusCode, string reasonPhrase);

    [LoggerMessage(EventId = 39, EventName = nameof(NonRsaKeyRetrieved),
        Level = LogLevel.Error,
        Message = $"Non-RSA KeyType received: {{keyType}}")]
    public static partial void NonRsaKeyRetrieved(this ILogger logger, string keyType);

    [LoggerMessage(EventId = 40, EventName = nameof(ReceivedKeyName),
        Level = LogLevel.Information,
        Message = $"Received Key Name: {{keyName}}")]
    public static partial void ReceivedKeyName(this ILogger logger, string keyName);

    [LoggerMessage(EventId = 41, EventName = nameof(ReceivedKeyVersion),
        Level = LogLevel.Information,
        Message = $"Received Key Version: {{keyVersion}}")]
    public static partial void ReceivedKeyVersion(this ILogger logger, string keyVersion);

    [LoggerMessage(EventId = 42, EventName = nameof(KeyCachingDisabled),
        Level = LogLevel.Information,
        Message = $"Key caching found disabled, fetching key information.")]
    public static partial void KeyCachingDisabled(this ILogger logger);

    [LoggerMessage(EventId = 43, EventName = nameof(CachedEntryNotFound),
        Level = LogLevel.Information,
        Message = $"Cached entry not found, creating new entry.")]
    public static partial void CachedEntryNotFound(this ILogger logger);

    [LoggerMessage(EventId = 44, EventName = nameof(AddedEntryToCache),
        Level = LogLevel.Information,
        Message = $"Entry added to local cache.")]
    public static partial void AddedEntryToCache(this ILogger logger);

    [LoggerMessage(EventId = 45, EventName = nameof(CachedEntryFound),
        Level = LogLevel.Information,
        Message = $"Cached entry found.")]
    public static partial void CachedEntryFound(this ILogger logger);

    [LoggerMessage(EventId = 46, EventName = nameof(InvalidCipherTextLength),
        Level = LogLevel.Error,
        Message = $"Cipher Text length: {{cipherTextLength}}; Key size (bytes): {{keySizeInBytes}}")]
    public static partial void InvalidCipherTextLength(this ILogger logger, int cipherTextLength, int keySizeInBytes);

    [LoggerMessage(EventId = 47, EventName = nameof(InvalidSignatureLength),
        Level = LogLevel.Error,
        Message = $"Signature length: {{signatureTextLength}}; Key size (bytes): {{keySizeInBytes}}")]
    public static partial void InvalidSignatureLength(this ILogger logger, int signatureTextLength, int keySizeInBytes);

    [LoggerMessage(EventId = 48, EventName = nameof(CouldNotVerifySignature),
        Level = LogLevel.Error,
        Message = $"Signature could not be verified.")]
    public static partial void CouldNotVerifySignature(this ILogger logger);

    [LoggerMessage(EventId = 49, EventName = nameof(NullOrEmptyUri),
        Level = LogLevel.Error,
        Message = $"Azure Key Vault URI found null or empty.")]
    public static partial void NullOrEmptyUri(this ILogger logger);

    [LoggerMessage(EventId = 50, EventName = nameof(MasterKeyPathNotValidUrl),
        Level = LogLevel.Error,
        Message = $"URI could not be created with provided master key path: {{masterKeyPath}}")]
    public static partial void MasterKeyPathNotValidUrl(this ILogger logger, string masterKeyPath);

    [LoggerMessage(EventId = 51, EventName = nameof(MasterKeyPathValid),
        Level = LogLevel.Information,
        Message = $"Azure Key Vault URI validated successfully.")]
    public static partial void MasterKeyPathValid(this ILogger logger);

    [LoggerMessage(EventId = 52, EventName = nameof(MasterKeyPathNotTrustedUrl),
        Level = LogLevel.Error,
        Message = $"Master Key Path could not be validated as it does not end with trusted endpoints: {{masterKeyPath}}")]
    public static partial void MasterKeyPathNotTrustedUrl(this ILogger logger, string masterKeyPath);

    [LoggerMessage(EventId = 53, EventName = nameof(VerifiedSignature),
        Level = LogLevel.Information,
        Message = $"Signature verified successfully.")]
    public static partial void VerifiedSignature(this ILogger logger);
}
