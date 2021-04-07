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
        private SqlCommand command = new SqlCommand();

        private const string dummyProviderName1 = "DummyProvider1";
        private const string dummyProviderName2 = "DummyProvider2";
        private const string dummyProviderName3 = "DummyProvider3";

        private IDictionary<string, SqlColumnEncryptionKeyStoreProvider> singleKeyStoreProvider =
            new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>()
            {
                    {dummyProviderName1, new DummyKeyStoreProvider() }
            };

        private IDictionary<string, SqlColumnEncryptionKeyStoreProvider> multipleKeyStoreProviders =
            new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>()
            {
                    { dummyProviderName2, new DummyKeyStoreProvider() },
                    { dummyProviderName3, new DummyKeyStoreProvider() }
            };

        [Fact]
        public void TestNullDictionary()
        {
            // Verify that we are unable to set null dictionary.
            string expectedMessage = SystemDataResourceManager.Instance.TCE_NullCustomKeyStoreProviderDictionary;
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders = null;

            AssertForAllCustomProviderCaches<ArgumentNullException>(customProviders, expectedMessage);
        }

        [Fact]
        public void TestInvalidProviderName()
        {
            // Verify the namespace reservation
            string providerWithReservedSystemPrefix = "MSSQL_DUMMY";
            string expectedMessage = string.Format(SystemDataResourceManager.Instance.TCE_InvalidCustomKeyStoreProviderName,
                providerWithReservedSystemPrefix, "MSSQL_");
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders =
                new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
            customProviders.Add(providerWithReservedSystemPrefix, new DummyKeyStoreProvider());

            AssertForAllCustomProviderCaches<ArgumentException>(customProviders, expectedMessage);
        }

        [Fact]
        public void TestNullProviderValue()
        {
            // Verify null provider value are not supported
            string providerName = "DUMMY";
            string expectedMessage = string.Format(SystemDataResourceManager.Instance.TCE_NullProviderValue, providerName);
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders =
                new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
            customProviders.Add(providerName, null);

            AssertForAllCustomProviderCaches<ArgumentNullException>(customProviders, expectedMessage);
        }

        [Fact]
        public void TestEmptyProviderName()
        {
            // Verify Empty provider names are not supported.
            string expectedMessage = SystemDataResourceManager.Instance.TCE_EmptyProviderName;
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders =
                new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
            customProviders.Add("   ", new DummyKeyStoreProvider());

            AssertForAllCustomProviderCaches<ArgumentNullException>(customProviders, expectedMessage);
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
        public void TestCanSetConnectionInstanceProvidersMoreThanOnce()
        {
            connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(singleKeyStoreProvider);
            IReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider> instanceCache =
                GetInstanceCacheFrom(connection);
            Assert.Single(instanceCache);
            Assert.True(instanceCache.ContainsKey(dummyProviderName1));

            connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(multipleKeyStoreProviders);
            instanceCache = GetInstanceCacheFrom(connection);
            Assert.Equal(2, instanceCache.Count);
            Assert.True(instanceCache.ContainsKey(dummyProviderName2));
            Assert.True(instanceCache.ContainsKey(dummyProviderName3));
        }

        [Fact]
        public void TestCanSetCommandInstanceProvidersMoreThanOnce()
        {
            command.RegisterColumnEncryptionKeyStoreProvidersOnCommand(singleKeyStoreProvider);
            IReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider> instanceCache =
                GetInstanceCacheFrom(command);
            Assert.Single(instanceCache);
            Assert.True(instanceCache.ContainsKey(dummyProviderName1));

            command.RegisterColumnEncryptionKeyStoreProvidersOnCommand(multipleKeyStoreProviders);
            instanceCache = GetInstanceCacheFrom(command);
            Assert.Equal(2, instanceCache.Count);
            Assert.True(instanceCache.ContainsKey(dummyProviderName2));
            Assert.True(instanceCache.ContainsKey(dummyProviderName3));
        }

        private void AssertForAllCustomProviderCaches<T>(IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders, string expectedMessage)
            where T : Exception
        {
            Exception ex = Assert.Throws<T>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders));
            Assert.Contains(expectedMessage, ex.Message);

            ex = Assert.Throws<T>(() => connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(customProviders));
            Assert.Contains(expectedMessage, ex.Message);

            ex = Assert.Throws<T>(() => command.RegisterColumnEncryptionKeyStoreProvidersOnCommand(customProviders));
            Assert.Contains(expectedMessage, ex.Message);
        }

        private IReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider> GetInstanceCacheFrom(object obj)
        {
            FieldInfo instanceCacheField = obj.GetType().GetField(
                "_customColumnEncryptionKeyStoreProviders", BindingFlags.NonPublic | BindingFlags.Instance);
            return instanceCacheField.GetValue(obj) as IReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider>;
        }
    }
}
