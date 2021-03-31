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

            e = Assert.Throws<ArgumentNullException>(() => connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(customProviders));
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

            e = Assert.Throws<ArgumentException>(() => connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(customProviders));
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

            e = Assert.Throws<ArgumentNullException>(() => connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(customProviders));
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

            e = Assert.Throws<ArgumentNullException>(() => connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(customProviders));
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

        [Fact]
        public void TestCanSetInstanceProvidersMoreThanOnce()
        {
            const string dummyProviderName1 = "DummyProvider1";
            const string dummyProviderName2 = "DummyProvider2";
            const string dummyProviderName3 = "DummyProvider3";
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> singleKeyStoreProvider =
                new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>()
                {
                    {dummyProviderName1, new DummyKeyStoreProvider() }
                };

            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> multipleKeyStoreProviders =
                new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>()
                {
                    { dummyProviderName2, new DummyKeyStoreProvider() },
                    { dummyProviderName3, new DummyKeyStoreProvider() }
                };

            using (SqlConnection connection = new SqlConnection())
            {
                connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(singleKeyStoreProvider);
                IReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider> instanceCache =
                    GetInstanceCacheFromConnection(connection);
                Assert.Single(instanceCache);
                Assert.True(instanceCache.ContainsKey(dummyProviderName1));

                connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(multipleKeyStoreProviders);
                instanceCache = GetInstanceCacheFromConnection(connection);
                Assert.Equal(2, instanceCache.Count);
                Assert.True(instanceCache.ContainsKey(dummyProviderName2));
                Assert.True(instanceCache.ContainsKey(dummyProviderName3));
            }

            IReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider> GetInstanceCacheFromConnection(SqlConnection conn)
            {
                FieldInfo instanceCacheField = conn.GetType().GetField(
                    "_customColumnEncryptionKeyStoreProviders", BindingFlags.NonPublic | BindingFlags.Instance);
                return instanceCacheField.GetValue(conn) as IReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider>;
            }
        }
    }
}
