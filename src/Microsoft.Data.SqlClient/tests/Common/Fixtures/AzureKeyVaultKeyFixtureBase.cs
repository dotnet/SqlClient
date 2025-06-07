// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.using System;

using System;
using System.Collections.Generic;
using Azure.Core;
using Azure.Security.KeyVault.Keys;

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures;

public abstract class AzureKeyVaultKeyFixtureBase : IDisposable
{
    private readonly KeyClient _keyClient;
    private readonly Random _randomGenerator;

    private readonly List<KeyVaultKey> _createdKeys = new List<KeyVaultKey>();

    protected AzureKeyVaultKeyFixtureBase(Uri keyVaultUri, TokenCredential keyVaultToken)
    {
        _keyClient = new KeyClient(keyVaultUri, keyVaultToken);
        _randomGenerator = new Random();
    }

    protected Uri CreateKey(string name, int keySize)
    {
        CreateRsaKeyOptions createOptions = new CreateRsaKeyOptions(GenerateUniqueName(name)) { KeySize = keySize };
        KeyVaultKey created = _keyClient.CreateRsaKey(createOptions);

        _createdKeys.Add(created);
        return created.Id;
    }

    private string GenerateUniqueName(string name)
    {
        byte[] rndBytes = new byte[16];

        _randomGenerator.NextBytes(rndBytes);
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
    }
}
