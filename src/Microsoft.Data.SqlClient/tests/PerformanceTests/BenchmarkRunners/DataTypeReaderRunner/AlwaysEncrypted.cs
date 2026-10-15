// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

namespace Microsoft.Data.SqlClient.PerformanceTests.BenchmarkRunners.DataTypeReaderRunner;

/// <summary>Compares supported encrypted individual types using Default command behavior.</summary>
public class AlwaysEncrypted : DataTypeReaderRunnerBase
{
    private ColumnMasterKeyCertificateFixture _cmkCertificate;
    private ColumnMasterKey _masterKey;
    private ColumnEncryptionKey _encryptionKey;

    public override IEnumerable<DataType> ExecutedTypes => AvailableTypes.Where(t => t.EncryptionSupported);

    protected override CommandRunnerJob Configuration => s_config.Benchmarks.AlwaysEncryptedDataTypeReaderRunnerConfig;

    protected override SqlConnectionStringBuilder CreateConnectionStringBuilder()
    {
        SqlConnectionStringBuilder builder = new(s_config.ConnectionString)
        {
            ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled
        };
        return builder;
    }

    protected override Table CreateTable()
    {
        _cmkCertificate = new ColumnMasterKeyCertificateFixture();
        _masterKey = new CertificateBackedColumnMasterKey(Connection, nameof(_masterKey), _cmkCertificate, false);
        _encryptionKey = new ColumnEncryptionKey(Connection, nameof(AlwaysEncrypted), _masterKey);

        return Table.Build(Type.Name)
            .AddColumn(new Column(Type, encryptionKey: _encryptionKey));
    }

    protected override void OnCleanup()
    {
        using (_cmkCertificate)
        using (_masterKey)
        using (_encryptionKey)
        {
        }
    }
}
