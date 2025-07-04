// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlErrorTest
    {
        private const string SQLMSF_FailoverPartnerNotSupported = 
            "Connecting to a mirrored SQL Server instance using the MultiSubnetFailover connection option is not supported.";
        private const byte FATAL_ERROR_CLASS = 20;

#if NETFRAMEWORK
        [Fact]
        public static void SqlErrorSerializationTest()
        {
            DataContractSerializer serializer = new DataContractSerializer(typeof(SqlError));
            SqlError expected = CreateError();
            SqlError actual = null;
            using (var stream = new MemoryStream())
            {
                try
                {
                    serializer.WriteObject(stream, expected);
                    stream.Position = 0;
                    actual = (SqlError)serializer.ReadObject(stream);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Unexpected Exception occurred: {ex.Message}");
                }
            }

            Assert.Equal(expected.Message, actual.Message);
            Assert.Equal(expected.Number, actual.Number);
            Assert.Equal(expected.State, actual.State);
            Assert.Equal(expected.Class, actual.Class);
            Assert.Equal(expected.Server, actual.Server);
            Assert.Equal(expected.Procedure, actual.Procedure);
            Assert.Equal(expected.LineNumber, actual.LineNumber);
            Assert.Equal(expected.Source, actual.Source);
        }
#endif


        private static SqlError CreateError()
        {
            string msg = SQLMSF_FailoverPartnerNotSupported;

            Type sqlErrorType = typeof(SqlError);

            // SqlError only has internal constructors, in order to instantiate this, we use reflection
            SqlError sqlError =  (SqlError)sqlErrorType.Assembly.CreateInstance(
                sqlErrorType.FullName,
                false,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { 100, (byte)0x00, FATAL_ERROR_CLASS, "ServerName", msg, "ProcedureName", 10, null, -1 },
                null,
                null);

            return sqlError;
        }
    }
}
