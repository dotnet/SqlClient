// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    /// <summary>
    /// Verifies that an agent registered in the application configuration file consumes the single
    /// process-wide registration.
    /// </summary>
    public class SqlClientAgentConfigurationTests
    {
        /// <summary>
        /// Verifies that an agent registered in the application configuration file consumes the
        /// single process-wide registration, so a later programmatic registration is rejected.
        /// </summary>
        /// <remarks>
        /// This cannot be verified on .NET because the test host substitutes its own configuration
        /// file for the one built alongside this assembly.
        /// </remarks>
        [ConditionalFact(typeof(TestUtility), nameof(TestUtility.IsNetFramework))]
        public void AppConfigAgent_PreventsProgrammaticRegistration()
        {
            Assert.False(SqlConnection.RegisterSqlClientAgent(SqlClientAgent.SemanticKernel));
        }
    }
}
