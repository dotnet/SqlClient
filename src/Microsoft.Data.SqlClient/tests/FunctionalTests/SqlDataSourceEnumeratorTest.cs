using System.Data;
using System.ServiceProcess;
using Microsoft.Data.Sql;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    internal class SqlDataSourceEnumeratorTest
    {
        [PlatformSpecific(TestPlatforms.Windows)]
        public void SqlDataSourceEnumerator_VerfifyDataTableSize()
        {
            ServiceController sc = new ServiceController("SQLBrowser");
            Assert.Equal(ServiceControllerStatus.Running, sc.Status);

            SqlDataSourceEnumerator instance = SqlDataSourceEnumerator.Instance;
            DataTable table = instance.GetDataSources();
            Assert.NotEmpty(table.Rows);
        }
    }
}
