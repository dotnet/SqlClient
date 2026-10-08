// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

namespace Microsoft.Data.SqlClient.PerformanceTests.BenchmarkRunners.LargeDataReadRunner;

public class AlwaysEncrypted : LargeDataReadRunnerBase
{
    private ColumnMasterKeyCertificateFixture _cmkCertificate;
    private ColumnMasterKey _masterKey;
    private ColumnEncryptionKey _encryptionKey;

    public override IEnumerable<CommandBehavior> ExecutedCommandBehaviors => [CommandBehavior.Default];

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

    protected override Tests.Common.Fixtures.DatabaseObjects.Table CreateTable()
    {
        _cmkCertificate = new ColumnMasterKeyCertificateFixture();
        _masterKey = new CertificateBackedColumnMasterKey(_connection, nameof(_masterKey), _cmkCertificate, false);
        _encryptionKey = new ColumnEncryptionKey(_connection, nameof(AlwaysEncrypted), _masterKey);

        return new Tests.Common.Fixtures.DatabaseObjects.Table(_connection, nameof(AlwaysEncrypted),
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
