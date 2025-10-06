using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    [CollectionDefinition("AlwaysEncrypted")]
    public class AlwaysEncryptedCollectionDefinition :ICollectionFixture<SQLSetupStrategyCertStoreProvider>
    {
    }
}
