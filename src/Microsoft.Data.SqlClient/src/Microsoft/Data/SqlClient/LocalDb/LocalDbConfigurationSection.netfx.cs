// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

using System.Configuration;

namespace Microsoft.Data.SqlClient.LocalDb
{
    internal sealed class LocalDbConfigurationSection : ConfigurationSection
    {
        [ConfigurationProperty("localdbinstances", IsRequired = true)]
        public LocalDbInstancesCollection LocalDbInstances =>
            (LocalDbInstancesCollection)this["localdbinstances"] ?? new LocalDbInstancesCollection();
    }
}

#endif
