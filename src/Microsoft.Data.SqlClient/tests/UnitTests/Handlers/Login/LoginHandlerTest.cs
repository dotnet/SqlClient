// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.Data.SqlClientX.Handlers.Connection;
using Microsoft.Data.SqlClientX.Handlers.Connection.Login;
using Microsoft.Data.SqlClientX.IO;
using Xunit;

namespace Microsoft.Data.SqlClient.NetCore.UnitTests.Handlers.Login
{
    public class LoginHandlerTest
    {
        [Theory]
        [InlineData(true, "Encrypt=Strict; Connect Retry Count = 0; User Id=abc; pwd = Idontcare;")]
        [InlineData(false, "Encrypt=Strict; Connect Retry Count = 0; User Id=abc; pwd = Idontcare;")]

        public async void Handler_Test(bool isAsync, string connectionString)
        {
            LoginHandler loginHandler = new();

            SqlConnectionString connectionOptions = new(connectionString);
            MemoryStream readStream = new();
            MemoryStream writeStream = new MemoryStream();
            TdsWriteStream wStream = new TdsWriteStream(writeStream);
            TdsReadStream rStream = new TdsReadStream(readStream);
            TdsStream tdsStream = new TdsStream(wStream, rStream);
            var context = new ConnectionHandlerContext
            {
                ConnectionString = connectionOptions,
                TdsStream = tdsStream,
                ServerInfo = new ServerInfo(connectionOptions)
                
            };
            await loginHandler.Handle(context, isAsync, default);

            // TODO: Verify the memory stream backing the write stream for contents.
            writeStream.Position = 0;

            Assert.True(writeStream.Length > 0);
            byte[] buffer = writeStream.GetBuffer();
            Assert.Equal(FeatureExtensions.FeatureTerminator, buffer[buffer.Length -1]);
        }
    }
}
