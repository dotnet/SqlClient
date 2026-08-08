// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

namespace Microsoft.Data.SqlClient.PerformanceTests.BenchmarkRunners.DataTypeReaderRunner;

public class AlwaysEncrypted : DataTypeReaderRunnerBase
{
    private ColumnMasterKeyCertificateFixture _cmkCertificate;
    private ColumnMasterKey _masterKey;
    private ColumnEncryptionKey _encryptionKey;

    protected override RunnerJob Configuration => s_config.Benchmarks.AlwaysEncryptedDataTypeReaderRunnerConfig;

    public override IEnumerable<DataType> ExecutedTypes => AvailableTypes.Where(t => t.EncryptionSupported);

    protected override SqlConnection OpenConnection()
    {
        SqlConnectionStringBuilder builder = new(s_config.ConnectionString)
        {
            ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled
        };
        SqlConnection conn = new(builder.ToString());

        conn.Open();
        return conn;
    }

    protected override Table CreateTable()
    {
        _cmkCertificate = new ColumnMasterKeyCertificateFixture();
        _masterKey = new CertificateBackedColumnMasterKey(_connection, nameof(_masterKey), _cmkCertificate, false);
        _encryptionKey = new ColumnEncryptionKey(_connection, nameof(AlwaysEncrypted), _masterKey);

        return Table.Build(Type.Name)
            .AddColumn(new Column(Type, encryptionKey: _encryptionKey))
            .CreateTable(_connection);
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
