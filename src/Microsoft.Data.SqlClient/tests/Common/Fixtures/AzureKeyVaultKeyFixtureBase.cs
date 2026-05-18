// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.using System;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Azure.Core;
using Azure.Security.KeyVault.Keys;

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures;

/// <summary>
/// Provides a base class for managing Azure Key Vault keys in test fixtures.
/// </summary>
/// <remarks>
/// This class simplifies the creation and cleanup of RSA keys in an Azure Key Vault during testing
/// scenarios. It ensures that any keys created during the fixture's lifetime are properly deleted when the fixture is
/// disposed.
/// </remarks>
public abstract class AzureKeyVaultKeyFixtureBase : IDisposable
{
    private readonly KeyClient _keyClient;
    private readonly RandomNumberGenerator _randomGenerator;

    private readonly List<KeyVaultKey> _createdKeys = new List<KeyVaultKey>();

    protected AzureKeyVaultKeyFixtureBase(Uri keyVaultUri, TokenCredential keyVaultToken)
    {
        _keyClient = new KeyClient(keyVaultUri, keyVaultToken);
        _randomGenerator = RandomNumberGenerator.Create();
    }

    protected Uri CreateKey(string name, int keySize)
    {
        const int MaxConflictResolutions = 5;
        KeyVaultKey created;
        int i = 0;

        while (true)
        {
            CreateRsaKeyOptions createOptions = new CreateRsaKeyOptions(GenerateUniqueName(name)) { KeySize = keySize };

            try
            {
                created = _keyClient.CreateRsaKey(createOptions);
                break;
            }
            // It's possible for a key to already exist with the same name, even in a deleted state. If so, CreateRsaKey
            // will throw an exception with HTTP status code 409 (Conflict.)
            // We can't assume we possess permissions to purge or to recover the key, so regenerate the name and try again.
            // Only make MaxConflictResolutions attempts, to avoid possible infinite loops.
            catch (Azure.RequestFailedException conflictException)
                when (conflictException.Status == 409 && i < MaxConflictResolutions)
            {
                i++;
            }
        }

        _createdKeys.Add(created);
        return created.Id;
    }

    private string GenerateUniqueName(string name)
    {
        byte[] rndBytes = new byte[16];

        _randomGenerator.GetBytes(rndBytes);
        return name + "-" + BitConverter.ToString(rndBytes);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        foreach (KeyVaultKey key in _createdKeys)
        {
            try
            {
                _keyClient.StartDeleteKey(key.Name).WaitForCompletion();
            }
            catch (Exception)
            {
                continue;
            }
        }

        _randomGenerator.Dispose();
    }
}
