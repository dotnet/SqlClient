// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Newtonsoft.Json;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public abstract class BaseRunner
    {
        public BaseRunner()
        {
            s_config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("runnerconfig.json"));
            s_datatypes = JsonConvert.DeserializeObject<DataTypes>(File.ReadAllText("datatypes.json"));
        }

        internal static Config s_config;
        internal static DataTypes s_datatypes;
    }
}
