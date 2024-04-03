// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS.PreLogin;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.DataCommon
{
    public class ConnectionTestParameters
    {
        public TDSPreLoginTokenEncryptionType TdsEncryptionType { get; set; }
        public SqlConnectionEncryptOption Encrypt { get; set; }
        public bool TrustServerCertificate { get; set; }
        public string Certificate { get; set; }
        public string HostNameInCertificate { get; set; }
        public bool TestResult { get; set; }
    }
}
