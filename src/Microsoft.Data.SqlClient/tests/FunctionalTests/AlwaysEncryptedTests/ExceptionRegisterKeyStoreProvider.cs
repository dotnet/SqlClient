// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    public class ExceptionRegisterKeyStoreProvider
    {

        [Fact]
        public void TestNullDictionary()
        {
            // Verify that we are unable to set null dictionary.
            ArgumentNullException e = Assert.Throws<ArgumentNullException>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(null));
            string expectedMessage = "Column encryption key store provider dictionary cannot be null. Expecting a non-null value.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [Fact]
        public void TestInvalidProviderName()
        {
            // Verify the namespace reservation
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
            customProviders.Add("MSSQL_DUMMY", new DummyKeyStoreProvider());
            ArgumentException e = Assert.Throws<ArgumentException>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders));
            string expectedMessage = "Invalid key store provider name 'MSSQL_DUMMY'. 'MSSQL_' prefix is reserved for system key store providers.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [Fact]
        public void TestNullProviderValue()
        {
            // Verify null provider value are not supported
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
            customProviders.Add("DUMMY", null);
            ArgumentNullException e = Assert.Throws<ArgumentNullException>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders));
            string expectedMessage = "Null reference specified for key store provider 'DUMMY'. Expecting a non-null value.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [Fact]
        public void TestNullProviderName()
        {
            // Verify Empty provider names are not supported.
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
            customProviders.Add("   ", new DummyKeyStoreProvider());
            ArgumentNullException e = Assert.Throws<ArgumentNullException>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders));
            string expectedMessage = "Invalid key store provider name specified. Key store provider names cannot be null or empty.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [Fact]
        public void TestCanCallOnlyOnce()
        {
            // Clear out the existing providers (to ensure test-rerunability)
            Utility.ClearSqlConnectionProviders();
            // Verify the provider can be set only once.
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
            customProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
            customProviders.Add(new KeyValuePair<string, SqlColumnEncryptionKeyStoreProvider>(@"DummyProvider", new DummyKeyStoreProvider()));
            SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders);
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders));
            string expectedMessage = "Key store providers cannot be set more than once.";
            Utility.ClearSqlConnectionProviders();
            Assert.Contains(expectedMessage, e.Message);
        }
    }
}
