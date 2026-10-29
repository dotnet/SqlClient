// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class ParserNamespaceTests
    {
        [Theory]
        [InlineData("Parser.TdsParser")]
        [InlineData("Parser.TdsParserStateObject")]
        [InlineData("Parser.TdsParserStateObjectFactory")]
        [InlineData("Parser.TdsParserSessionPool")]
        [InlineData("Parser.TdsParserStaticMethods")]
        [InlineData("Parser.TdsParameterSetter")]
        [InlineData("Parser.TdsRecordBufferSetter")]
        [InlineData("Parser.TdsValueSetter")]
        [InlineData("Parser.TdsEnums")]
        [InlineData("Parser.TdsOperationStatus")]
        [InlineData("Parser.LastIOTimer")]
        [InlineData("Parser.Packet")]
        [InlineData("Parser.PacketHandle")]
        [InlineData("Parser.SessionHandle")]
        [InlineData("Parser.CallbackType")]
        [InlineData("Parser.RunBehavior")]
        [InlineData("Parser.TdsParserState")]
        [InlineData("Parser.SniContext")]
        [InlineData("Parser.ParsingErrorState")]
        [InlineData("Parser.DescribeParameterEncryptionResultSet1")]
        [InlineData("Parser.DescribeParameterEncryptionResultSet2")]
        [InlineData("Parser.DescribeParameterEncryptionResultSet3")]
        [InlineData("Parser.SslProtocolsHelper")]
        [InlineData("Parser._SqlRPC")]
        [InlineData("Parser.Login.EncryptionOptions")]
        [InlineData("Parser.Login.FederatedAuthenticationFeatureExtensionData")]
        [InlineData("Parser.Login.PreLoginHandshakeStatus")]
        [InlineData("Parser.Login.PreLoginOptions")]
        [InlineData("Parser.Login.SqlLogin")]
        [InlineData("Parser.Login.SqlFedAuthToken")]
        [InlineData("Parser.Tokens.SqlLoginAck")]
        [InlineData("Parser.Tokens.SqlFedAuthInfo")]
        [InlineData("Parser.Tokens.TdsReturnValueToken")]
        [InlineData("Parser.Tokens.SqlMetaDataPriv")]
        [InlineData("Parser.Tokens.SqlMetaDataUdt")]
        [InlineData("Parser.Tokens.SqlMetaDataXmlSchemaCollection")]
        [InlineData("Parser.Tokens.TdsColumnMetadata")]
        [InlineData("Parser.Tokens.TdsColumnMetadataToken")]
        [InlineData("Parser.Tokens.TdsColumnMetadataTokenCollection")]
        [InlineData("Parser.Tokens.MultiPartTableName")]
        [InlineData("Parser.Tokens.RoutingInfo")]
        public void ParserTypesCanBeResolvedByName(string name)
        {
            Assembly assembly = typeof(SqlConnection).Assembly;
            Type type = assembly.GetType("Microsoft.Data.SqlClient." + name, throwOnError: true)!;

            Assert.False(type.IsPublic);
            Assert.Null(assembly.GetType("Microsoft.Data.SqlClient." + type.Name));
        }

        [Fact]
        public void PlatformStateObjectTypesCanBeResolvedByName()
        {
            Assembly assembly = typeof(SqlConnection).Assembly;
#if NET
            Type managed = assembly.GetType("Microsoft.Data.SqlClient.Parser.TdsParserStateObjectManaged", throwOnError: true)!;
            Assert.NotNull(managed.GetField("_sessionHandle", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.Null(assembly.GetType("Microsoft.Data.SqlClient.ManagedSni.TdsParserStateObjectManaged"));
#endif
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.NotNull(assembly.GetType("Microsoft.Data.SqlClient.Parser.TdsParserStateObjectNative", throwOnError: true));
                foreach (string name in new[] { "SNIHandle", "SNILoadHandle", "SNIPacket" })
                {
                    Assert.NotNull(assembly.GetType("Microsoft.Data.SqlClient.NativeSni." + name, throwOnError: true));
                    Assert.Null(assembly.GetType("Microsoft.Data.SqlClient." + name));
                    Assert.Null(assembly.GetType("Microsoft.Data.SqlClient.Parser." + name));
                }
            }
        }
    }
}
