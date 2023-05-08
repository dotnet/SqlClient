// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class DataReaderStreamsTest
    {
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(GetCommandBehavioursAndIsAsync))]
        public static async Task GetFieldValueAsync_OfStream(CommandBehavior behavior, bool isExecuteAsync)
        {
            const int PacketSize = 512; // force minimun packet size so that the test data spans multiple packets to test sequential access spanning
            string connectionString = SetConnectionStringPacketSize(DataTestUtility.TCPConnectionString, PacketSize);
            byte[] originalData = CreateBinaryData(PacketSize, forcedPacketCount: 4);
            string query = CreateBinaryDataQuery(originalData);

            string streamTypeName = null;
            byte[] outputData = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                connection.Open();
                using (SqlDataReader reader = await ExecuteReader(command, behavior, isExecuteAsync))
                {
                    if (await Read(reader, isExecuteAsync))
                    {
                        using (MemoryStream buffer = new MemoryStream(originalData.Length))
                        using (Stream stream = await reader.GetFieldValueAsync<Stream>(1))
                        {
                            streamTypeName = stream.GetType().Name;
                            await stream.CopyToAsync(buffer);
                            outputData = buffer.ToArray();
                        }
                    }
                }
            }

            Assert.True(behavior != CommandBehavior.SequentialAccess || streamTypeName.Contains("Sequential"));
            Assert.NotNull(outputData);
            Assert.Equal(originalData.Length, outputData.Length);
            Assert.Equal(originalData, outputData);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetCommandBehavioursAndIsAsync))]
        public static async Task GetFieldValueAsync_OfXmlReader(CommandBehavior behavior, bool isExecuteAsync)
        {
            const int PacketSize = 512; // force minimun packet size so that the test data spans multiple packets to test sequential access spanning
            string connectionString = SetConnectionStringPacketSize(DataTestUtility.TCPConnectionString, PacketSize);
            string originalXml = CreateXmlData(PacketSize, forcedPacketCount: 4);
            string query = CreateXmlDataQuery(originalXml);

            bool isAsync = false;
            string outputXml = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                connection.Open();
                using (SqlDataReader reader = await ExecuteReader(command, behavior, isExecuteAsync))
                {
                    if (await Read(reader, isExecuteAsync))
                    {
                        using (XmlReader xmlReader = await reader.GetFieldValueAsync<XmlReader>(1))
                        {
                            isAsync = xmlReader.Settings.Async;
                            outputXml = GetXmlDocumentContents(xmlReader);
                        }
                    }
                }
            }

            Assert.True(behavior != CommandBehavior.SequentialAccess || isAsync);
            Assert.NotNull(outputXml);
            Assert.Equal(originalXml.Length, outputXml.Length);
            Assert.Equal(originalXml, outputXml);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(GetCommandBehavioursAndIsAsync))]
        public static async Task GetFieldValueAsync_OfTextReader(CommandBehavior behavior, bool isExecuteAsync)
        {
            const int PacketSize = 512; // force minimun packet size so that the test data spans multiple packets to test sequential access spanning
            string connectionString = SetConnectionStringPacketSize(DataTestUtility.TCPConnectionString, PacketSize);
            string originalText = CreateXmlData(PacketSize, forcedPacketCount: 4);
            string query = CreateTextDataQuery(originalText);

            string streamTypeName = null;
            string outputText = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                connection.Open();
                using (SqlDataReader reader = await ExecuteReader(command, behavior, isExecuteAsync))
                {
                    if (await Read(reader, isExecuteAsync))
                    {
                        using (TextReader textReader = await reader.GetFieldValueAsync<TextReader>(1))
                        {
                            streamTypeName = textReader.GetType().Name;
                            outputText = await textReader.ReadToEndAsync();
                        }
                    }
                }
            }

            Assert.True(behavior != CommandBehavior.SequentialAccess || streamTypeName.Contains("Sequential"));
            Assert.NotNull(outputText);
            Assert.Equal(originalText.Length, outputText.Length);
            Assert.Equal(originalText, outputText);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(GetCommandBehavioursAndIsAsync))]
        public static async Task GetFieldValueAsync_Char_OfTextReader(CommandBehavior behavior, bool isExecuteAsync)
        {
            const int PacketSize = 512; // force minimun packet size so that the test data spans multiple packets to test sequential access spanning
            string connectionString = SetConnectionStringPacketSize(DataTestUtility.TCPConnectionString, PacketSize);
            string originalText = new ('c', PacketSize * 4);
            string query = CreateCharDataQuery(originalText);

            string streamTypeName = null;
            string outputText = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                connection.Open();
                using (SqlDataReader reader = await ExecuteReader(command, behavior, isExecuteAsync))
                {
                    if (await Read(reader, isExecuteAsync))
                    {
                        using (TextReader textReader = await reader.GetFieldValueAsync<TextReader>(1))
                        {
                            streamTypeName = textReader.GetType().Name;
                            outputText = await textReader.ReadToEndAsync();
                        }
                    }
                }
            }

            Assert.True(behavior != CommandBehavior.SequentialAccess || streamTypeName.Contains("Sequential"));
            Assert.NotNull(outputText);
            Assert.Equal(originalText.Length, outputText.Length);
            Assert.Equal(originalText, outputText);
        }

        // Synapse: Cannot find data type 'XML'.
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetCommandBehavioursAndIsAsync))]
        public static async void GetFieldValue_OfXmlReader(CommandBehavior behavior, bool isExecuteAsync)
        {
            const int PacketSize = 512; // force minimun packet size so that the test data spans multiple packets to test sequential access spanning
            string connectionString = SetConnectionStringPacketSize(DataTestUtility.TCPConnectionString, PacketSize);
            string originalXml = CreateXmlData(PacketSize, forcedPacketCount: 4);
            string query = CreateXmlDataQuery(originalXml);

            bool isAsync = false;
            string outputXml = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                connection.Open();
                using (SqlDataReader reader = await ExecuteReader(command, behavior, isExecuteAsync))
                {
                    if (await Read(reader, isExecuteAsync))
                    {
                        using (XmlReader xmlReader = reader.GetFieldValue<XmlReader>(1))
                        {
                            isAsync = xmlReader.Settings.Async;
                            outputXml = GetXmlDocumentContents(xmlReader);
                        }
                    }
                }
            }

            Assert.False(isAsync);
            Assert.NotNull(outputXml);
            Assert.Equal(originalXml.Length, outputXml.Length);
            Assert.Equal(originalXml, outputXml);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(GetCommandBehavioursAndIsAsync))]
        public static async void GetFieldValue_OfStream(CommandBehavior behavior, bool isExecuteAsync)
        {
            const int PacketSize = 512; // force minimun packet size so that the test data spans multiple packets to test sequential access spanning
            string connectionString = SetConnectionStringPacketSize(DataTestUtility.TCPConnectionString, PacketSize);
            byte[] originalData = CreateBinaryData(PacketSize, forcedPacketCount: 4);
            string query = CreateBinaryDataQuery(originalData);

            string streamTypeName = null;
            byte[] outputData = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                connection.Open();
                using (SqlDataReader reader = await ExecuteReader(command, behavior, isExecuteAsync))
                {
                    if (await Read(reader, isExecuteAsync))
                    {
                        using (Stream stream = reader.GetFieldValue<Stream>(1))
                        {
                            streamTypeName = stream.GetType().Name;
                            outputData = GetStreamContents(stream);
                        }
                    }
                }
            }
            Assert.True(behavior != CommandBehavior.SequentialAccess || streamTypeName.Contains("Sequential"));
            Assert.NotNull(outputData);
            Assert.Equal(originalData.Length, outputData.Length);
            Assert.Equal(originalData, outputData);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetCommandBehavioursAndIsAsync))]
        public static async void GetFieldValue_OfTextReader(CommandBehavior behavior, bool isExecuteAsync)
        {
            const int PacketSize = 512; // force minimun packet size so that the test data spans multiple packets to test sequential access spanning
            string connectionString = SetConnectionStringPacketSize(DataTestUtility.TCPConnectionString, PacketSize);
            string originalText = CreateXmlData(PacketSize, forcedPacketCount: 4);
            string query = CreateTextDataQuery(originalText);

            string streamTypeName = null;
            string outputText = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                connection.Open();
                using (SqlDataReader reader = await ExecuteReader(command, behavior, isExecuteAsync))
                {
                    if (await Read(reader, isExecuteAsync))
                    {
                        using (TextReader textReader = reader.GetFieldValue<TextReader>(1))
                        {
                            streamTypeName = textReader.GetType().Name;
                            outputText = textReader.ReadToEnd();
                        }
                    }
                }
            }

            Assert.True(behavior != CommandBehavior.SequentialAccess || streamTypeName.Contains("Sequential"));
            Assert.NotNull(outputText);
            Assert.Equal(originalText.Length, outputText.Length);
            Assert.Equal(originalText, outputText);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(GetCommandBehavioursAndIsAsync))]
        public static async void GetStream(CommandBehavior behavior, bool isExecuteAsync)
        {
            const int PacketSize = 512; // force minimun packet size so that the test data spans multiple packets to test sequential access spanning
            string connectionString = SetConnectionStringPacketSize(DataTestUtility.TCPConnectionString, PacketSize);
            byte[] originalData = CreateBinaryData(PacketSize, forcedPacketCount: 4);
            string query = CreateBinaryDataQuery(originalData);

            string streamTypeName = null;
            byte[] outputData = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                connection.Open();
                using (SqlDataReader reader = await ExecuteReader(command, behavior, isExecuteAsync))
                {
                    if (await Read(reader, isExecuteAsync))
                    {
                        using (MemoryStream buffer = new MemoryStream(originalData.Length))
                        using (Stream stream = reader.GetStream(1))
                        {
                            streamTypeName = stream.GetType().Name;
                            stream.CopyTo(buffer);
                            outputData = buffer.ToArray();
                        }
                    }
                }
            }

            Assert.True(behavior != CommandBehavior.SequentialAccess || streamTypeName.Contains("Sequential"));
            Assert.NotNull(outputData);
            Assert.Equal(originalData.Length, outputData.Length);
            Assert.Equal(originalData, outputData);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetCommandBehavioursAndIsAsync))]
        public static async void GetXmlReader(CommandBehavior behavior, bool isExecuteAsync)
        {
            const int PacketSize = 512; // force minimun packet size so that the test data spans multiple packets to test sequential access spanning
            string connectionString = SetConnectionStringPacketSize(DataTestUtility.TCPConnectionString, PacketSize);
            string originalXml = CreateXmlData(PacketSize, forcedPacketCount: 4);
            string query = CreateXmlDataQuery(originalXml);

            bool isAsync = false;
            string outputXml = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                connection.Open();
                using (SqlDataReader reader = await ExecuteReader(command, behavior, isExecuteAsync))
                {
                    if (await Read(reader, isExecuteAsync))
                    {
                        using (XmlReader xmlReader = reader.GetXmlReader(1))
                        {
                            isAsync = xmlReader.Settings.Async;
                            outputXml = GetXmlDocumentContents(xmlReader);
                        }
                    }
                }
            }

            Assert.False(isAsync);
            Assert.NotNull(outputXml);
            Assert.Equal(originalXml.Length, outputXml.Length);
            Assert.Equal(originalXml, outputXml);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(GetCommandBehavioursAndIsAsync))]
        public static async void GetTextReader(CommandBehavior behavior, bool isExecuteAsync)
        {
            const int PacketSize = 512; // force minimun packet size so that the test data spans multiple packets to test sequential access spanning
            string connectionString = SetConnectionStringPacketSize(DataTestUtility.TCPConnectionString, PacketSize);
            string originalText = CreateXmlData(PacketSize, forcedPacketCount: 4);
            string query = CreateTextDataQuery(originalText);

            string streamTypeName = null;
            string outputText = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                connection.Open();
                using (SqlDataReader reader = await ExecuteReader(command, behavior, isExecuteAsync))
                {
                    if (await Read(reader, isExecuteAsync))
                    {
                        using (TextReader textReader = reader.GetTextReader(1))
                        {
                            streamTypeName = textReader.GetType().Name;
                            outputText = textReader.ReadToEnd();
                        }
                    }
                }
            }

            Assert.True(behavior != CommandBehavior.SequentialAccess || streamTypeName.Contains("Sequential"));
            Assert.NotNull(outputText);
            Assert.Equal(originalText.Length, outputText.Length);
            Assert.Equal(originalText, outputText);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetCommandBehaviourAndAccessorTypes))]
        public static void NullStreamProperties(CommandBehavior behavior, AccessorType accessorType)
        {
            string query = "SELECT convert(xml,NULL) AS XmlData, convert(nvarchar(max),NULL) as TextData, convert(varbinary(max),NULL) as StreamData";

            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                connection.Open();

                // do clean queries to get field values again in case of sequential mode

                using (SqlDataReader reader = command.ExecuteReader(behavior))
                {
                    if (reader.Read())
                    {
                        Assert.True(reader.IsDBNull(0));
                        Assert.True(reader.IsDBNull(1));
                        Assert.True(reader.IsDBNull(2));
                    }
                }

                using (SqlDataReader reader = command.ExecuteReader(behavior))
                {
                    if (reader.Read())
                    {
                        Assert.True(reader.IsDBNullAsync(0).GetAwaiter().GetResult());
                        Assert.True(reader.IsDBNullAsync(1).GetAwaiter().GetResult());
                        Assert.True(reader.IsDBNullAsync(2).GetAwaiter().GetResult());
                    }
                }

                using (SqlDataReader reader = command.ExecuteReader(behavior))
                {
                    if (reader.Read())
                    {
                        using (XmlReader xmlReader = GetValue<XmlReader>(reader, 0, accessorType))
                        {
                            Assert.NotNull(xmlReader);
                            Assert.Equal(accessorType == AccessorType.GetFieldValueAsync, xmlReader.Settings.Async);
                            Assert.Equal(xmlReader.Value, string.Empty);
                            Assert.False(xmlReader.Read());
                            Assert.True(xmlReader.EOF);
                        }

                        using (TextReader textReader = GetValue<TextReader>(reader, 1, accessorType))
                        {
                            Assert.NotNull(textReader);
                            Assert.True(behavior != CommandBehavior.SequentialAccess || textReader.GetType().Name.Contains("Sequential"));
                            Assert.Equal(textReader.ReadToEnd(), string.Empty);
                        }

                        using (Stream stream = GetValue<Stream>(reader, 2, accessorType))
                        {
                            Assert.NotNull(stream);
                            Assert.True(behavior != CommandBehavior.SequentialAccess || stream.GetType().Name.Contains("Sequential"));
                        }
                    }
                }

                using (SqlDataReader reader = command.ExecuteReader(behavior))
                {
                    if (reader.Read())
                    {
                        // get a clean reader over the same field and check that the value is empty
                        using (XmlReader xmlReader = GetValue<XmlReader>(reader, 0, accessorType))
                        {
                            Assert.Equal(GetXmlDocumentContents(xmlReader), string.Empty);
                        }

                        using (TextReader textReader = GetValue<TextReader>(reader, 1, accessorType))
                        {
                            Assert.Equal(textReader.ReadToEnd(), string.Empty);
                        }

                        using (Stream stream = GetValue<Stream>(reader, 2, accessorType))
                        {
                            Assert.Equal(GetStreamContents(stream), Array.Empty<byte>());
                        }
                    }
                }
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(GetCommandBehaviourAndAccessorTypes))]
        public static void InvalidCastExceptionStream(CommandBehavior behavior, AccessorType accessorType)
        {
            string query = "SELECT convert(xml,NULL) AS XmlData, convert(nvarchar(max),NULL) as TextData";

            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                connection.Open();

                using (SqlDataReader reader = command.ExecuteReader(behavior))
                {
                    Assert.True(reader.Read(), "It's excpected to read a row.");

                    InvalidCastException ex = Assert.Throws<InvalidCastException>(() => GetValue<TextReader>(reader, 0, accessorType));
                    Assert.Contains("The GetTextReader function can only be used on columns of type Char, NChar, NText, NVarChar, Text or VarChar.", ex.Message);

                    ex = Assert.Throws<InvalidCastException>(() => GetValue<Stream>(reader, 0, accessorType));
                    Assert.Contains("The GetStream function can only be used on columns of type Binary, Image, Udt or VarBinary.", ex.Message);

                    ex = Assert.Throws<InvalidCastException>(() => GetValue<XmlReader>(reader, 1, accessorType));
                    Assert.Contains("The GetXmlReader function can only be used on columns of type Xml.", ex.Message);
                }
            }
        }

