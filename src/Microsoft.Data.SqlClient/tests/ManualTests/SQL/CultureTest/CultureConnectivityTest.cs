using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static Microsoft.Data.SqlClient.ManualTesting.Tests.DataTestUtility;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class CultureConnectivityTest
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(IsUsingManagedSNI))]
        public void ConnectionTestWithCultureTH()
        {
            //AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true); // Used this only when testing locally
            // Save current cultures
            CultureInfo savedCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo savedUICulture = Thread.CurrentThread.CurrentUICulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("th-TH");
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("th-TH");

                using TestTdsServer server = TestTdsServer.StartTestServer();
                using SqlConnection connection = new SqlConnection(server.ConnectionString);
                connection.Open();
                Assert.Equal(ConnectionState.Open, connection.State);
            }
            finally
            {
                // Restore saved cultures
                if (Thread.CurrentThread.CurrentCulture != savedCulture)
                    Thread.CurrentThread.CurrentCulture = savedCulture;
                if (Thread.CurrentThread.CurrentUICulture != savedUICulture)
                    Thread.CurrentThread.CurrentUICulture = savedUICulture;
            }
        }
    }
}
