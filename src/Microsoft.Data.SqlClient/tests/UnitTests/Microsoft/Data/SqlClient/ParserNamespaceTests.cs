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
        [InlineData("Parser.PacketHandle")]
        [InlineData("Parser.SessionHandle")]
        [InlineData("Parser.Login.EncryptionOptions")]
        [InlineData("Parser.Login.FederatedAuthenticationFeatureExtensionData")]
        [InlineData("Parser.Login.PreLoginHandshakeStatus")]
        [InlineData("Parser.Login.PreLoginOptions")]
        [InlineData("Parser.Login.SqlLogin")]
        [InlineData("Parser.Login.SqlFedAuthToken")]
        [InlineData("Parser.Tokens.SqlLoginAck")]
        [InlineData("Parser.Tokens.SqlFedAuthInfo")]
        [InlineData("Parser.Tokens.TdsReturnValueToken")]
        [InlineData("Parser.Tokens.TdsTypeInfo")]
        [InlineData("Parser.Tokens.TdsUdtTypeInfo")]
        [InlineData("Parser.Tokens.TdsXmlTypeInfo")]
        [InlineData("Parser.Tokens.TdsColumnMetadata")]
        [InlineData("Parser.Tokens.TdsColumnMetadataToken")]
        [InlineData("Parser.Tokens.TdsAltMetadataCollection")]
        [InlineData("Parser.Tokens.TdsTableName")]
        [InlineData("Parser.Tokens.TdsEnvChangeRoutingInfo")]
        public void ParserTypesCanBeResolvedByName(string name)
        {
            Assembly assembly = typeof(SqlConnection).Assembly;
            Type type = assembly.GetType("Microsoft.Data.SqlClient." + name, throwOnError: true);

            Assert.False(type.IsPublic);
            Assert.Null(assembly.GetType("Microsoft.Data.SqlClient." + type.Name));
        }

        [Fact]
        public void PlatformStateObjectTypesCanBeResolvedByName()
        {
            Assembly assembly = typeof(SqlConnection).Assembly;
#if NETCOREAPP
            Type managed = assembly.GetType("Microsoft.Data.SqlClient.Parser.TdsParserStateObjectManaged", throwOnError: true);
            Assert.NotNull(managed.GetField("_sessionHandle", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.Null(assembly.GetType("Microsoft.Data.SqlClient.ManagedSni.TdsParserStateObjectManaged"));
#endif
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.NotNull(assembly.GetType("Microsoft.Data.SqlClient.Parser.TdsParserStateObjectNative", throwOnError: true));
                foreach (string name in new[] { "SNIHandle", "SNILoadHandle", "SNIPacket", "WritePacketCache" })
                {
                    Assert.NotNull(assembly.GetType("Microsoft.Data.SqlClient.NativeSni." + name, throwOnError: true));
                    Assert.Null(assembly.GetType("Microsoft.Data.SqlClient." + name));
                }
            }
        }
    }
}
