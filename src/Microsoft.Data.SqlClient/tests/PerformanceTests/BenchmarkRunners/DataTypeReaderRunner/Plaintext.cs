// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Microsoft.Data.SqlClient.PerformanceTests.BenchmarkRunners.DataTypeReaderRunner;

/// <summary>Exercises plaintext individual types and mixed small-row shapes.</summary>
public class Plaintext : DataTypeReaderRunnerBase
{
    public override IEnumerable<DataType> ExecutedTypes => AvailableTypes;

    public override IEnumerable<ReaderCase> ExecutedCases =>
        base.ExecutedCases.Concat([new ReaderCase(1), new ReaderCase(4)]);

    public override IEnumerable<CommandBehavior> ExecutedCommandBehaviors =>
        [CommandBehavior.Default, CommandBehavior.SequentialAccess];

    protected override CommandRunnerJob Configuration => s_config.Benchmarks.DataTypeReaderRunnerConfig;

    protected override Table CreateTable() =>
        Table.Build(Type.Name)
            .AddColumn(new Column(Type));
}
