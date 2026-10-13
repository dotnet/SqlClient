// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

namespace Microsoft.Data.SqlClient.PerformanceTests.BenchmarkRunners.LargeDataReadRunner;

/// <summary>Compares encrypted binary materialization without unsupported sequential/streaming modes.</summary>
public class AlwaysEncrypted : LargeDataReadRunnerBase
{
    private ColumnMasterKeyCertificateFixture _cmkCertificate;
    private ColumnMasterKey _masterKey;
    private ColumnEncryptionKey _encryptionKey;

    public override IEnumerable<CommandBehavior> ExecutedCommandBehaviors => [CommandBehavior.Default];

    protected override CommandRunnerJob Configuration => s_config.Benchmarks.AlwaysEncryptedLargeDataReadRunnerConfig;

    protected override SqlConnectionStringBuilder CreateConnectionStringBuilder()
    {
        SqlConnectionStringBuilder builder = new(s_config.ConnectionString)
        {
            ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled
        };
        return builder;
    }

    protected override Tests.Common.Fixtures.DatabaseObjects.Table CreateTable()
    {
        _cmkCertificate = new ColumnMasterKeyCertificateFixture();
        _masterKey = new CertificateBackedColumnMasterKey(Connection, nameof(_masterKey), _cmkCertificate, false);
        _encryptionKey = new ColumnEncryptionKey(Connection, nameof(AlwaysEncrypted), _masterKey);

        return new Tests.Common.Fixtures.DatabaseObjects.Table(Connection, nameof(AlwaysEncrypted),
            "(" +
            "Id INT IDENTITY PRIMARY KEY," +
            "Data VARBINARY(MAX) ENCRYPTED WITH" +
            "(" +
            $"COLUMN_ENCRYPTION_KEY = {_encryptionKey.Name}," +
            "ENCRYPTION_TYPE = DETERMINISTIC," +
            "ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'" +
            ")" +
            ")");
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
