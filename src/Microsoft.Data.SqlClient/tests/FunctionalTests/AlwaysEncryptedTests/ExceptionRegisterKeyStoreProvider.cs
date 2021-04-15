// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    public class ExceptionRegisterKeyStoreProvider
    {
        private SqlConnection connection = new SqlConnection();

        [Fact]
        public void TestNullDictionary()
        {
            // Verify that we are unable to set null dictionary.
            string expectedMessage = SystemDataResourceManager.Instance.TCE_NullCustomKeyStoreProviderDictionary;
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders = null;

            ArgumentNullException e = Assert.Throws<ArgumentNullException>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders));
            Assert.Contains(expectedMessage, e.Message);
        }

        [Fact]
        public void TestInvalidProviderName()
        {
            // Verify the namespace reservation
            string providerWithReservedSystemPrefix = "MSSQL_DUMMY";
            string expectedMessage = string.Format(SystemDataResourceManager.Instance.TCE_InvalidCustomKeyStoreProviderName, providerWithReservedSystemPrefix, "MSSQL_");
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
            customProviders.Add(providerWithReservedSystemPrefix, new DummyKeyStoreProvider());

            ArgumentException e = Assert.Throws<ArgumentException>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders));
            Assert.Contains(expectedMessage, e.Message);
        }

        [Fact]
        public void TestNullProviderValue()
        {
            // Verify null provider value are not supported
            string providerName = "DUMMY";
            string expectedMessage = string.Format(SystemDataResourceManager.Instance.TCE_NullProviderValue, providerName);
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
            customProviders.Add(providerName, null);

            ArgumentNullException e = Assert.Throws<ArgumentNullException>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders));
            Assert.Contains(expectedMessage, e.Message);
        }

        [Fact]
        public void TestEmptyProviderName()
        {
            // Verify Empty provider names are not supported.
            string expectedMessage = SystemDataResourceManager.Instance.TCE_EmptyProviderName;
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
            customProviders.Add("   ", new DummyKeyStoreProvider());

            ArgumentNullException e = Assert.Throws<ArgumentNullException>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders));
            Assert.Contains(expectedMessage, e.Message);
        }

        [Fact]
        public void TestCanSetGlobalProvidersOnlyOnce()
        {
            Utility.ClearSqlConnectionGlobalProviders();

            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders =
                new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>()
                {
                    { DummyKeyStoreProvider.Name, new DummyKeyStoreProvider() }
                };
            SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders);

            InvalidOperationException e = Assert.Throws<InvalidOperationException>(
                () => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders));
            string expectedMessage = SystemDataResourceManager.Instance.TCE_CanOnlyCallOnce;
            Assert.Contains(expectedMessage, e.Message);

            Utility.ClearSqlConnectionGlobalProviders();
        }
    }
}
