// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Data.SqlClient.PerformanceTests.BenchmarkRunners.DataTypeReaderRunner;

public class Plaintext : DataTypeReaderRunnerBase
{
    public override IEnumerable<DataType> ExecutedTypes => AvailableTypes;

    protected override RunnerJob Configuration => s_config.Benchmarks.DataTypeReaderRunnerConfig;

    protected override SqlConnection OpenConnection()
    {
        SqlConnection conn = new(s_config.ConnectionString);

        conn.Open();
        return conn;
    }

    protected override Table CreateTable() =>
        Table.Build(Type.Name)
            .AddColumn(new Column(Type))
            .CreateTable(_connection);
}
