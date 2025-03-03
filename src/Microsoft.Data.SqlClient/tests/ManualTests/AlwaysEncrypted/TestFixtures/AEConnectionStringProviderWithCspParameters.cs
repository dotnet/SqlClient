// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class AEConnectionStringProviderWithCspParameters : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            const string ProvidersRegistryKeyPath = @"SOFTWARE\Microsoft\Cryptography\Defaults\Provider";
            using Microsoft.Win32.RegistryKey defaultCryptoProvidersRegistryKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(ProvidersRegistryKeyPath);

            foreach (string subKeyName in defaultCryptoProvidersRegistryKey.GetSubKeyNames())
            {
                CspParameters providerCspParameters;

                // NOTE: RSACryptoServiceProvider.SignData() fails for other providers when testing locally
                if (!subKeyName.Contains(@"RSA and AES"))
                {
                    continue;
                }

                using (Microsoft.Win32.RegistryKey providerKey = defaultCryptoProvidersRegistryKey.OpenSubKey(subKeyName))
                {
                    // Get Provider Name and its type
                    string providerName = providerKey.Name.Substring(providerKey.Name.LastIndexOf(@"\", StringComparison.Ordinal) + 1);
                    int providerType = (int)providerKey.GetValue(@"Type");

                    providerCspParameters = new CspParameters(providerType, providerName);
                }

                foreach (string connStrAE in DataTestUtility.AEConnStrings)
                {
                    yield return new object[] { connStrAE, providerCspParameters };
                }
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
