using System;
using System.Data;
using System.ServiceProcess;
using Microsoft.Data.Sql;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlDataSourceEnumeratorTest
    {
        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void SqlDataSourceEnumerator_VerfifyDataTableSize()
        {
            ServiceController sc = new("SQLBrowser");
            Assert.Equal(ServiceControllerStatus.Running, sc.Status);

            SqlDataSourceEnumerator instance = SqlDataSourceEnumerator.Instance;
            DataTable table = instance.GetDataSources();
            Assert.NotEmpty(table.Rows);

            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
            System.Data.DataTable table2 = instance.GetDataSources();
            Assert.NotEmpty(table2.Rows);
        }
    }
}
