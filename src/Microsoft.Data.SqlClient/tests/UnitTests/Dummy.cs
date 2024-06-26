using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.Handlers;
using Microsoft.Data.SqlClientX.Handlers.Connection;
using Microsoft.Data.SqlClientX.Handlers.TransportCreation;
using Xunit;

namespace Microsoft.Data.SqlClient.NetCore.UnitTests
{
    public class Dummy
    {
        [Fact]
        public void Test()
        {
            int offset = 0;
            byte[] payload= new byte[] { 1, 2, 3, 4 };
            int payLoadOffset = payload[offset++] << 8 | payload[offset++];
            int payloadLength = payload[offset++] << 8 | payload[offset++];

            Assert.Equal(payLoadOffset, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(0, 2)));

            Assert.Equal(payloadLength, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(2, 2)));
        }

        [Fact]
        public void TestE2E()
        {
            DataSourceParsingHandler dspHandler = new DataSourceParsingHandler();
            TransportCreationHandler tcHandler = new TransportCreationHandler();
            PreloginHandler plHandler = new PreloginHandler();
            ConnectionHandlerContext chc = new ConnectionHandlerContext();
            SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder();
            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
            csb.DataSource = "tcp:saurabhsingh.database.windows.net";
            csb.UserID = "saurabh";
            csb.Password = "HappyPass1234";
            csb.InitialCatalog = "drivers";
            csb.Encrypt = SqlConnectionEncryptOption.Strict;

            csb.TrustServerCertificate = true;

            SqlConnectionString scs = new SqlConnectionString(csb.ConnectionString);
            chc.ConnectionString = scs;
            var serverInfo = new ServerInfo(scs);
            serverInfo.SetDerivedNames(null, serverInfo.UserServerName);
            chc.SeverInfo = serverInfo;
            dspHandler.NextHandler = tcHandler;
            tcHandler.NextHandler = plHandler;
            dspHandler.Handle(chc, false, default).GetAwaiter().GetResult();
        }

        [Fact]
        public void ExistingClient()
        {
            SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder();
            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
            csb.DataSource = "tcp:saurabhsingh.database.windows.net";
            csb.UserID = "saurabh";
            csb.Password = "HappyPass1234";
            csb.InitialCatalog = "drivers";
            csb.Encrypt = SqlConnectionEncryptOption.Strict;

            csb.TrustServerCertificate = true;
            using (SqlConnection conn = new SqlConnection(csb.ConnectionString))
            {
                conn.Open();
            }
        }
    }
}
