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
        private readonly TdsParser _parser = new(false, false);

        // TODO(ADO-37888): Avoid reflection by exposing a way for tests to intercept outbound TDS packets.
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
            byte[] payload = Encoding.UTF8.GetBytes("User-Agent-Payload");
            var (_, countBefore) = ExtractOutputBuffer(_parser);

            int lengthOnly = _parser.WriteUserAgentFeatureRequest(payload, write: false);

            var (_, countAfter) = ExtractOutputBuffer(_parser);

            // assert: total = 1 (feat-ID) + 4 (len field) + payload.Length
            int expectedTotalLen = 1 + 4 + payload.Length;
            Assert.Equal(expectedTotalLen, lengthOnly);

            // assert: no bytes were written when write == false
            Assert.Equal(countBefore, countAfter);
        }

        [Fact]
        public void WriteUserAgentFeatureRequest_WriteTrue_AppendsOnlyExtensionBytes()
        {
            byte[] payload = Encoding.UTF8.GetBytes("User-Agent-Payload");
            var (bufferBefore, countBefore) = ExtractOutputBuffer(_parser);

            int returnedLength = _parser.WriteUserAgentFeatureRequest(payload, write: true);

            var (bufferAfter, countAfter) = ExtractOutputBuffer(_parser);

            // We expect both of these to be the same object
            Assert.Same(bufferBefore, bufferAfter);
            int appended = countAfter - countBefore;
            Assert.Equal(returnedLength, appended);

            int start = countBefore;

            Assert.Equal(
                TdsEnums.FEATUREEXT_USERAGENT,
                bufferAfter[start]);

            int dataLenFromStream = BitConverter.ToInt32(bufferAfter, start + 1);
            Assert.Equal(payload.Length, dataLenFromStream);

            // slice into the existing buffer
            ReadOnlySpan<byte> writtenSpan = new(
                bufferAfter,
                start + 5,
                appended - 5);

            Assert.True(
                writtenSpan.SequenceEqual(payload),
                "Payload bytes did not match");
        }

    }
}
