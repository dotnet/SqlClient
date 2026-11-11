// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient.TestUtilities;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Verifies Managed Instance setup retains the authentication supplied by its configuration.
    /// These checks do not require a database connection.
    /// </summary>
    [Trait("Set", "3")]
    public class DataTestUtilityTests
    {
        /// <summary>
        /// Guards against replacing configured credentials with an unavailable test identity.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsManagedInstance))]
        public void StaticInitialization_ManagedInstance_PreservesConfiguredConnectionString()
        {
            // Arrange
            string configuredConnectionString = Config.Load().TCPConnectionString;

            // Act
            string connectionString = DataTestUtility.TCPConnectionString;

            // Assert
            Assert.True(
                string.Equals(configuredConnectionString, connectionString, StringComparison.Ordinal),
                "Managed Instance setup must preserve the configured connection string.");
        }

        /// <summary>
        /// Ensures a configured Managed Instance client ID is handled by the provider that reads
        /// it from authentication parameters, independently of optional identity-test settings.
        /// </summary>
        [ConditionalFact(nameof(IsManagedInstanceUserAssignedIdentityConfigured))]
        public void StaticInitialization_ManagedInstanceUserAssignedIdentity_UsesConfiguredProvider()
        {
            // Act
            SqlAuthenticationProvider provider = SqlAuthenticationProvider.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryManagedIdentity);

            // Assert
            Assert.IsType<UserAssignedManagedIdentityProvider>(provider);
        }

        /// <summary>
        /// Selects configurations whose primary connection uses a user-assigned Managed Identity.
        /// </summary>
        /// <returns>Whether Managed Instance requires the configured identity provider.</returns>
        public static bool IsManagedInstanceUserAssignedIdentityConfigured()
        {
            Config config = Config.Load();
            return DataTestUtility.IsManagedInstance
                && new SqlConnectionStringBuilder(config.TCPConnectionString).Authentication
                    == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;
        }
    }
}