#if NETCOREAPP
        [ConditionalFact(typeof(DataTestUtility),nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async void ReadAsyncContentsCompletes()
        {
            string expectedXml = "<test>This is a test string</test>";
            string query = $"SELECT CAST('{expectedXml}' AS NVARCHAR(MAX))";

            string returnedXml = null;
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                connection.Open();

                await using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        using (TextReader textReader = reader.GetTextReader(0))
                        using (XmlReader xmlReader = XmlReader.Create(textReader, new XmlReaderSettings() { Async = true }))
                        {
                            XDocument xdoc = await XDocument.LoadAsync(xmlReader, LoadOptions.None, default).ConfigureAwait(false);
                            returnedXml = xdoc.ToString();
                        }
                    }
                }
            }

            Assert.Equal(expectedXml, returnedXml, StringComparer.Ordinal);
        }
#endif

        private static async Task<SqlDataReader> ExecuteReader(SqlCommand command, CommandBehavior behavior, bool isExecuteAsync)
            => isExecuteAsync ? await command.ExecuteReaderAsync(behavior) : command.ExecuteReader(behavior);

        private static async Task<bool> Read(SqlDataReader reader, bool isExecuteAsync)
            => isExecuteAsync ? await reader.ReadAsync() : reader.Read();

        public static IEnumerable<object[]> GetCommandBehaviourAndAccessorTypes()
        {
            foreach (CommandBehavior behavior in new CommandBehavior[] { CommandBehavior.Default, CommandBehavior.SequentialAccess })
            {
                foreach (AccessorType accessorType in new AccessorType[] { AccessorType.GetNamedValue, AccessorType.GetFieldValue, AccessorType.GetFieldValueAsync })
                {
                    yield return new object[] { behavior, accessorType };
                }
            }
        }

        public static IEnumerable<object[]> GetCommandBehavioursAndIsAsync()
        {
            foreach (CommandBehavior behavior in new CommandBehavior[] { CommandBehavior.Default, CommandBehavior.SequentialAccess })
            {
                yield return new object[] { behavior, true };
                yield return new object[] { behavior, false };
            }
        }

        public enum AccessorType
        {
            GetNamedValue, // GetStream, GetXmlReader, GetTextReader
            GetFieldValue,
            GetFieldValueAsync
        }

        private static T GetValue<T>(SqlDataReader reader, int ordinal, AccessorType accesor)
        {
            switch (accesor)
            {
                case AccessorType.GetFieldValue:
                    return GetFieldValue<T>(reader, ordinal);
                case AccessorType.GetFieldValueAsync:
                    return GetFieldValueAsync<T>(reader, ordinal);
                case AccessorType.GetNamedValue:
                    return GetNamedValue<T>(reader, ordinal);
                default:
                    throw new NotSupportedException();
            }
        }

        private static T GetFieldValueAsync<T>(SqlDataReader reader, int ordinal)
        {
            return reader.GetFieldValueAsync<T>(ordinal).GetAwaiter().GetResult();
        }

        private static T GetFieldValue<T>(SqlDataReader reader, int ordinal)
        {
            return reader.GetFieldValue<T>(ordinal);
        }

        private static T GetNamedValue<T>(SqlDataReader reader, int ordinal)
        {
            if (typeof(T) == typeof(XmlReader))
            {
                return (T)(object)reader.GetXmlReader(ordinal);
            }
            else if (typeof(T) == typeof(TextReader))
            {
                return (T)(object)reader.GetTextReader(ordinal);
            }
            else if (typeof(T) == typeof(Stream))
            {
                return (T)(object)reader.GetStream(ordinal);
            }
            else
            {
                throw new NotSupportedException($"type {typeof(T).Name} is not a supported field type");
            }
        }


        private static string SetConnectionStringPacketSize(string connectionString, int packetSize)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);
            builder.PersistSecurityInfo = true;
            builder.PacketSize = packetSize;
            return builder.ToString();
        }

        private static byte[] CreateBinaryData(int packetSize, int forcedPacketCount)
        {
            byte[] originalData = new byte[packetSize * forcedPacketCount]; // with header overhead this should cause forcedPacketCount+1 packets of data
            Random random = new Random(100); // static seed for ease of debugging reproducibility
            random.NextBytes(originalData);
            return originalData;
        }

        private static string CreateXmlData(int packetSize, int forcedPacketCount)
        {
            XmlWriterSettings settings = new XmlWriterSettings
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                Encoding = Encoding.Unicode,
                Indent = true,
                OmitXmlDeclaration = true
            };
            StringBuilder buffer = new StringBuilder(2048);
            using (StringWriter stringWriter = new StringWriter(buffer))
            using (XmlWriter xmlWriter = XmlWriter.Create(stringWriter, settings))
            {
                int index = 1;
                xmlWriter.WriteStartElement("root");
                while (buffer.Length / 2 < (packetSize * forcedPacketCount))
                {
                    xmlWriter.WriteStartElement("block");
                    {
                        xmlWriter.WriteStartElement("value1");
                        xmlWriter.WriteValue(index++);
                        xmlWriter.WriteEndElement();

                        xmlWriter.WriteStartElement("value2");
                        xmlWriter.WriteValue(index++);
                        xmlWriter.WriteEndElement();

                        xmlWriter.WriteStartElement("value3");
                        xmlWriter.WriteValue(index++);
                        xmlWriter.WriteEndElement();
                    }
                    xmlWriter.WriteEndElement();
                }
                xmlWriter.WriteEndElement();
            }
            return buffer.ToString();
        }

        private static string CreateBinaryDataQuery(byte[] originalData)
        {
            StringBuilder queryBuilder = new StringBuilder(originalData.Length * 2 + 128);
            queryBuilder.Append("SELECT 1 as DummyField, 0x");
            for (int index = 0; index < originalData.Length; index++)
            {
                queryBuilder.AppendFormat("{0:X2}", originalData[index]);
            }
            queryBuilder.Append(" AS Data");
            return queryBuilder.ToString();
        }

        private static string CreateXmlDataQuery(string originalXml)
        {
            StringBuilder queryBuilder = new StringBuilder(originalXml.Length + 128);
            queryBuilder.Append("SELECT 1 as DummyField, convert(xml,'");
            queryBuilder.Append(originalXml);
            queryBuilder.Append("') AS Data");
            return queryBuilder.ToString();
        }

        private static string CreateTextDataQuery(string originalText)
        {
            StringBuilder queryBuilder = new StringBuilder(originalText.Length + 128);
            queryBuilder.Append("SELECT 1 as DummyField, convert(nvarchar(max),'");
            queryBuilder.Append(originalText);
            queryBuilder.Append("') AS Data");
            return queryBuilder.ToString();
        }

        private static string CreateCharDataQuery(string originalText)
        {
            StringBuilder queryBuilder = new StringBuilder(originalText.Length + 128);
            queryBuilder.Append($"SELECT 1 as DummyField, convert(char({originalText.Length}),'");
            queryBuilder.Append(originalText);
            queryBuilder.Append("') AS Data");
            return queryBuilder.ToString();
        }

        private static string GetXmlDocumentContents(XmlReader xmlReader)
        {
            string outputXml;
            XmlDocument document = new XmlDocument();
            document.Load(xmlReader);

            XmlWriterSettings settings = new XmlWriterSettings
            {
                ConformanceLevel = ConformanceLevel.Document,
                Encoding = Encoding.Unicode,
                Indent = true,
                OmitXmlDeclaration = true
            };

            StringBuilder buffer = new StringBuilder(2048);
            using (StringWriter stringWriter = new StringWriter(buffer))
            using (XmlWriter xmlWriter = XmlWriter.Create(stringWriter, settings))
            {
                document.WriteContentTo(xmlWriter);
            }
            outputXml = buffer.ToString();
            return outputXml;
        }

        private static byte[] GetStreamContents(Stream stream)
        {
            using (MemoryStream buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                buffer.Flush();
                return buffer.ToArray();
            }
        }
    }
}
