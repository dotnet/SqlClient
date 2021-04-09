// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Configurable retry logic manager;
    /// Receive the default providers by a loader and feeds the connections and commands.
    /// </summary>
    internal sealed partial class SqlConfigurableRetryLogicManager
    {
        private static readonly Lazy<SqlConfigurableRetryLogicLoader> s_loader =
            new Lazy<SqlConfigurableRetryLogicLoader>(() => new SqlConfigurableRetryLogicLoader());
    }
    /// <summary>
    /// Configurable retry logic loader
    /// </summary>
    internal sealed partial class SqlConfigurableRetryLogicLoader
    {
        public SqlConfigurableRetryLogicLoader()
        {
            AssignProviders();
        }
    }
}
