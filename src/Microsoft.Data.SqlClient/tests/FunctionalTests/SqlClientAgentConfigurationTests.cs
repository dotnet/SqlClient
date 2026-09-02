// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlClientAgentConfigurationTests
    {
        // The FunctionalTests project employs a .NET Framework app.config file
        // that registers the EntityFramework agent.  Verify that this consumes
        // the single process-wide registration, so a later programmatic
        // registration is rejected.
        //
        // This cannot be verified on .NET because the test host substitutes its
        // own configuration file for the one built alongside this assembly.
        [ConditionalFact(typeof(TestUtility), nameof(TestUtility.IsNetFramework))]
        public void AppConfigAgent_PreventsProgrammaticRegistration()
        {
            Assert.Throws<InvalidOperationException>(
                () => SqlConnection.RegisterSqlClientAgent(SqlClientAgent.SemanticKernel));
        }
    }
}
