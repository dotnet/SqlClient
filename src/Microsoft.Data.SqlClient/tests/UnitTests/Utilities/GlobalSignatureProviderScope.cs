// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities
{
    /// <summary>
    /// Temporarily installs a global provider and removes its unique signature-cache entry on exit.
    /// Must be used in a collection with parallelization disabled.
    /// </summary>
    internal sealed class GlobalSignatureProviderScope : IDisposable
    {
        private readonly FieldInfo _providersField;
        private readonly object? _originalProviders;
        private readonly MemoryCache _cache;
        private readonly string _cacheKey;

        internal string ProviderName { get; } = "TEST_SIGNATURE_" + Guid.NewGuid().ToString("N");
        internal string KeyPath { get; } = "signature-key-" + Guid.NewGuid().ToString("N");
        internal byte[] Signature { get; } = Guid.NewGuid().ToByteArray();

        /// <summary>Installs a provider without permanently consuming one-time global registration.</summary>
        /// <param name="provider">Provider to use for this scope's unique signature metadata.</param>
        internal GlobalSignatureProviderScope(SqlColumnEncryptionKeyStoreProvider provider)
        {
            _providersField = typeof(SqlConnection).GetField(
                "s_globalCustomColumnEncryptionKeyStoreProviders",
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(typeof(SqlConnection).FullName,
                    "s_globalCustomColumnEncryptionKeyStoreProviders");
            _originalProviders = _providersField.GetValue(null);

            Type cacheType = typeof(ColumnMasterKeyMetadataSignatureVerificationCache);
            FieldInfo cacheField = cacheType.GetField(
                "_cache", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(cacheType.FullName, "_cache");
            _cache = (MemoryCache)cacheField.GetValue(
                ColumnMasterKeyMetadataSignatureVerificationCache.Instance)!;
            MethodInfo keyMethod = cacheType.GetMethod(
                "GetCacheLookupKey", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(cacheType.FullName, "GetCacheLookupKey");
            _cacheKey = (string)keyMethod.Invoke(
                null, new object[] { KeyPath, true, Signature, ProviderName })!;

            Dictionary<string, SqlColumnEncryptionKeyStoreProvider> providers = new(
                StringComparer.OrdinalIgnoreCase);
            IReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider>? original =
                (IReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider>?)_originalProviders;
            if (original is not null)
            {
                foreach (KeyValuePair<string, SqlColumnEncryptionKeyStoreProvider> entry in original)
                {
                    providers.Add(entry.Key, entry.Value);
                }
            }
            providers.Add(ProviderName, provider);
            _providersField.SetValue(null, providers);
        }

        /// <summary>Restores prior registrations and removes only this scope's cached result.</summary>
        public void Dispose()
        {
            _providersField.SetValue(null, _originalProviders);
            _cache.Remove(_cacheKey);
        }
    }
}
