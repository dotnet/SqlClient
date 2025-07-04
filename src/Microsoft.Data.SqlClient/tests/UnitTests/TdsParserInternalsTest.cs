// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class TdsParserInternalsTest
    {
        // Selects and returns the first non-public instance constructor for TdsParser 
        private static TdsParser CreateParserInstance()
        {
            Type parserType = typeof(TdsParser);

            ConstructorInfo ctor = parserType
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .First();

            // build default args for each parameter
            object?[] ctorArgs = ctor.GetParameters()
                .Select(p => p.ParameterType.IsValueType
                    ? Activator.CreateInstance(p.ParameterType)
                    : null)
                .ToArray();

            return (TdsParser)ctor.Invoke(ctorArgs);
        }

        // Helper function to extract private _physicalStateObj fields raw buffer and no. of bytes written so far 
        private static (byte[] buffer, int count) ExtractOutputBuffer(TdsParser parser)
        {
            FieldInfo stateField = typeof(TdsParser)
                .GetField("_physicalStateObj", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("_physicalStateObj not found");

            object stateObj = stateField.GetValue(parser)
                ?? throw new InvalidOperationException("physical state object is null");

            Type stateType = stateObj.GetType();

            FieldInfo buffField = stateType
                .GetField("_outBuff", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("_outBuff not found");

            byte[] buffer = (byte[])buffField.GetValue(stateObj)!;

            FieldInfo usedField = stateType
                .GetField("_outBytesUsed", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("_outBytesUsed not found");

            int count = (int)usedField.GetValue(stateObj)!;

            return (buffer, count);
        }

        [Fact]
        public void WriteUserAgentFeatureRequest_WriteFalse_LengthOnlyReturn()
        {
            byte[] payload = Encoding.UTF8.GetBytes("{\"kel\":\"sier\"}");
            var parser = CreateParserInstance();

            int lengthOnly = parser.WriteUserAgentFeatureRequest(payload, write: false);

            // assert: total = 1 (feat-ID) + 4 (len field) + [1 (version) + payload.Length]
            int expectedDataLen = 1 + payload.Length;
            int expectedTotalLen = 1 + 4 + expectedDataLen;
            Assert.Equal(expectedTotalLen, lengthOnly);
        }

        [Fact]
        public void WriteUserAgentFeatureRequest_WriteTrue_AppendsOnlyExtensionBytes()
        {
            byte[] payload = Encoding.UTF8.GetBytes("{\"kel\":\"sier\"}");
            var parser = CreateParserInstance();

            var (bufferBefore, countBefore) = ExtractOutputBuffer(parser);

            int returnedLength = parser.WriteUserAgentFeatureRequest(payload, write: true);

            var (bufferAfter, countAfter) = ExtractOutputBuffer(parser);

            int appended = countAfter - countBefore;
            Assert.Equal(returnedLength, appended);

            int start = countBefore;

            Assert.Equal(
                TdsEnums.FEATUREEXT_USERAGENT,
                bufferAfter[start]);

            int dataLenFromStream = BitConverter.ToInt32(bufferAfter, start + 1);
            int expectedDataLen = 1 + payload.Length;
            Assert.Equal(expectedDataLen, dataLenFromStream);

            Assert.Equal(
                TdsEnums.SUPPORTED_USER_AGENT_VERSION,
                bufferAfter[start + 5]);

            byte[] writtenPayload = bufferAfter
                .Skip(start + 6)
                .Take(payload.Length)
                .ToArray();
            Assert.Equal(payload, writtenPayload);

            Assert.Equal(returnedLength, appended);
        }

    }
}
