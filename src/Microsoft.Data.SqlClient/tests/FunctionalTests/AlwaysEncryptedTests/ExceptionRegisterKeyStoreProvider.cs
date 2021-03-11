// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    public class ExceptionRegisterKeyStoreProvider
    {
        private SqlConnection connection = new SqlConnection();

        IDictionary<string, SqlColumnEncryptionKeyStoreProvider> singleKeyStoreProvider =
            new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>()
            {
                { "DummyProvider1", new DummyKeyStoreProvider() }
            };

        IDictionary<string, SqlColumnEncryptionKeyStoreProvider> multipleKeyStoreProviders =
            new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>()
            {
                { "DummyProvider2", new DummyKeyStoreProvider() },
                { "DummyProvider3", new DummyKeyStoreProvider() }
            };

        [Fact]
        public void TestNullDictionary()
        {
            // Verify that we are unable to set null dictionary.
            string expectedMessage = "Column encryption key store provider dictionary cannot be null. Expecting a non-null value.";
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
            string expectedMessage = "Invalid key store provider name 'MSSQL_DUMMY'. 'MSSQL_' prefix is reserved for system key store providers.";
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
            customProviders.Add("MSSQL_DUMMY", new DummyKeyStoreProvider());

            ArgumentException e = Assert.Throws<ArgumentException>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders));
            Assert.Contains(expectedMessage, e.Message);

            e = Assert.Throws<ArgumentException>(() => connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(customProviders));
            Assert.Contains(expectedMessage, e.Message);
        }

        [Fact]
        public void TestNullProviderValue()
        {
            // Verify null provider value are not supported
            string expectedMessage = "Null reference specified for key store provider 'DUMMY'. Expecting a non-null value.";
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
            customProviders.Add("DUMMY", null);

            ArgumentNullException e = Assert.Throws<ArgumentNullException>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders));
            Assert.Contains(expectedMessage, e.Message);

            e = Assert.Throws<ArgumentNullException>(() => connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(customProviders));
            Assert.Contains(expectedMessage, e.Message);
        }

        [Fact]
        public void TestEmptyProviderName()
        {
            // Verify Empty provider names are not supported.
            string expectedMessage = "Invalid key store provider name specified. Key store provider names cannot be null or empty.";
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
            // Clear out the existing providers (to ensure test-rerunability)
            Utility.ClearSqlConnectionGlobalProviders();
            // Verify the provider can be set only once.
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
            customProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
            customProviders.Add(new KeyValuePair<string, SqlColumnEncryptionKeyStoreProvider>(@"DummyProvider", new DummyKeyStoreProvider()));
            SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders);

            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders));
            string expectedMessage = "Key store providers cannot be set more than once.";
            Assert.Contains(expectedMessage, e.Message);

            Utility.ClearSqlConnectionGlobalProviders();
        }

        [Fact]
        public void TestCanSetInstanceProvidersMoreThanOnce()
        {
            using (SqlConnection connection = new SqlConnection())
            {
                connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(singleKeyStoreProvider);
                FieldInfo field = connection.GetType().GetField("_CustomColumnEncryptionKeyStoreProviders",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                ReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider> instanceCache =
                    field.GetValue(connection) as ReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider>;

                Assert.True(instanceCache.Count == 1);
                Assert.True(instanceCache.ContainsKey("DummyProvider1"));

                connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(multipleKeyStoreProviders);
                instanceCache = field.GetValue(connection) as ReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider>;
                Assert.True(instanceCache.Count == 2);
                Assert.True(instanceCache.ContainsKey("DummyProvider2"));
                Assert.True(instanceCache.ContainsKey("DummyProvider3"));
            }
        }
    }
}
